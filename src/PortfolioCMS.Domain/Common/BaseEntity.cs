namespace PortfolioCMS.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// Provides Id, audit timestamps, and soft-delete support.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
