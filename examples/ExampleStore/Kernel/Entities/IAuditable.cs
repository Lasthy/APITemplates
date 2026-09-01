namespace Kernel.Entities;

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DefaultId? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    DefaultId? UpdatedBy { get; set; }
}
