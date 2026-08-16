using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.ChangeProductName;

public sealed record ChangeProductNameCommand(
    Guid ProductId,
    string DefaultLanguage,
    IReadOnlyDictionary<string, string> NameTranslations)
    : ICommand<ChangeProductNameResult?>;

public sealed record ChangeProductNameResult(
    Guid ProductId,
    string DefaultLanguage,
    IReadOnlyDictionary<string, string> NameTranslations,
    string Status);