using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;

public interface IProductTypeSchemaCoordinator
{
    Task ExecuteCreationAsync(
        ProductTypeId newProductTypeId,
        ProductTypeId? parentProductTypeId,
        Func<CancellationToken, Task> persistAsync,
        CancellationToken cancellationToken = default);

    Task ExecuteSchemaChangeAsync(
        ProductTypeId affectedProductTypeId,
        Func<CancellationToken, Task> persistAsync,
        CancellationToken cancellationToken = default);
}