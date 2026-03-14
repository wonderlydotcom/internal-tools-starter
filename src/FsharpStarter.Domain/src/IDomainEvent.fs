namespace FsharpStarter.Domain

open System

type IDomainEvent =
    abstract member EventId: Guid
    abstract member AggregateId: Guid
    abstract member Version: int
    abstract member OccurredAt: DateTime
    abstract member EventTypeName: string