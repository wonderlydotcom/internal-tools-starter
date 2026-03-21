#!/usr/bin/env python3
import argparse
import os
import shutil
import subprocess
import sys
import tempfile


def parse_secret_spec(value: str) -> tuple[str, str]:
    if "=" in value:
        secret_id, label = value.split("=", 1)
    else:
        secret_id, label = value, value

    secret_id = secret_id.strip()
    label = label.strip() or secret_id

    if not secret_id:
        raise argparse.ArgumentTypeError("secret id must not be empty")

    return secret_id, label


def require_gcloud() -> None:
    if shutil.which("gcloud") is None:
        raise SystemExit("Missing required command: gcloud")


def run(cmd: list[str], *, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(cmd, check=check, text=True, capture_output=True)


def ensure_secret_exists(project: str, secret_id: str, dry_run: bool) -> None:
    if dry_run:
        print(f"[dry-run] would verify secret exists: {secret_id}")
        return

    result = run(["gcloud", "secrets", "describe", secret_id, "--project", project], check=False)
    if result.returncode != 0:
        message = result.stderr.strip() or result.stdout.strip() or "unknown error"
        raise SystemExit(
            f"Secret '{secret_id}' does not appear to exist in project '{project}'. "
            f"Create the secret plumbing in ../internal-tools-infra first. Details: {message}"
        )


def prompt_for_secret(secret_id: str, label: str, end_marker: str) -> str:
    print()
    print(f"Paste the value for {secret_id} ({label}).")
    print(
        f"Finish by entering a line containing only {end_marker}. "
        "The value is not written to git or terraform.tfvars."
    )

    lines: list[str] = []

    while True:
        try:
            line = input()
        except EOFError:
            break

        if line == end_marker:
            break

        lines.append(line)

    value = "\n".join(lines)
    if not value.strip():
        raise SystemExit(f"No value was provided for '{secret_id}'.")

    return value


def upload_secret(project: str, secret_id: str, value: str, dry_run: bool) -> None:
    if dry_run:
        print(f"[dry-run] would upload a new version for {secret_id} in project {project}")
        return

    with tempfile.NamedTemporaryFile("w", delete=False) as handle:
        handle.write(value)
        temp_path = handle.name

    try:
        run(
            [
                "gcloud",
                "secrets",
                "versions",
                "add",
                secret_id,
                "--project",
                project,
                "--data-file",
                temp_path,
            ]
        )
    finally:
        try:
            os.remove(temp_path)
        except FileNotFoundError:
            pass


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Prompt for one-time secret values and upload them to GCP Secret Manager."
    )
    parser.add_argument(
        "--project",
        default=os.environ.get("GCP_PROJECT_ID", "wonderly-idp-sso"),
        help="GCP project id. Defaults to GCP_PROJECT_ID or wonderly-idp-sso.",
    )
    parser.add_argument(
        "--secret",
        dest="secrets",
        action="append",
        required=True,
        type=parse_secret_spec,
        help="Secret spec as secret-id=human label. Repeat for each secret.",
    )
    parser.add_argument(
        "--end-marker",
        default="__END__",
        help="Line marker that ends pasted secret input. Default: __END__",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Validate flow without calling gcloud.",
    )
    args = parser.parse_args()

    if not args.dry_run:
        require_gcloud()

    print(f"Uploading {len(args.secrets)} secret value(s) to project {args.project}.")

    for secret_id, label in args.secrets:
        ensure_secret_exists(args.project, secret_id, args.dry_run)
        value = prompt_for_secret(secret_id, label, args.end_marker)
        upload_secret(args.project, secret_id, value, args.dry_run)
        print(f"Uploaded new version for {secret_id}.")

    print("Done.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
