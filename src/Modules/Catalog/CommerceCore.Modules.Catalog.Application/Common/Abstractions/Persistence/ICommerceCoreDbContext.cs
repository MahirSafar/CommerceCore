using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.ProductTypes;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;

public interface ICommerceCoreDbContext
{
    DbSet<Product> Products { get; }
    DbSet<ProductType> ProductTypes { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}