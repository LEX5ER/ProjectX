using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ProjectX.POS.Application.Abstractions;

namespace ProjectX.POS.API;

public sealed class ApplicationExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApplicationProblemException exception)
        {
            return;
        }

        context.Result = exception switch
        {
            ApplicationValidationException validationException => new BadRequestObjectResult(
                new ValidationProblemDetails(validationException.Errors.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value))),
            ApplicationForbiddenException forbiddenException => CreateProblemResult(forbiddenException, StatusCodes.Status403Forbidden),
            ApplicationConflictException conflictException => CreateProblemResult(conflictException, StatusCodes.Status409Conflict),
            ApplicationServiceUnavailableException unavailableException => CreateProblemResult(unavailableException, StatusCodes.Status503ServiceUnavailable),
            _ => null
        };

        context.ExceptionHandled = context.Result is not null;
    }

    private static ObjectResult CreateProblemResult(ApplicationProblemException exception, int statusCode)
    {
        return new ObjectResult(new ProblemDetails
        {
            Title = exception.Title,
            Detail = exception.Detail,
            Status = statusCode
        })
        {
            StatusCode = statusCode
        };
    }
}
