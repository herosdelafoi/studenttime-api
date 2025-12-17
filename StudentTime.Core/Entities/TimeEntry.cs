using System.ComponentModel.DataAnnotations;

namespace StudentTime.Core.Entities;

public class TimeEntry
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int? DurationSeconds { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;

    // Navigation property
    public virtual User User { get; set; } = null!;

    // Computed properties
    public bool IsActive => EndTime == null;

    public int CalculateDuration()
    {
        if (EndTime == null) return 0;
        return (int)(EndTime.Value - StartTime).TotalSeconds;
    }

    public void Complete()
    {
        EndTime = DateTime.UtcNow;
        DurationSeconds = CalculateDuration();
        UpdatedAt = DateTime.UtcNow;
    }
}

