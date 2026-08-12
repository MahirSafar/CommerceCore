using CommerceCore.Domain.Common.Exceptions;

namespace CommerceCore.Domain.Catalog.Products.Exceptions;

public sealed class ProductDomainException(string code, string message) : DomainException(code, message)
{
}