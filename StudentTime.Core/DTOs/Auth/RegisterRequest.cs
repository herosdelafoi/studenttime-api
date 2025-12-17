using System.ComponentModel.DataAnnotations;

namespace StudentTime.Core.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le mot de passe est requis")]
    [MinLength(8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Le mot de passe doit contenir au moins une majuscule, une minuscule et un chiffre")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nom est requis")]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class GoogleAuthRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsFirstLogin { get; set; } = false;
}

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le code est requis")]
    [StringLength(10, MinimumLength = 6, ErrorMessage = "Le code doit contenir entre 6 et 10 caractères")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nouveau mot de passe est requis")]
    [MinLength(8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Le mot de passe doit contenir au moins une majuscule, une minuscule et un chiffre")]
    public string NewPassword { get; set; } = string.Empty;
}

public class ForgotPasswordResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
}

public class VerifyEmailRequest
{
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le code est requis")]
    [StringLength(10, MinimumLength = 6, ErrorMessage = "Le code doit contenir entre 6 et 10 caractères")]
    public string Code { get; set; } = string.Empty;
}

public class VerifyEmailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ResendVerificationRequest
{
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    public string Email { get; set; } = string.Empty;
}

public class ResendVerificationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

