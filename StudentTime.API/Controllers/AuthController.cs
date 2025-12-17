using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StudentTime.Core.DTOs.Auth;
using StudentTime.Core.Exceptions;
using StudentTime.Core.Interfaces;

namespace StudentTime.API.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IEmailService _emailService;

    public AuthController(IAuthService authService, ILogger<AuthController> logger, IEmailService emailService)
    {
        _authService = authService;
        _logger = logger;
        _emailService = emailService;
    }

    /// <summary>
    /// Méthode helper pour définir le cookie d'authentification HttpOnly + Secure
    /// </summary>
    private void SetAuthCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,                                    // Pas accessible via JavaScript (protection XSS)
            Secure = true,                                       // Seulement sur HTTPS
            SameSite = SameSiteMode.None,                       // Permet les requêtes cross-origin avec credentials
            Expires = DateTimeOffset.UtcNow.AddDays(7),        // Durée de vie: 7 jours
            Path = "/"                                          // Disponible sur tous les chemins
        };
        
        Response.Cookies.Append("accessToken", token, cookieOptions);
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();
            return BadRequest(new { message = "Données invalides", errors });
        }

        try
        {
            var response = await _authService.RegisterAsync(request);
            
            // Définir le token dans un cookie HttpOnly + Secure
            SetAuthCookie(response.Token);
            
            // Pour ancien iOS Safari : retourner aussi le token dans le body si demandé
            var useTokenResponse = Request.Headers["X-Use-Token-Response"].FirstOrDefault() == "true";
            
            return CreatedAtAction(nameof(Register), new { id = response.UserId }, new
            {
                userId = response.UserId,
                email = response.Email,
                displayName = response.DisplayName,
                token = useTokenResponse ? response.Token : null
            });
        }
        catch (BusinessException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();
            return BadRequest(new { message = "Données invalides", errors });
        }

        try
        {
            var response = await _authService.LoginAsync(request);
            
            // Définir le token dans un cookie HttpOnly + Secure
            SetAuthCookie(response.Token);
            
            // Pour ancien iOS Safari : retourner aussi le token dans le body si demandé
            var useTokenResponse = Request.Headers["X-Use-Token-Response"].FirstOrDefault() == "true";
            _logger.LogInformation("Login - X-Use-Token-Response: {UseTokenResponse}", useTokenResponse);
            
            return Ok(new
            {
                userId = response.UserId,
                email = response.Email,
                displayName = response.DisplayName,
                token = useTokenResponse ? response.Token : null
            });
        }
        catch (BusinessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("google")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuthRequest request)
    {
        // Le header COOP est déjà défini par le middleware global
        // Pas besoin de le redéfinir ici
        
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();
            return BadRequest(new { message = "Données invalides", errors });
        }

        try
        {
            var response = await _authService.GoogleAuthAsync(request);
            
            // Définir le token dans un cookie HttpOnly + Secure
            SetAuthCookie(response.Token);
            
            // Pour ancien iOS Safari : retourner aussi le token dans le body si demandé
            var useTokenResponse = Request.Headers["X-Use-Token-Response"].FirstOrDefault() == "true";
            
            return Ok(new
            {
                userId = response.UserId,
                email = response.Email,
                displayName = response.DisplayName,
                token = useTokenResponse ? response.Token : null
            });
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning("Erreur Business lors de l'authentification Google: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inattendue lors de l'authentification Google");
            return BadRequest(new { message = "Erreur lors de l'authentification Google. Veuillez réessayer." });
        }
    }

    [Authorize]
    [HttpPost("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult ValidateToken()
    {
        return Ok(new { valid = true });
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        // Récupérer les claims du token JWT depuis le cookie
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                    ?? User.FindFirst("sub")?.Value
                    ?? User.FindFirst("userId")?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value 
                   ?? User.FindFirst("email")?.Value;
        var displayName = User.FindFirst("displayName")?.Value 
                         ?? User.FindFirst("display_name")?.Value
                         ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        return Ok(new
        {
            userId,
            email,
            displayName,
            isAuthenticated = true
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        // Supprimer le cookie d'authentification
        Response.Cookies.Delete("accessToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/"
        });
        
        return Ok(new { message = "Déconnexion réussie" });
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();
            return BadRequest(new { message = "Données invalides", errors });
        }

        try
        {
            var response = await _authService.ForgotPasswordAsync(request);
            return Ok(response);
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Logger toutes les autres exceptions pour diagnostic
            _logger.LogError(ex, "Erreur inattendue lors de la demande de réinitialisation de mot de passe");
            // Retourner une erreur générique pour la sécurité
            return StatusCode(500, new { message = "Une erreur est survenue. Veuillez réessayer plus tard." });
        }
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();
            return BadRequest(new { message = "Données invalides", errors });
        }

        try
        {
            await _authService.ResetPasswordAsync(request);
            return Ok(new { message = "Mot de passe réinitialisé avec succès" });
        }
        catch (BusinessException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inattendue lors de la réinitialisation de mot de passe");
            return StatusCode(500, new { message = "Une erreur est survenue. Veuillez réessayer plus tard." });
        }
    }

    /// <summary>
    /// Endpoint de diagnostic pour vérifier la configuration email/SendGrid
    /// Utile pour déboguer les problèmes d'envoi d'email
    /// </summary>
    [HttpGet("email-config-check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CheckEmailConfiguration()
    {
        try
        {
            var validation = _emailService.ValidateConfiguration();
            
            // Ajouter des informations supplémentaires sur les variables d'environnement
            var envVars = new Dictionary<string, string>
            {
                ["Email__SendGridApiKey"] = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Email__SendGridApiKey")) ? "CONFIGURÉ" : "NON CONFIGURÉ",
                ["Email__SmtpHost"] = Environment.GetEnvironmentVariable("Email__SmtpHost") ?? "NON CONFIGURÉ",
                ["Email__SmtpPort"] = Environment.GetEnvironmentVariable("Email__SmtpPort") ?? "NON CONFIGURÉ",
                ["Email__SmtpUsername"] = Environment.GetEnvironmentVariable("Email__SmtpUsername") ?? "NON CONFIGURÉ",
                ["Email__SmtpPassword"] = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Email__SmtpPassword")) ? "CONFIGURÉ" : "NON CONFIGURÉ",
                ["Email__FromEmail"] = Environment.GetEnvironmentVariable("Email__FromEmail") ?? "NON CONFIGURÉ",
                ["Email__FromName"] = Environment.GetEnvironmentVariable("Email__FromName") ?? "NON CONFIGURÉ"
            };
            
            return Ok(new
            {
                isValid = validation.IsValid,
                errors = validation.Errors,
                warnings = validation.Warnings,
                configuration = validation.Configuration,
                environmentVariables = envVars,
                note = "Dans Railway, utilisez des double underscores (__) pour les clés avec des deux-points. Ex: Email__SmtpHost au lieu de Email:SmtpHost"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la validation de la configuration email");
            return StatusCode(500, new { message = "Erreur lors de la validation", error = ex.Message });
        }
    }

    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(VerifyEmailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();
            return BadRequest(new { message = "Données invalides", errors });
        }

        try
        {
            var response = await _authService.VerifyEmailAsync(request);
            if (response.Success)
            {
                return Ok(response);
            }
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inattendue lors de la vérification d'email");
            return StatusCode(500, new { message = "Une erreur est survenue. Veuillez réessayer plus tard." });
        }
    }

    [HttpPost("resend-verification")]
    [ProducesResponseType(typeof(ResendVerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => e.ErrorMessage))
                .ToList();
            _logger.LogWarning("Validation échouée pour ResendVerification - Email: {Email}, Erreurs: {Errors}", 
                request?.Email ?? "NULL", string.Join(", ", errors));
            return BadRequest(new { message = "Données invalides", errors });
        }

        try
        {
            _logger.LogInformation("Demande de renvoi de code de vérification pour {Email}", request.Email);
            var response = await _authService.ResendVerificationEmailAsync(request);
            _logger.LogInformation("Réponse ResendVerification pour {Email}: Success={Success}", request.Email, response.Success);
            return Ok(response);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning("Erreur Business lors de la demande de renvoi de code pour {Email}: {Message}", 
                request.Email, ex.Message);
            // Retourner un succès pour des raisons de sécurité (ne pas révéler si l'email existe)
            return Ok(new ResendVerificationResponse
            {
                Success = true,
                Message = "Si cet email existe dans notre système, un code de vérification vous a été envoyé."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inattendue lors de la demande de renvoi de code pour {Email}. Type: {Type}, Message: {Message}", 
                request.Email, ex.GetType().Name, ex.Message);
            _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "Exception interne: {Message}", ex.InnerException.Message);
            }
            // Retourner un succès pour des raisons de sécurité (ne pas révéler si l'email existe)
            return Ok(new ResendVerificationResponse
            {
                Success = true,
                Message = "Si cet email existe dans notre système, un code de vérification vous a été envoyé."
            });
        }
    }

    /// <summary>
    /// Endpoint de diagnostic pour vérifier l'état des migrations et des utilisateurs
    /// Utile pour diagnostiquer les problèmes de migrations
    /// </summary>
    [HttpGet("diagnostics/migrations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetMigrationDiagnostics()
    {
        try
        {
            // Cette méthode nécessite l'injection de AppDbContext
            // Pour l'instant, on retourne juste un message informatif
            return Ok(new
            {
                message = "Vérifiez les logs de l'application au démarrage pour voir l'état des migrations",
                note = "Les migrations sont appliquées automatiquement au démarrage de l'application"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des diagnostics");
            return StatusCode(500, new { message = "Erreur lors de la récupération des diagnostics" });
        }
    }
}

