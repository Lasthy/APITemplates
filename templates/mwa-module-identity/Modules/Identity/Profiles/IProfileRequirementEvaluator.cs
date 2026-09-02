using Kernel.Common;
using Kernel.Context;

namespace Identity.Profiles;

/// <summary>
/// Service responsible for evaluating profile creation requirements.
/// </summary>
public interface IProfileRequirementEvaluator
{
    /// <summary>
    /// Evaluates whether the caller described in <paramref name="context"/> satisfies all creation requirements
    /// for the specified profile.
    /// </summary>
    Task<Result> EvaluateAsync(
        ProfileCreationRequirementContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Convenience overload to evaluate creation requirements by profile name and scope context.
    /// </summary>
    Task<Result> EvaluateAsync(
        string profileName,
        IScopeContext scopeContext,
        string? email = null,
        string? userName = null,
        IServiceProvider? services = null,
        CancellationToken ct = default);
}
