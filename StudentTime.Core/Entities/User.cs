using System.ComponentModel.DataAnnotations;

namespace StudentTime.Core.Entities;

public class User
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    [MaxLength(255)]
    public string? GoogleId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; } = true;

    public bool EmailVerified { get; set; } = false;

    // Navigation property
    public virtual ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();

    // Validation methods
    public bool HasPasswordAuth() => !string.IsNullOrEmpty(PasswordHash);
    public bool HasGoogleAuth() => !string.IsNullOrEmpty(GoogleId);
}

