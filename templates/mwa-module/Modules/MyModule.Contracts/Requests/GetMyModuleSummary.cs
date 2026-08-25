using Kernel.Requests;
using MyModule.Contracts.Dtos;

namespace MyModule.Contracts.Requests;

/// <summary>
/// CQRS query request to fetch summary details of a MyModule resource.
/// Dispatched across modules via <see cref="Kernel.Requests.IDispatcher"/>.
/// </summary>
public record GetMyModuleSummary(DefaultId Id) : IRequest<MyModuleSummaryDto>;
