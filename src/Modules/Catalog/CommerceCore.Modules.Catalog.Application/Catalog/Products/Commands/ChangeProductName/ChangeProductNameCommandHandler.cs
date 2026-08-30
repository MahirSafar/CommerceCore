using CommerceCore.Application.Common.Factories;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.ChangeProductName;

public sealed class ChangeProductNameCommandHandler(
    ICommerceCoreDbContext dbContext)
    : ICommandHandler<ChangeProductNameCommand, ChangeProductNameResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<ChangeProductNameResult?> Handle(
        ChangeProductNameCommand command,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product? product = await _dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == productId,
            cancellationToken);

        if (product is null)
            return null;

        LocalizedText name = LocalizedTextFactory.Create(
            command.DefaultLanguage,
            command.NameTranslations);

        bool changed = product.ChangeName(name);

        if (changed)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new ChangeProductNameResult(
            product.Id.Value,
            product.Name.DefaultLanguage.Value,
            product.Name.Translations.ToDictionary(
                translation => translation.Key.Value,
                translation => translation.Value,
                StringComparer.Ordinal),
            product.Status.ToString());
    }
}