using CommerceCore.Domain.Common;

namespace CommerceCore.Domain.Catalog.Products.Exceptions;

public sealed class ProductDomainException(string message) : DomainException(message)
{
}