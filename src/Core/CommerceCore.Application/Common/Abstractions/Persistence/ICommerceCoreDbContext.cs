using CommerceCore.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Common.Abstractions.Persistence;

public interface ICommerceCoreDbContext
{
    DbSet<Product> Products { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}