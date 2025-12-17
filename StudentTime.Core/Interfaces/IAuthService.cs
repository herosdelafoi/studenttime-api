using StudentTime.Core.DTOs.Auth;

namespace StudentTime.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> GoogleAuthAsync(GoogleAuthRequest request);
    Task<bool> ValidateTokenAsync(string token);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task<VerifyEmailResponse> VerifyEmailAsync(VerifyEmailRequest request);
    Task<ResendVerificationResponse> ResendVerificationEmailAsync(ResendVerificationRequest request);
}

