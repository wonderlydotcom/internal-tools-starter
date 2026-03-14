namespace FsharpStarter.Application.DTOs

open System

[<CLIMutable>]
type CreateExampleRequestDto = { Name: string }

[<CLIMutable>]
type ExampleResponseDto = {
    Id: Guid
    Name: string
    CreatedAt: DateTime
    Version: int
}