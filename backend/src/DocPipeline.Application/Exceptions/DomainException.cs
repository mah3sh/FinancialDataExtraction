namespace DocPipeline.Application.Exceptions;

public class DomainException(string message) : Exception(message);

public class NotFoundException(string entity, object id)
    : DomainException($"{entity} with id '{id}' was not found.");

public class ValidationException(string message) : DomainException(message);

public class UnauthorizedException(string message) : DomainException(message);
