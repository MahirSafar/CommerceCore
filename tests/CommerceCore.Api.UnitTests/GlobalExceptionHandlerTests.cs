using System.Text.Json;
using CommerceCore.Api.Common.Errors;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CommerceCore.Api.UnitTests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WithLastActiveVariantRule_ReturnsConflict()
    {
        DefaultHttpContext context = new();
        GlobalExceptionHandler handler = new(
            NullLogger<GlobalExceptionHandler>.Instance);

        bool handled = await handler.TryHandleAsync(
            context,
            new ProductDomainException(
                "product.last_active_variant_cannot_be_deactivated",
                "Deactivate the product before deactivating its last active variant."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status409Conflict,
            context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_WithOtherDomainRule_ReturnsUnprocessableEntity()
    {
        DefaultHttpContext context = new();
        GlobalExceptionHandler handler = new(
            NullLogger<GlobalExceptionHandler>.Instance);

        bool handled = await handler.TryHandleAsync(
            context,
            new ProductDomainException(
                "product.activation_requires_active_variant",
                "A product requires at least one active variant before activation."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status422UnprocessableEntity,
            context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_WithUniqueConstraintViolation_ReturnsConflict()
    {
        DefaultHttpContext context = new();

        GlobalExceptionHandler handler = new(
            NullLogger<GlobalExceptionHandler>.Instance);

        DbUpdateException exception = new(
            "Duplicate value.",
            new PostgresException(
                "duplicate key value violates unique constraint",
                "ERROR",
                "ERROR",
                "23505"));

        bool handled = await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status409Conflict,
            context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_WithForeignKeyViolation_ReturnsConflict()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();

        GlobalExceptionHandler handler = new(
            NullLogger<GlobalExceptionHandler>.Instance);

        DbUpdateException exception = new(
            "Foreign key violation.",
            new PostgresException(
                "insert or update on table violates foreign key constraint",
                "ERROR",
                "ERROR",
                PostgresErrorCodes.ForeignKeyViolation));

        bool handled = await handler.TryHandleAsync(
            context,
            exception,
            cancellationToken);

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status409Conflict,
            context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: cancellationToken);
        Assert.Equal(
            "/problems/referenced-resource-conflict",
            document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_WithCheckViolation_ReturnsUnprocessableEntity()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();

        GlobalExceptionHandler handler = new(
            NullLogger<GlobalExceptionHandler>.Instance);

        DbUpdateException exception = new(
            "Check violation.",
            new PostgresException(
                "new row for relation violates check constraint",
                "ERROR",
                "ERROR",
                PostgresErrorCodes.CheckViolation));

        bool handled = await handler.TryHandleAsync(
            context,
            exception,
            cancellationToken);

        Assert.True(handled);
        Assert.Equal(
            StatusCodes.Status422UnprocessableEntity,
            context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: cancellationToken);
        Assert.Equal(
            "/problems/database-constraint-violation",
            document.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_WithCancelledRequest_DoesNotWriteProblemResponse()
    {
        var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        DefaultHttpContext context = new()
        {
            RequestAborted = cancellationSource.Token
        };
        context.Response.Body = new MemoryStream();

        GlobalExceptionHandler handler = new(
            NullLogger<GlobalExceptionHandler>.Instance);

        bool handled = await handler.TryHandleAsync(
            context,
            new OperationCanceledException(),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(0, context.Response.Body.Length);
    }
}
