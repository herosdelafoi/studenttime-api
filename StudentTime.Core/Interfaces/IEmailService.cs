namespace StudentTime.Core.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetCodeAsync(string email, string code, string displayName);
    
    Task SendEmailVerificationCodeAsync(string email, string code, string displayName);
    
    /// <summary>
    /// Valide la configuration SMTP et retourne un rapport de diagnostic
    /// </summary>
    EmailConfigurationValidationResult ValidateConfiguration();
}

public class EmailConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, string> Configuration { get; set; } = new();
}

