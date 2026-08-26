using Catalog.Contracts.Dtos;
using Kernel.Requests;

namespace Catalog.Contracts.Requests;

/// <summary>
/// Cross-module query to retrieve product details by ID.
/// </summary>
public record GetProductSummary(DefaultId ProductId) : IRequest<ProductDto>;
