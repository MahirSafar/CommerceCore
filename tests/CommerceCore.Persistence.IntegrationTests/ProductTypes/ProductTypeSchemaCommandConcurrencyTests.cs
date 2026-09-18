using CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;
using CommerceCore.Application.Catalog.ProductTypes.Commands.DefineAttribute;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ProductTypeSchemaCommandConcurrencyTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task DefineAttribute_ConcurrentCommandsWithSameDisplayOrder_ThrowsDomainExceptionInsteadOfDbUpdateException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        fixture.SetTenantForCurrentTest();

        Guid productTypeId;

        await using (AsyncServiceScope seedScope = fixture.Services.CreateAsyncScope())
        {
            CommerceCoreDbContext dbContext = seedScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();
            IProductTypeSchemaCoordinator coordinator = seedScope.ServiceProvider
                .GetRequiredService<IProductTypeSchemaCoordinator>();
            ITenantContext tenantContext = seedScope.ServiceProvider
                .GetRequiredService<ITenantContext>();

            CreateProductTypeCommandHandler createHandler = new(
                dbContext,
                coordinator,
                tenantContext);

            CreateProductTypeResult result = await createHandler.Handle(
                new CreateProductTypeCommand(
                    $"type_{Guid.NewGuid():N}"[..16],
                    ParentProductTypeId: null,
                    IsAssignable: false),
                cancellationToken);

            productTypeId = result.ProductTypeId;
        }

        var command1 = new DefineAttributeCommand(
            productTypeId,
            Key: "attr_concurrency_1",
            DataType: AttributeDataType.Text,
            Scope: AttributeScope.ProductSpecification,
            IsRequired: false,
            DisplayOrder: 0,
            MinimumValue: null,
            MaximumValue: null,
            MinimumLength: null,
            MaximumLength: null,
            MeasurementUnitFamily: null);

        var command2 = new DefineAttributeCommand(
            productTypeId,
            Key: "attr_concurrency_2",
            DataType: AttributeDataType.Text,
            Scope: AttributeScope.ProductSpecification,
            IsRequired: false,
            DisplayOrder: 0,
            MinimumValue: null,
            MaximumValue: null,
            MinimumLength: null,
            MaximumLength: null,
            MeasurementUnitFamily: null);

        using var fakeCoordinator = new SynchronizedFakeSchemaCoordinator();
        var fakeRegistry = new NoOpAttributeDefinitionRegistry();

        await using AsyncServiceScope scope1 = fixture.Services.CreateAsyncScope();
        CommerceCoreDbContext dbContext1 = scope1.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();
        DefineAttributeCommandHandler handler1 = new(
            dbContext1,
            fakeCoordinator,
            fakeRegistry);

        await using AsyncServiceScope scope2 = fixture.Services.CreateAsyncScope();
        CommerceCoreDbContext dbContext2 = scope2.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();
        DefineAttributeCommandHandler handler2 = new(
            dbContext2,
            fakeCoordinator,
            fakeRegistry);

        List<Exception> results = [];
        object syncLock = new();

        Task task1 = Task.Run(async () =>
        {
            try
            {
                await handler1.Handle(command1, cancellationToken);
            }
            catch (Exception ex)
            {
                lock (syncLock)
                {
                    results.Add(ex);
                }
            }
        }, cancellationToken);

        Task task2 = Task.Run(async () =>
        {
            try
            {
                await handler2.Handle(command2, cancellationToken);
            }
            catch (Exception ex)
            {
                lock (syncLock)
                {
                    results.Add(ex);
                }
            }
        }, cancellationToken);

        await Task.WhenAll(task1, task2);

        ProductTypeDomainException exception = Assert.Single(
            results.OfType<ProductTypeDomainException>());

        Assert.Equal(
            "product_type.duplicate_attribute_display_order",
            exception.Code);

        Assert.DoesNotContain(results, error => error is DbUpdateException);

        await using AsyncServiceScope freshScope = fixture.Services.CreateAsyncScope();
        CommerceCoreDbContext freshDbContext = freshScope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        ProductType reloadedProductType = await freshDbContext.ProductTypes
            .Include(item => item.AttributeDefinitions)
            .SingleAsync(
                item => item.Id == ProductTypeId.From(productTypeId),
                cancellationToken);

        Assert.Single(reloadedProductType.AttributeDefinitions);
    }

    private sealed class SynchronizedFakeSchemaCoordinator : IProductTypeSchemaCoordinator, IDisposable
    {
        private readonly TaskCompletionSource _bothArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _gate = new(1, 1);
        private int _arrivedCount;

        public Task ExecuteCreationAsync(
            ProductTypeId newProductTypeId,
            ProductTypeId? parentProductTypeId,
            Func<CancellationToken, Task> persistAsync,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task ExecuteSchemaChangeAsync(
            ProductTypeId affectedProductTypeId,
            Func<CancellationToken, Task> persistAsync,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivedCount) >= 2)
            {
                _bothArrived.TrySetResult();
            }
            else
            {
                await _bothArrived.Task.WaitAsync(cancellationToken);
            }

            await _gate.WaitAsync(cancellationToken);
            try
            {
                await persistAsync(cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            _gate.Dispose();
        }
    }

    private sealed class NoOpAttributeDefinitionRegistry : IAttributeDefinitionRegistry
    {
        public Task EnsureKeyIsUniqueInHierarchyAsync(
            ProductTypeId productTypeId,
            AttributeKey attributeKey,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
