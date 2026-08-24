namespace Kernel.Partitioning;

/// <summary>
/// Manages partition number generation for distributed processing and message outbox sharding.
/// </summary>
public interface IPartitionManager
{
    /// <summary>
    /// Returns a random partition number within the total application partition range.
    /// </summary>
    int GetRandomPartition();
}
