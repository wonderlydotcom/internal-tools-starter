namespace FsharpStarter.Domain.ValueObjects

open System

[<Struct>]
type ExampleId =
    private
    | ExampleId of Guid

    member this.Value =
        let (ExampleId value) = this
        value

    static member New() = ExampleId(Guid.NewGuid())

    static member OfGuid(id: Guid) = ExampleId(id)

    static member TryParse(value: string) =
        match Guid.TryParse(value) with
        | true, guid -> Some(ExampleId guid)
        | false, _ -> None

module ExampleId =
    let value (exampleId: ExampleId) = exampleId.Value