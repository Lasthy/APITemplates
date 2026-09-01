namespace Kernel.Partitioning;

/// <summary>
/// Represents an inclusive partition range [Start, End] owned by an instance for sharded processing.
/// </summary>
public record PartitionRange
{
    /// <summary>
    /// The inclusive lower bound partition index.
    /// </summary>
    public int Start { get; init; }

    /// <summary>
    /// The inclusive upper bound partition index.
    /// </summary>
    public int End { get; init; }

    public PartitionRange() { }

    public PartitionRange(int start, int end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Checks whether a given partition number falls within this range.
    /// </summary>
    public bool Contains(int partition) => partition >= Start && partition <= End;

    /// <summary>
    /// Enables tuple deconstruction: <c>var (start, end) = range;</c>
    /// </summary>
    public void Deconstruct(out int start, out int end)
    {
        start = Start;
        end = End;
    }
}
