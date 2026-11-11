using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RequestsPortal.Core.Exceptions;

namespace RequestsPortal.Api.ExceptionHandling;

public sealed class RequestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            RequestValidationException validation => new ValidationProblemDetails(validation.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Datos de solicitud inválidos."
            },
            RequestConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "La solicitud entra en conflicto con una existente.",
                Detail = conflict.Message
            },
            _ => null
        };

        if (problem is null)
        {
            return false;
        }

        await Results.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }
}
