using CommerceCore.Domain.Common.Exceptions;

namespace CommerceCore.Domain.Catalog.ProductTypes.Exceptions;

public sealed class ProductTypeDomainException(string code, string message)
    : DomainException(code, message);