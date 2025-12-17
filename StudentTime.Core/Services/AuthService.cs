using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StudentTime.Core.DTOs.Auth;
using StudentTime.Core.Entities;
using StudentTime.Core.Exceptions;
using StudentTime.Core.Interfaces;
using BCrypt.Net;
using Google.Apis.Auth;

namespace StudentTime.Core.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetCodeRepository _passwordResetCodeRepository;
    private readonly IEmailVerificationCodeRepository _emailVerificationCodeRepository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IPasswordResetCodeRepository passwordResetCodeRepository,
        IEmailVerificationCodeRepository emailVerificationCodeRepository,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordResetCodeRepository = passwordResetCodeRepository;
        _emailVerificationCodeRepository = emailVerificationCodeRepository;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await _userRepository.EmailExistsAsync(request.Email))
        {
            throw new BusinessException("Un compte existe déjà avec cet email");
        }

        var user = new User
        {
            Email = request.Email.ToLower(),
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12),
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false // Nouveau compte non vérifié
        };

        await _userRepository.AddAsync(user);

        // Envoyer l'email de vérification
        try
        {
            var random = new Random();
            var code = random.Next(100000, 999999).ToString();

            var verificationCode = new EmailVerificationCode
            {
                Email = request.Email.ToLower(),
                Code = code,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24) // Code valide 24 heures
            };

            await _emailVerificationCodeRepository.AddAsync(verificationCode);
            
            _logger.LogInformation("Code de vérification créé pour {Email}: {Code}", user.Email, code);
            
            // Envoyer l'email de manière asynchrone (fire-and-forget)
            // Mais avec un meilleur logging pour diagnostiquer les problèmes
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("📤 Début de l'envoi de l'email de vérification à {Email}...", user.Email);
                    await _emailService.SendEmailVerificationCodeAsync(user.Email, code, user.DisplayName);
                    _logger.LogInformation("✅ Email de vérification envoyé avec succès à {Email}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erreur lors de l'envoi de l'email de vérification à {Email}. Détails: {Message}", 
                        user.Email, ex.Message);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création du code de vérification pour {Email}", user.Email);
            // Ne pas faire échouer l'inscription si l'email échoue
        }

        return GenerateAuthResponse(user, true); // Première connexion après inscription
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            _logger.LogInformation("Tentative de connexion pour l'email: {Email}", request.Email);
            
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null || !user.HasPasswordAuth())
            {
                _logger.LogWarning("Utilisateur introuvable ou sans mot de passe pour: {Email}", request.Email);
                throw new BusinessException("Email ou mot de passe incorrect");
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Mot de passe incorrect pour: {Email}", request.Email);
                throw new BusinessException("Email ou mot de passe incorrect");
            }

            // Vérifier que l'email est vérifié avant de permettre la connexion
            if (!user.EmailVerified)
            {
                _logger.LogWarning("Tentative de connexion avec email non vérifié: {Email}", user.Email);
                throw new BusinessException("Votre adresse email n'est pas encore vérifiée. Veuillez vérifier votre email avant de vous connecter.");
            }

            // Détecter si c'est la première connexion (LastLoginAt est null)
            var isFirstLogin = user.LastLoginAt == null;

            _logger.LogInformation("Mise à jour de LastLoginAt pour l'utilisateur {UserId}", user.Id);
            try
            {
                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);
                _logger.LogInformation("LastLoginAt mis à jour avec succès pour l'utilisateur {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la mise à jour de LastLoginAt pour l'utilisateur {UserId}. Détails: {Message}", user.Id, ex.Message);
                _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner exception: {Message}", ex.InnerException.Message);
                }
                throw;
            }

            return GenerateAuthResponse(user, isFirstLogin);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la connexion pour l'email: {Email}. Détails: {Message}", request.Email, ex.Message);
            throw;
        }
    }

    public async Task<AuthResponse> GoogleAuthAsync(GoogleAuthRequest request)
    {
        var clientId = _configuration["Google:ClientId"] ?? "";
        _logger.LogInformation("Tentative de validation Google OAuth avec ClientId: {ClientId}", 
            string.IsNullOrEmpty(clientId) ? "NON CONFIGURÉ" : clientId.Substring(0, Math.Min(20, clientId.Length)) + "...");
        
        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogError("Google:ClientId n'est pas configuré dans les variables d'environnement");
            throw new BusinessException("Configuration Google OAuth manquante. Veuillez contacter le support.");
        }
        
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { clientId }
                });
            
            _logger.LogInformation("Token Google validé avec succès pour {Email}", payload.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la validation du token Google. Type: {Type}, Message: {Message}", 
                ex.GetType().Name, ex.Message);
            throw new BusinessException("Token Google invalide ou expiré. Veuillez réessayer.");
        }

        // Chercher d'abord par GoogleId
        var user = await _userRepository.GetByGoogleIdAsync(payload.Subject);
        bool isFirstLogin = false;
        
        if (user == null)
        {
            // Si pas trouvé par GoogleId, chercher par email
            var existingUserByEmail = await _userRepository.GetByEmailAsync(payload.Email);
            
            if (existingUserByEmail != null)
            {
                // L'utilisateur existe déjà avec cet email
                if (existingUserByEmail.EmailVerified)
                {
                    // Email vérifié → lier automatiquement le compte Google
                    _logger.LogInformation("Liaison du compte Google à l'utilisateur existant {Email} (email vérifié)", payload.Email);
                    
                    existingUserByEmail.GoogleId = payload.Subject;
                    
                    // Mettre à jour le DisplayName si vide ou différent
                    if (string.IsNullOrEmpty(existingUserByEmail.DisplayName) || 
                        existingUserByEmail.DisplayName != payload.Name)
                    {
                        existingUserByEmail.DisplayName = payload.Name;
                    }
                    
                    // Marquer l'email comme vérifié (au cas où)
                    existingUserByEmail.EmailVerified = true;
                    
                    isFirstLogin = existingUserByEmail.LastLoginAt == null;
                    existingUserByEmail.LastLoginAt = DateTime.UtcNow;
                    
                    await _userRepository.UpdateAsync(existingUserByEmail);
                    user = existingUserByEmail;
                }
                else
                {
                    // Email non vérifié → demander de vérifier d'abord
                    throw new BusinessException(
                        "Votre adresse email n'est pas encore vérifiée. Veuillez vérifier votre email avant de lier votre compte Google.");
                }
            }
            else
            {
                // Nouvel utilisateur → créer le compte
                _logger.LogInformation("Création d'un nouveau compte avec Google pour {Email}", payload.Email);
                
                user = new User
                {
                    Email = payload.Email.ToLower(),
                    GoogleId = payload.Subject,
                    DisplayName = payload.Name,
                    CreatedAt = DateTime.UtcNow,
                    EmailVerified = true // Google vérifie déjà l'email
                };
                await _userRepository.AddAsync(user);
                isFirstLogin = true;
            }
        }
        else
        {
            // Utilisateur trouvé par GoogleId → connexion normale
            // Si l'utilisateur a déjà un GoogleId, c'est qu'il s'est déjà connecté avec Google
            // donc son email est vérifié par Google (on le marque comme vérifié)
            isFirstLogin = user.LastLoginAt == null;
            user.LastLoginAt = DateTime.UtcNow;
            
            // Mettre à jour le DisplayName si nécessaire
            if (string.IsNullOrEmpty(user.DisplayName) || user.DisplayName != payload.Name)
            {
                user.DisplayName = payload.Name;
            }
            
            // S'assurer que l'email est marqué comme vérifié (Google vérifie l'email)
            user.EmailVerified = true;
            
            await _userRepository.UpdateAsync(user);
        }

        return GenerateAuthResponse(user, isFirstLogin);
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        
        // Pour des raisons de sécurité, on ne révèle pas si l'email existe ou non
        if (user == null || !user.HasPasswordAuth())
        {
            // Retourner un succès même si l'email n'existe pas (security best practice)
            return new ForgotPasswordResponse
            {
                Success = true,
                Message = "Si cet email existe dans notre système, un code de réinitialisation vous a été envoyé."
            };
        }

        // Générer un code à 6 chiffres
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();

        // Supprimer les anciens codes expirés (ne pas faire échouer si ça échoue)
        try
        {
            await _passwordResetCodeRepository.DeleteExpiredCodesAsync();
        }
        catch (Exception ex)
        {
            // Logger l'erreur mais continuer - les codes expirés seront supprimés lors de la prochaine tentative
            _logger.LogWarning(ex, "Erreur lors de la suppression des codes expirés, continuation...");
        }

        // Créer un nouveau code de réinitialisation
        var resetCode = new PasswordResetCode
        {
            Email = request.Email.ToLower(),
            Code = code,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15), // Code valide 15 minutes
            IsUsed = false // S'assurer que IsUsed est explicitement défini
        };

        _logger.LogInformation("Création du code de réinitialisation pour {Email}: {Code}, IsUsed: {IsUsed}", 
            request.Email, code, resetCode.IsUsed);

        try
        {
            await _passwordResetCodeRepository.AddAsync(resetCode);
            _logger.LogInformation("Code de réinitialisation créé avec succès pour {Email}", request.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création du code de réinitialisation pour {Email}. Détails: {Message}", 
                request.Email, ex.Message);
            throw;
        }

        // Envoyer l'email avec le code de manière asynchrone (fire-and-forget)
        // Ne pas attendre la fin de l'envoi pour répondre à la requête HTTP
        // Cela évite les timeouts et améliore l'expérience utilisateur
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendPasswordResetCodeAsync(user.Email, code, user.DisplayName);
            }
            catch (Exception ex)
            {
                // Logger l'erreur mais ne pas faire échouer la requête
                // Le code est déjà sauvegardé en base, l'utilisateur peut le demander à nouveau
                _logger.LogError(ex, "Erreur lors de l'envoi de l'email de réinitialisation à {Email}", user.Email);
            }
        });

        return new ForgotPasswordResponse
        {
            Success = true,
            Message = "Si cet email existe dans notre système, un code de réinitialisation vous a été envoyé."
        };
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        _logger.LogInformation("Tentative de réinitialisation de mot de passe pour {Email}", request.Email);
        
        var resetCode = await _passwordResetCodeRepository.GetByEmailAndCodeAsync(request.Email, request.Code);
        
        if (resetCode == null)
        {
            _logger.LogWarning("Code de réinitialisation introuvable pour {Email} avec le code {Code}", 
                request.Email, request.Code);
            throw new BusinessException("Code invalide ou expiré. Veuillez demander un nouveau code.");
        }
        
        // Vérifier manuellement la validité du code
        var now = DateTime.UtcNow;
        var isExpired = resetCode.ExpiresAt <= now;
        var isUsed = resetCode.IsUsed;
        
        _logger.LogInformation("Code trouvé - ExpiresAt: {ExpiresAt}, Now: {Now}, IsExpired: {IsExpired}, IsUsed: {IsUsed}", 
            resetCode.ExpiresAt, now, isExpired, isUsed);
        
        if (isUsed)
        {
            _logger.LogWarning("Code de réinitialisation déjà utilisé pour {Email}", request.Email);
            throw new BusinessException("Ce code a déjà été utilisé. Veuillez demander un nouveau code.");
        }
        
        if (isExpired)
        {
            _logger.LogWarning("Code de réinitialisation expiré pour {Email} - ExpiresAt: {ExpiresAt}, Now: {Now}", 
                request.Email, resetCode.ExpiresAt, now);
            throw new BusinessException("Code expiré. Veuillez demander un nouveau code.");
        }

        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Utilisateur introuvable pour {Email} lors de la réinitialisation", request.Email);
            throw new BusinessException("Utilisateur introuvable.");
        }
        
        if (!user.HasPasswordAuth())
        {
            _logger.LogWarning("Tentative de réinitialisation pour un compte sans mot de passe: {Email}", request.Email);
            throw new BusinessException("Ce compte n'a pas de mot de passe configuré. Utilisez la connexion Google.");
        }

        // Mettre à jour le mot de passe
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);
        await _userRepository.UpdateAsync(user);

        // Marquer le code comme utilisé
        resetCode.IsUsed = true;
        await _passwordResetCodeRepository.UpdateAsync(resetCode);
        
        _logger.LogInformation("Mot de passe réinitialisé avec succès pour {Email}", request.Email);
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        _logger.LogInformation("Tentative de vérification d'email pour {Email}", request.Email);
        
        var verificationCode = await _emailVerificationCodeRepository.GetByEmailAndCodeAsync(request.Email, request.Code);
        
        if (verificationCode == null)
        {
            _logger.LogWarning("Code de vérification introuvable pour {Email} avec le code {Code}", 
                request.Email, request.Code);
            return new VerifyEmailResponse
            {
                Success = false,
                Message = "Code invalide ou expiré. Veuillez demander un nouveau code."
            };
        }
        
        if (!verificationCode.IsValid)
        {
            _logger.LogWarning("Code de vérification invalide ou expiré pour {Email}. IsUsed: {IsUsed}, ExpiresAt: {ExpiresAt}", 
                request.Email, verificationCode.IsUsed, verificationCode.ExpiresAt);
            return new VerifyEmailResponse
            {
                Success = false,
                Message = "Code invalide ou expiré. Veuillez demander un nouveau code."
            };
        }

        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Utilisateur introuvable pour {Email}", request.Email);
            return new VerifyEmailResponse
            {
                Success = false,
                Message = "Utilisateur introuvable."
            };
        }

        try
        {
            // Marquer l'email comme vérifié
            _logger.LogInformation("Marquage de l'email comme vérifié pour {Email}", request.Email);
            user.EmailVerified = true;
            await _userRepository.UpdateAsync(user);

            // Marquer le code comme utilisé
            _logger.LogInformation("Marquage du code comme utilisé pour {Email}", request.Email);
            verificationCode.IsUsed = true;
            await _emailVerificationCodeRepository.UpdateAsync(verificationCode);
            
            _logger.LogInformation("Email vérifié avec succès pour {Email}", request.Email);
            
            return new VerifyEmailResponse
            {
                Success = true,
                Message = "Votre adresse email a été vérifiée avec succès."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la vérification de l'email pour {Email}", request.Email);
            return new VerifyEmailResponse
            {
                Success = false,
                Message = "Une erreur est survenue lors de la vérification. Veuillez réessayer."
            };
        }
    }

    public async Task<ResendVerificationResponse> ResendVerificationEmailAsync(ResendVerificationRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        
        // Pour des raisons de sécurité, on ne révèle pas si l'email existe ou est déjà vérifié
        if (user == null)
        {
            return new ResendVerificationResponse
            {
                Success = true,
                Message = "Si cet email existe dans notre système, un code de vérification vous a été envoyé."
            };
        }

        if (user.EmailVerified)
        {
            return new ResendVerificationResponse
            {
                Success = true,
                Message = "Si cet email existe dans notre système, un code de vérification vous a été envoyé."
            };
        }

        // Supprimer les anciens codes expirés
        try
        {
            await _emailVerificationCodeRepository.DeleteExpiredCodesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erreur lors de la suppression des codes expirés");
        }

        // Générer un nouveau code
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();

        var verificationCode = new EmailVerificationCode
        {
            Email = request.Email.ToLower(),
            Code = code,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        try
        {
            await _emailVerificationCodeRepository.AddAsync(verificationCode);
            _logger.LogInformation("Code de vérification créé pour {Email}: {Code}", request.Email, code);
        }
        catch (ArgumentException argEx)
        {
            _logger.LogError(argEx, "Erreur de validation lors de la création du code de vérification pour {Email}. Détails: {Message}", 
                request.Email, argEx.Message);
            // Ne pas faire échouer la requête - retourner un succès pour des raisons de sécurité
            return new ResendVerificationResponse
            {
                Success = true,
                Message = "Si cet email existe dans notre système, un code de vérification vous a été envoyé."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création du code de vérification pour {Email}. Type: {Type}, Détails: {Message}", 
                request.Email, ex.GetType().Name, ex.Message);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "Exception interne: {Message}", ex.InnerException.Message);
            }
            // Ne pas faire échouer la requête - retourner un succès pour des raisons de sécurité
            return new ResendVerificationResponse
            {
                Success = true,
                Message = "Si cet email existe dans notre système, un code de vérification vous a été envoyé."
            };
        }

        // Envoyer l'email de manière asynchrone (fire-and-forget)
        // Mais avec un meilleur logging pour diagnostiquer les problèmes
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("📤 Début de l'envoi de l'email de vérification à {Email}...", user.Email);
                await _emailService.SendEmailVerificationCodeAsync(user.Email, code, user.DisplayName);
                _logger.LogInformation("✅ Email de vérification envoyé avec succès à {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erreur lors de l'envoi de l'email de vérification à {Email}. Détails: {Message}", 
                    user.Email, ex.Message);
                _logger.LogError(ex, "Stack trace complète: {StackTrace}", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Exception interne: {Message}", ex.InnerException.Message);
                }
            }
        });

        return new ResendVerificationResponse
        {
            Success = true,
            Message = "Si cet email existe dans notre système, un code de vérification vous a été envoyé."
        };
    }

    /// <summary>
    /// Corrige automatiquement les problèmes d'encodage UTF-8 courants
    /// </summary>
    private static string FixUtf8Encoding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Utiliser les codes Unicode échappés pour éviter les problèmes d'encodage dans le fichier C#
        var result = text;
        
        // Corrections courantes (Windows-1252 mal interprété comme UTF-8)
        result = result.Replace("\u00C3\u00A9", "\u00E9");  // é
        result = result.Replace("\u00C3\u00A8", "\u00E8");  // è
        result = result.Replace("\u00C3\u00AA", "\u00EA");  // ê
        result = result.Replace("\u00C3\u00AB", "\u00EB");  // ë
        result = result.Replace("\u00C3\u00A0", "\u00E0");  // à
        result = result.Replace("\u00C3\u00A2", "\u00E2");  // â
        result = result.Replace("\u00C3\u00B4", "\u00F4");  // ô
        result = result.Replace("\u00C3\u00AE", "\u00EE");  // î
        result = result.Replace("\u00C3\u00AF", "\u00EF");  // ï
        result = result.Replace("\u00C3\u00B9", "\u00F9");  // ù
        result = result.Replace("\u00C3\u00BB", "\u00FB");  // û
        result = result.Replace("\u00C3\u00A7", "\u00E7");  // ç
        result = result.Replace("\u00C3\u0089", "\u00C9");  // É
        result = result.Replace("\u00C3\u0088", "\u00C8");  // È
        result = result.Replace("\u00C3\u008A", "\u00CA");  // Ê
        result = result.Replace("\u00C3\u0080", "\u00C0");  // À
        result = result.Replace("\u00C3\u0082", "\u00C2");  // Â
        result = result.Replace("\u00C3\u0094", "\u00D4");  // Ô
        result = result.Replace("\u00C3\u0087", "\u00C7");  // Ç

        return result;
    }

    private AuthResponse GenerateAuthResponse(User user, bool isFirstLogin = false)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!);
        var expiresAt = DateTime.UtcNow.AddHours(24);

        // Utiliser DisplayName ou extraire de l'email si vide
        var rawDisplayName = !string.IsNullOrWhiteSpace(user.DisplayName) 
            ? user.DisplayName 
            : user.Email.Split('@')[0];
        
        // Corriger automatiquement les problèmes d'encodage
        var displayName = FixUtf8Encoding(rawDisplayName);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, displayName),
                new Claim("displayName", displayName), // Claim custom pour faciliter l'accès frontend
                new Claim("userId", user.Id) // ID utilisateur accessible facilement
            }),
            Expires = expiresAt,
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return new AuthResponse
        {
            Token = tokenHandler.WriteToken(token),
            UserId = user.Id,
            Email = user.Email,
            DisplayName = displayName,
            ExpiresAt = expiresAt,
            IsFirstLogin = isFirstLogin
        };
    }
}

