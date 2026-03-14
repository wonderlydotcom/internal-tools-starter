namespace FsharpStarter.Domain.Ports

open FsharpStarter.Domain
open FsharpStarter.Domain.Entities
open FsharpStarter.Domain.ValueObjects

type IExampleRepository =
    abstract member Save: ExampleAggregate -> Async<Result<unit, DomainError>>
    abstract member GetById: ExampleId -> Async<Result<ExampleAggregate option, DomainError>>