using Kernel.Requests;
using Orders.Contracts.Dtos;

namespace Orders.Contracts.Requests;

/// <summary>
/// CQRS query request to fetch summary details of a Orders resource.
/// Dispatched across modules via <see cref="Kernel.Requests.IDispatcher"/>.
/// </summary>
public record GetOrdersSummary(DefaultId Id) : IRequest<OrdersSummaryDto>;
