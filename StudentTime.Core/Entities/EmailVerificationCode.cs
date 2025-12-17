using System.ComponentModel.DataAnnotations;

namespace StudentTime.Core.Entities;

public class EmailVerificationCode
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public bool IsValid => !IsUsed && !IsExpired;
}

