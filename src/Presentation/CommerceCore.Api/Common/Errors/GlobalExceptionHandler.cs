using CommerceCore.Domain.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CommerceCore.Api.Common.Errors;

public sealed partial class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    [LoggerMessage(
        LogLevel.Error,
        "Unhandled exception. TraceId: {TraceId}")]
    private static partial void LogUnhandledException(
        ILogger logger,
        Exception exception,
        string traceId);

    private static Task WriteProblemAsync<TProblemDetails>(
        HttpContext httpContext,
        TProblemDetails problem,
        CancellationToken cancellationToken)
        where TProblemDetails : ProblemDetails
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

    private static int GetDomainProblemStatusCode(string code) => code switch
    {
        "product.last_active_variant_cannot_be_deactivated" or
        "product.active_default_variant_cannot_be_deactivated" or
        "product.active_default_variant_must_be_active" =>
            StatusCodes.Status409Conflict,

        _ => StatusCodes.Status422UnprocessableEntity
    };

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
            Status = GetDomainProblemStatusCode(exception.Code),
            Instance = httpContext.Request.Path
        };

        problem.Extensions["code"] = exception.Code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        await WriteProblemAsync(
            httpContext,
            problem,
            cancellationToken);
    }

    private static Task WriteConcurrencyProblemAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        ProblemDetails problem = new()
        {
            Type = "/problems/concurrency-conflict",
            Title = "The resource was modified by another request.",
            Detail = "Reload the resource and try again.",
            Status = StatusCodes.Status409Conflict,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return WriteProblemAsync(httpContext, problem, cancellationToken);
    }

    private static Task WriteUniqueConstraintProblemAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem = new()
        {
            Type = "/problems/unique-constraint-conflict",
            Title = "A resource with the same unique value already exists.",
            Status = StatusCodes.Status409Conflict,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return WriteProblemAsync(httpContext, problem, cancellationToken);
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

            case DbUpdateException
            {
                InnerException: PostgresException { SqlState: "23505" }
            }:
                await WriteUniqueConstraintProblemAsync(
                    httpContext,
                    cancellationToken);

                return true;

            case DbUpdateConcurrencyException:
                await WriteConcurrencyProblemAsync(
                    httpContext,
                    cancellationToken);

                return true;
            default:
                LogUnhandledException(
                    _logger,
                    exception,
                    httpContext.TraceIdentifier);

                await WriteUnexpectedProblemAsync(
                    httpContext,
                    cancellationToken);

                return true;
        }
    }

}
