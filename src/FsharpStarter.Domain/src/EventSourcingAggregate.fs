namespace FsharpStarter.Domain

[<AbstractClass>]
type EventSourcingAggregate<'TEvent>() =
    let uncommittedEvents = ResizeArray<'TEvent>()
    let mutable version = 0

    member _.Version = version

    member _.GetUncommittedEvents() = uncommittedEvents |> Seq.toList

    member _.ClearUncommittedEvents() = uncommittedEvents.Clear()

    member private _.SetVersion(newVersion: int) = version <- newVersion

    member this.LoadFromHistory(events: 'TEvent list, apply: 'TEvent -> unit, getVersion: 'TEvent -> int) =
        for domainEvent in events do
            apply domainEvent
            this.SetVersion(getVersion domainEvent)

    member this.Raise(domainEvent: 'TEvent, apply: 'TEvent -> unit, getVersion: 'TEvent -> int) =
        apply domainEvent
        this.SetVersion(getVersion domainEvent)
        uncommittedEvents.Add(domainEvent)