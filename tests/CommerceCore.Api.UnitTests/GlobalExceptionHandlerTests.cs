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
}