using Kernel.Requests;
using Catalog.Contracts.Dtos;

namespace Catalog.Contracts.Requests;

/// <summary>
/// CQRS query request to fetch summary details of a Catalog resource.
/// Dispatched across modules via <see cref="Kernel.Requests.IDispatcher"/>.
/// </summary>
public record GetCatalogSummary(DefaultId Id) : IRequest<CatalogSummaryDto>;
