using Kernel.Configuration;

namespace Kernel.Partitioning;

/// <summary>
/// Default implementation of <see cref="IPartitionManager"/>.
/// Generates a random partition number within the range owned by this instance,
/// as configured in <c>settings.Instance.json</c> → <c>InstanceSettings.Partition</c>.
/// </summary>
public class PartitionManager : IPartitionManager
{
    private readonly InstanceSettings _instanceSettings;

    public PartitionManager(InstanceSettings instanceSettings)
    {
        _instanceSettings = instanceSettings;
    }

    /// <summary>
    /// Returns a random partition number in the range [Start, End] assigned to this instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the partition range is invalid (Start > End).
    /// </exception>
    public int GetRandomPartition()
    {
        var (start, end) = _instanceSettings.Partition;

        if (start > end)
        {
            throw new InvalidOperationException(
                $"Invalid partition range configured: Start ({start}) > End ({end}). " +
                $"Check InstanceSettings in settings.Instance.json.");
        }

        // Random.Shared is thread-safe in .NET 6+.
        return Random.Shared.Next(start, end + 1);
    }
}
