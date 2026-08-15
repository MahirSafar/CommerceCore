using CommerceCore.Domain.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CommerceCore.Api.Common.Errors;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    private static Task WriteProblemAsync(
        HttpContext httpContext,
        ProblemDetails problem,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = problem.Status
            ?? StatusCodes.Status500InternalServerError;

        return httpContext.Response.WriteAsJsonAsync(
            value: problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    }
    private static async Task WriteValidationProblemAsync(HttpContext httpContext,
                                                          ValidationException exception,
                                                          CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = exception.Errors
            .GroupBy(failure =>
                string.IsNullOrWhiteSpace(failure.PropertyName)
                    ? "request"
                    : failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(failure => failure.ErrorMessage)
                    .Distinct()
                    .ToArray());

        ValidationProblemDetails problem = new(errors)
        {
            Type = "/problems/validation",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await WriteProblemAsync(
            httpContext,
            problem,
            cancellationToken);
    }
    private static async Task WriteInvalidRequestProblemAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var problem = new ProblemDetails
        {
            Type = "/problems/invalid-request",
            Title = "The request body is invalid.",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await WriteProblemAsync(
            httpContext,
            problem,
            cancellationToken);
    }
    private static async Task WriteUnexpectedProblemAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var problem = new ProblemDetails
        {
            Type = "/problems/internal-server-error",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await WriteProblemAsync(
            httpContext,
            problem,
            cancellationToken);
    }
    private static async Task WriteDomainProblemAsync(
       HttpContext httpContext,
       DomainException exception,
       CancellationToken cancellationToken)
    {
        var problem = new ProblemDetails
        {
            Type = $"/problems/{exception.Code}",
            Title = "A business rule was violated.",
            Detail = exception.Message,
            Status = StatusCodes.Status422UnprocessableEntity,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["code"] = exception.Code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await WriteProblemAsync(
            httpContext,
            problem,
            cancellationToken);
    }
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case BadHttpRequestException:
                await WriteInvalidRequestProblemAsync(
                    httpContext,
                    cancellationToken);

                return true;

            case ValidationException validationException:
                await WriteValidationProblemAsync(
                    httpContext,
                    validationException,
                    cancellationToken);

                return true;

            case DomainException domainException:
                await WriteDomainProblemAsync(
                    httpContext,
                    domainException,
                    cancellationToken);

                return true;

            default:
                _logger.LogError(
                    exception,
                    "Unhandled exception. TraceId: {TraceId}",
                    httpContext.TraceIdentifier);

                await WriteUnexpectedProblemAsync(
                    httpContext,
                    cancellationToken);

                return true;
        }
    }

}
