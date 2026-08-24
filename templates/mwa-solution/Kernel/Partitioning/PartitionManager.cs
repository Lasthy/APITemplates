using Kernel.Configuration;

namespace Kernel.Partitioning;

/// <summary>
/// Default implementation of <see cref="IPartitionManager"/>.
/// Generates a random partition number within the total partition space,
/// as configured in <c>appsettings.Application.json</c> → <c>ApplicationSettings.PartitionAmount</c>.
/// </summary>
public class PartitionManager : IPartitionManager
{
    private readonly ApplicationSettings _applicationSettings;

    public PartitionManager(ApplicationSettings applicationSettings)
    {
        _applicationSettings = applicationSettings;
    }

    /// <summary>
    /// Returns a random partition number in the range [0, PartitionAmount - 1] as configured in application settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when PartitionAmount is less than or equal to 0.
    /// </exception>
    public int GetRandomPartition()
    {
        var amount = _applicationSettings.PartitionAmount;

        if (amount <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid PartitionAmount configured: {amount}. PartitionAmount must be greater than 0. " +
                $"Check ApplicationSettings in appsettings.Application.json.");
        }

        // Random.Shared is thread-safe in .NET 6+.
        return Random.Shared.Next(amount);
    }
}
