namespace FsharpStarter.Infrastructure.Database

open System
open Microsoft.EntityFrameworkCore

type ExampleRecord() =
    member val Id = Guid.Empty with get, set
    member val Name = String.Empty with get, set
    member val CreatedAt = DateTime.MinValue with get, set
    member val Version = 0 with get, set

type DomainEventRecord() =
    member val EventId = Guid.Empty with get, set
    member val AggregateId = Guid.Empty with get, set
    member val Version = 0 with get, set
    member val OccurredAt = DateTime.MinValue with get, set
    member val EventType = String.Empty with get, set
    member val PayloadJson = String.Empty with get, set

type FsharpStarterDbContext(options: DbContextOptions<FsharpStarterDbContext>) =
    inherit DbContext(options)

    [<DefaultValue>]
    val mutable private examples: DbSet<ExampleRecord>

    [<DefaultValue>]
    val mutable private domainEvents: DbSet<DomainEventRecord>

    member this.Examples
        with get () = this.examples
        and set value = this.examples <- value

    member this.DomainEvents
        with get () = this.domainEvents
        and set value = this.domainEvents <- value

    override _.OnModelCreating(modelBuilder: ModelBuilder) =
        let examples = modelBuilder.Entity<ExampleRecord>()
        examples.ToTable("examples") |> ignore
        examples.HasKey("Id") |> ignore
        examples.Property(fun row -> row.Name).IsRequired() |> ignore

        let events = modelBuilder.Entity<DomainEventRecord>()
        events.ToTable("domain_events") |> ignore
        events.HasKey("EventId") |> ignore
        events.Property(fun row -> row.EventType).IsRequired() |> ignore
        events.Property(fun row -> row.PayloadJson).IsRequired() |> ignore

        events.HasIndex([| "AggregateId"; "Version" |]).IsUnique() |> ignore