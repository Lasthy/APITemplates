namespace Kernel.Entities;

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
    DefaultId? DeletedBy { get; set; }
}
