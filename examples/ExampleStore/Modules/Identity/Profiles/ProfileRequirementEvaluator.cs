using Kernel.Common;
using Kernel.Context;

namespace Identity.Profiles;

/// <summary>
/// Default implementation of <see cref="IProfileRequirementEvaluator"/>.
/// </summary>
public class ProfileRequirementEvaluator : IProfileRequirementEvaluator
{
    private readonly ProfileRegistry _profileRegistry;

    public ProfileRequirementEvaluator(ProfileRegistry profileRegistry)
    {
        _profileRegistry = profileRegistry ?? throw new ArgumentNullException(nameof(profileRegistry));
    }

    /// <inheritdoc/>
    public async Task<Result> EvaluateAsync(
        ProfileCreationRequirementContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // System super-users have unrestricted creation privileges
        if (context.ScopeContext.IsSystem)
        {
            return Result.Success();
        }

        var requirements = context.Profile.CreationRequirements;
        foreach (var req in requirements)
        {
            var result = await req.EvaluateAsync(context, ct);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> EvaluateAsync(
        string profileName,
        IScopeContext scopeContext,
        string? email = null,
        string? userName = null,
        IServiceProvider? services = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ArgumentNullException.ThrowIfNull(scopeContext);

        var profile = _profileRegistry.Get(profileName);
        if (profile == null)
        {
            return Result.Failure(new InvalidOperationException($"Profile '{profileName}' is not registered."));
        }

        var context = new ProfileCreationRequirementContext
        {
            ScopeContext = scopeContext,
            Profile = profile,
            Email = email,
            UserName = userName,
            Services = services
        };

        return await EvaluateAsync(context, ct);
    }
}
