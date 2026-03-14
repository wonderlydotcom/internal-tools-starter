namespace FsharpStarter.Domain

type DomainError =
    | ValidationError of message: string
    | NotFound of message: string
    | Conflict of message: string
    | PersistenceError of message: string

module DomainError =
    let message =
        function
        | ValidationError msg
        | NotFound msg
        | Conflict msg
        | PersistenceError msg -> msg