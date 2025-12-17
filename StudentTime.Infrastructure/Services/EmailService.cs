using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudentTime.Core.Interfaces;

namespace StudentTime.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Récupère une valeur de configuration depuis la configuration ou les variables d'environnement
    /// Les variables d'environnement ont la priorité et utilisent __ (double underscore) au lieu de :
    /// </summary>
    private string? GetConfigValue(string key)
    {
        // Essayer d'abord les variables d'environnement (priorité)
        var envKey = key.Replace(":", "__");
        var envValue = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrEmpty(envValue))
        {
            return envValue;
        }
        
        // Sinon, utiliser la configuration normale
        return _configuration[key];
    }

    public async Task SendPasswordResetCodeAsync(string email, string code, string displayName)
    {
        var sendGridApiKey = GetConfigValue("Email:SendGridApiKey");
        var fromEmail = GetConfigValue("Email:FromEmail") ?? "noreply@studenttime.com";
        var fromName = GetConfigValue("Email:FromName") ?? "StudentTime";

        // PRIORITÉ 1 : Si SendGrid API Key est configurée, utiliser l'API REST (plus fiable)
        if (!string.IsNullOrEmpty(sendGridApiKey))
        {
            try
            {
                _logger.LogInformation("📤 Tentative d'envoi via SendGrid API REST pour {Email}...", email);
                await SendViaSendGridApiAsync(email, code, displayName, sendGridApiKey, fromEmail, fromName);
                return; // Succès, on sort
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Erreur lors de l'envoi via SendGrid API, tentative SMTP en fallback...");
                // Continue vers SMTP comme fallback
            }
        }

        // PRIORITÉ 2 : Fallback vers SMTP (code existant)
        var smtpHost = GetConfigValue("Email:SmtpHost");
        var smtpPortStr = GetConfigValue("Email:SmtpPort");
        var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;
        var smtpUsername = GetConfigValue("Email:SmtpUsername");
        var smtpPassword = GetConfigValue("Email:SmtpPassword");

        // Log de diagnostic de la configuration
        _logger.LogInformation(
            "Configuration SMTP - Host: {Host}, Port: {Port}, Username: {Username}, FromEmail: {FromEmail}, Password configurée: {HasPassword}",
            smtpHost ?? "NON CONFIGURÉ",
            smtpPort,
            smtpUsername ?? "NON CONFIGURÉ",
            fromEmail,
            !string.IsNullOrEmpty(smtpPassword));

        // En développement, si SMTP n'est pas configuré, afficher dans les logs
        if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername))
        {
            // Afficher le code de manière très visible dans la console
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("📧 [DEV MODE] CODE DE RÉINITIALISATION");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Code: {code}");
            Console.WriteLine(new string('=', 60) + "\n");
            
            _logger.LogWarning(
                "📧 [DEV MODE] Code de réinitialisation pour {Email}: {Code}",
                email, code);
            _logger.LogWarning(
                "Pour activer l'envoi d'email, configurez Email:SmtpHost, Email:SmtpUsername, Email:SmtpPassword dans appsettings.json");
            return;
        }

        try
        {
            // Configuration SSL selon le port
            // Port 465 : SSL direct (connexion chiffrée dès le début)
            // Port 587 : STARTTLS (connexion non chiffrée puis upgrade vers TLS)
            if (smtpPort == 465)
            {
                // Pour le port 465, forcer TLS 1.2 ou supérieur
                System.Net.ServicePointManager.SecurityProtocol = 
                    System.Net.SecurityProtocolType.Tls12 | 
                    System.Net.SecurityProtocolType.Tls13;
                
                _logger.LogInformation("Configuration SSL direct pour le port 465 (SSL dès la connexion)");
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true, // SendGrid nécessite SSL/TLS
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000 // 15 secondes pour la connexion SMTP (SendGrid répond généralement en 2-5 secondes)
            };

            _logger.LogInformation("🔌 Tentative de connexion SMTP à {Host}:{Port} (SSL: {Ssl}) pour {Email}", 
                smtpHost, smtpPort, client.EnableSsl, email);

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "Réinitialisation de votre mot de passe - StudentTime",
                Body = GeneratePasswordResetEmailBody(code, displayName),
                IsBodyHtml = true
            };

            message.To.Add(email);

            _logger.LogInformation("📧 Message créé - De: {FromEmail}, À: {ToEmail}, Sujet: {Subject}", 
                fromEmail, email, message.Subject);
            _logger.LogInformation("📤 Envoi du message via SMTP...");

            // Utiliser SendMailAsync avec un CancellationToken de 15 secondes
            // SendGrid devrait répondre en quelques secondes, 15 secondes est largement suffisant
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await client.SendMailAsync(message, cts.Token);
            
            _logger.LogInformation("✅ Email de réinitialisation envoyé avec succès à {Email}", email);
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, 
                "❌ Erreur SMTP lors de l'envoi à {Email}. StatusCode: {StatusCode}, Message: {Message}", 
                email, smtpEx.StatusCode, smtpEx.Message);
            
            // Log détaillé pour diagnostic SendGrid
            _logger.LogError("Configuration utilisée - Host: {Host}, Port: {Port}, Username: {Username}", 
                smtpHost, smtpPort, smtpUsername);
            
            // En cas d'erreur, afficher le code dans les logs pour le développement
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("⚠️ ERREUR ENVOI EMAIL - CODE DE RÉINITIALISATION");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Code: {code}");
            Console.WriteLine($"Erreur: {smtpEx.Message}");
            Console.WriteLine($"StatusCode: {smtpEx.StatusCode}");
            if (smtpEx.InnerException != null)
            {
                Console.WriteLine($"Erreur interne: {smtpEx.InnerException.Message}");
            }
            Console.WriteLine($"Host: {smtpHost}, Port: {smtpPort}, Username: {smtpUsername}");
            Console.WriteLine(new string('=', 60) + "\n");
            
            _logger.LogWarning("Code de réinitialisation (fallback): {Code}", code);
            // NE PAS THROW - laisser le code être sauvegardé en base
            // L'utilisateur pourra demander un nouveau code si nécessaire
            // L'erreur est loggée pour diagnostic
        }
        catch (TaskCanceledException cancelEx)
        {
            _logger.LogError(cancelEx, 
                "⏱️ Timeout lors de l'envoi d'email à {Email}. La connexion SMTP a pris trop de temps (>15s).", email);
            
            // Log détaillé pour diagnostic
            _logger.LogError("Configuration utilisée - Host: {Host}, Port: {Port}, Username: {Username}, Password configurée: {HasPassword}", 
                smtpHost, smtpPort, smtpUsername, !string.IsNullOrEmpty(smtpPassword));
            _logger.LogError("Vérifications SendGrid:");
            _logger.LogError("  - Host doit être 'smtp.sendgrid.net'");
            _logger.LogError("  - Port doit être 587 (STARTTLS) ou 465 (SSL direct)");
            _logger.LogError("  - Username doit être 'apikey' (littéralement)");
            _logger.LogError("  - Password doit être votre clé API SendGrid complète (commence par SG.)");
            _logger.LogError("  - Si le port 587 ne fonctionne pas, essayez 465 (SSL direct)");
            
            // En cas d'erreur, afficher le code dans les logs pour le développement
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("⚠️ TIMEOUT ENVOI EMAIL - CODE DE RÉINITIALISATION");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Code: {code}");
            Console.WriteLine($"Erreur: Timeout - La connexion SMTP a pris trop de temps (>15s)");
            Console.WriteLine($"Configuration actuelle:");
            Console.WriteLine($"  Host: {smtpHost}");
            Console.WriteLine($"  Port: {smtpPort}");
            Console.WriteLine($"  Username: {smtpUsername}");
            Console.WriteLine($"  Password configurée: {(!string.IsNullOrEmpty(smtpPassword) ? "OUI" : "NON")}");
            Console.WriteLine($"Vérifications SendGrid:");
            Console.WriteLine($"  ✓ Host doit être 'smtp.sendgrid.net'");
            Console.WriteLine($"  ✓ Port doit être 587 (STARTTLS) ou 465 (SSL direct)");
            Console.WriteLine($"  ✓ Username doit être 'apikey' (littéralement)");
            Console.WriteLine($"  ✓ Password doit être votre clé API SendGrid complète (commence par SG.)");
            Console.WriteLine($"💡 Astuce: Si le port 587 ne fonctionne pas, essayez 465 (SSL direct)");
            Console.WriteLine(new string('=', 60) + "\n");
            
            _logger.LogWarning("Code de réinitialisation (fallback): {Code}", code);
            // NE PAS THROW - laisser le code être sauvegardé en base
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erreur inattendue lors de l'envoi de l'email à {Email}. Type: {Type}, Message: {Message}", 
                email, ex.GetType().Name, ex.Message);
            
            // Log détaillé pour diagnostic
            _logger.LogError("Configuration utilisée - Host: {Host}, Port: {Port}, Username: {Username}", 
                smtpHost, smtpPort, smtpUsername);
            
            // En cas d'erreur, afficher le code dans les logs pour le développement
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("⚠️ ERREUR ENVOI EMAIL - CODE DE RÉINITIALISATION");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Code: {code}");
            Console.WriteLine($"Type d'erreur: {ex.GetType().Name}");
            Console.WriteLine($"Erreur: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Erreur interne: {ex.InnerException.Message}");
            }
            Console.WriteLine($"Host: {smtpHost}, Port: {smtpPort}, Username: {smtpUsername}");
            Console.WriteLine(new string('=', 60) + "\n");
            
            _logger.LogWarning("Code de réinitialisation (fallback): {Code}", code);
            // NE PAS THROW - laisser le code être sauvegardé en base
            // L'utilisateur pourra demander un nouveau code si nécessaire
            // L'erreur est loggée pour diagnostic
        }
    }

    public EmailConfigurationValidationResult ValidateConfiguration()
    {
        var result = new EmailConfigurationValidationResult();
        
        var smtpHost = GetConfigValue("Email:SmtpHost");
        var smtpPortStr = GetConfigValue("Email:SmtpPort");
        var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;
        var smtpUsername = GetConfigValue("Email:SmtpUsername");
        var smtpPassword = GetConfigValue("Email:SmtpPassword");
        var fromEmail = GetConfigValue("Email:FromEmail") ?? "noreply@studenttime.com";
        var fromName = GetConfigValue("Email:FromName") ?? "StudentTime";

        // Stocker la configuration (masquer le mot de passe)
        result.Configuration["SmtpHost"] = smtpHost ?? "NON CONFIGURÉ";
        result.Configuration["SmtpPort"] = smtpPort.ToString();
        result.Configuration["SmtpUsername"] = smtpUsername ?? "NON CONFIGURÉ";
        result.Configuration["SmtpPassword"] = string.IsNullOrEmpty(smtpPassword) ? "NON CONFIGURÉ" : "***CONFIGURÉ***";
        result.Configuration["FromEmail"] = fromEmail;
        result.Configuration["FromName"] = fromName;

        // Vérifications pour SendGrid
        if (string.IsNullOrEmpty(smtpHost))
        {
            result.Errors.Add("Email:SmtpHost n'est pas configuré");
        }
        else if (!smtpHost.Equals("smtp.sendgrid.net", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"Email:SmtpHost est '{smtpHost}' mais devrait être 'smtp.sendgrid.net' pour SendGrid");
        }

        if (string.IsNullOrEmpty(smtpUsername))
        {
            result.Errors.Add("Email:SmtpUsername n'est pas configuré");
        }
        else if (!smtpUsername.Equals("apikey", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"Email:SmtpUsername est '{smtpUsername}' mais devrait être 'apikey' (littéralement) pour SendGrid");
        }

        if (string.IsNullOrEmpty(smtpPassword))
        {
            result.Errors.Add("Email:SmtpPassword n'est pas configuré");
        }
        else if (!smtpPassword.StartsWith("SG.", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add("Email:SmtpPassword ne commence pas par 'SG.' - vérifiez que c'est bien votre clé API SendGrid complète");
        }

        if (smtpPort != 587 && smtpPort != 465)
        {
            result.Warnings.Add($"Email:SmtpPort est {smtpPort} mais SendGrid recommande 587 (TLS) ou 465 (SSL direct)");
        }

        if (string.IsNullOrEmpty(fromEmail) || !fromEmail.Contains("@"))
        {
            result.Warnings.Add($"Email:FromEmail '{fromEmail}' ne semble pas être une adresse email valide");
        }

        result.IsValid = result.Errors.Count == 0;
        
        return result;
    }

    private async Task SendViaSendGridApiAsync(string email, string code, string displayName, 
        string apiKey, string fromEmail, string fromName)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            httpClient.Timeout = TimeSpan.FromSeconds(10); // Plus rapide que SMTP

            var payload = new
            {
                personalizations = new[]
                {
                    new
                    {
                        to = new[] { new { email = email } },
                        subject = "Réinitialisation de votre mot de passe - StudentTime"
                    }
                },
                from = new { email = fromEmail, name = fromName },
                content = new[]
                {
                    new
                    {
                        type = "text/html",
                        value = GeneratePasswordResetEmailBody(code, displayName)
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("📤 Envoi d'email via SendGrid API REST pour {Email}...", email);

            var response = await httpClient.PostAsync(
                "https://api.sendgrid.com/v3/mail/send", 
                content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Email envoyé avec succès via SendGrid API à {Email}", email);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ Erreur SendGrid API: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                throw new Exception($"SendGrid API error: {response.StatusCode} - {errorContent}");
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("⏱️ Timeout lors de l'envoi via SendGrid API");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erreur lors de l'envoi via SendGrid API");
            throw;
        }
    }

    public async Task SendEmailVerificationCodeAsync(string email, string code, string displayName)
    {
        // Log de diagnostic de la configuration
        var sendGridApiKey = GetConfigValue("Email:SendGridApiKey");
        var smtpHost = GetConfigValue("Email:SmtpHost");
        var smtpUsername = GetConfigValue("Email:SmtpUsername");
        var smtpPassword = GetConfigValue("Email:SmtpPassword");
        var fromEmail = GetConfigValue("Email:FromEmail") ?? "noreply@studenttime.com";
        var fromName = GetConfigValue("Email:FromName") ?? "StudentTime";

        _logger.LogInformation(
            "📧 Configuration email pour vérification - SendGridApiKey: {HasApiKey}, SmtpHost: {Host}, SmtpUsername: {Username}, Password: {HasPassword}, FromEmail: {FromEmail}",
            !string.IsNullOrEmpty(sendGridApiKey) ? "CONFIGURÉ" : "NON CONFIGURÉ",
            smtpHost ?? "NON CONFIGURÉ",
            smtpUsername ?? "NON CONFIGURÉ",
            !string.IsNullOrEmpty(smtpPassword) ? "CONFIGURÉ" : "NON CONFIGURÉ",
            fromEmail);

        // PRIORITÉ 1 : Si SendGrid API Key est configurée, utiliser l'API REST
        if (!string.IsNullOrEmpty(sendGridApiKey))
        {
            try
            {
                _logger.LogInformation("📤 Tentative d'envoi d'email de vérification via SendGrid API REST pour {Email}...", email);
                await SendVerificationEmailViaSendGridApiAsync(email, code, displayName, sendGridApiKey, fromEmail, fromName);
                _logger.LogInformation("✅ Email de vérification envoyé avec succès via SendGrid API à {Email}", email);
                return; // Succès, on sort
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erreur lors de l'envoi via SendGrid API pour {Email}. Détails: {Message}", email, ex.Message);
                _logger.LogWarning("⚠️ Tentative de fallback vers SMTP...");
                // Continue vers SMTP comme fallback
            }
        }
        else
        {
            _logger.LogInformation("📤 SendGrid API Key non configurée, utilisation de SMTP pour {Email}...", email);
        }

        // PRIORITÉ 2 : Fallback vers SMTP ou mode dev
        if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername))
        {
            var errorMsg = $"Configuration email manquante - SendGridApiKey: {(string.IsNullOrEmpty(sendGridApiKey) ? "NON CONFIGURÉ" : "CONFIGURÉ MAIS ÉCHOUÉ")}, SmtpHost: {(smtpHost ?? "NULL")}, SmtpUsername: {(smtpUsername ?? "NULL")}";
            _logger.LogError("❌ {ErrorMsg}", errorMsg);
            _logger.LogError("💡 Pour activer l'envoi d'email, configurez AU MOINS UNE des options suivantes dans Railway:");
            _logger.LogError("   Option 1 (Recommandé) - SendGrid API REST:");
            _logger.LogError("     - Email__SendGridApiKey = VOTRE_CLE_API_SENDGRID");
            _logger.LogError("   Option 2 - SendGrid SMTP:");
            _logger.LogError("     - Email__SmtpHost = smtp.sendgrid.net");
            _logger.LogError("     - Email__SmtpPort = 587");
            _logger.LogError("     - Email__SmtpUsername = apikey");
            _logger.LogError("     - Email__SmtpPassword = VOTRE_CLE_API_SENDGRID");
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("📧 [DEV MODE] CODE DE VÉRIFICATION EMAIL");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Code: {code}");
            Console.WriteLine($"⚠️ Configuration email manquante - l'email n'a pas été envoyé");
            Console.WriteLine($"💡 Configurez Email__SendGridApiKey dans Railway pour activer l'envoi d'email");
            Console.WriteLine(new string('=', 60) + "\n");
            
            _logger.LogWarning("📧 [DEV MODE] Code de vérification pour {Email}: {Code}", email, code);
            // Ne pas throw - juste logger l'erreur pour ne pas faire échouer la requête HTTP
            // Le code est sauvegardé en base, l'utilisateur peut le demander à nouveau
            return;
        }

        // SMTP fallback (similaire à SendPasswordResetCodeAsync mais avec template de vérification)
        var smtpPortStr = GetConfigValue("Email:SmtpPort");
        var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 587;
        
        try
        {

            if (smtpPort == 465)
            {
                System.Net.ServicePointManager.SecurityProtocol = 
                    System.Net.SecurityProtocolType.Tls12 | 
                    System.Net.SecurityProtocolType.Tls13;
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "Vérifiez votre adresse email - StudentTime",
                Body = GenerateEmailVerificationBody(code, displayName),
                IsBodyHtml = true
            };

            message.To.Add(email);

            _logger.LogInformation("🔌 Tentative de connexion SMTP à {Host}:{Port} (SSL: {Ssl}) pour {Email}", 
                smtpHost, smtpPort, true, email);
            _logger.LogInformation("📧 Message créé - De: {FromEmail}, À: {ToEmail}, Sujet: {Subject}", 
                fromEmail, email, message.Subject);
            _logger.LogInformation("📤 Envoi du message via SMTP...");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await client.SendMailAsync(message, cts.Token);
            
            _logger.LogInformation("✅ Email de vérification envoyé avec succès via SMTP à {Email}", email);
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, 
                "❌ Erreur SMTP lors de l'envoi de l'email de vérification à {Email}. StatusCode: {StatusCode}, Message: {Message}", 
                email, smtpEx.StatusCode, smtpEx.Message);
            _logger.LogError("Configuration utilisée - Host: {Host}, Port: {Port}, Username: {Username}", 
                smtpHost, smtpPort, smtpUsername);
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("⚠️ ERREUR SMTP ENVOI EMAIL DE VÉRIFICATION");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Code: {code}");
            Console.WriteLine($"Erreur: {smtpEx.Message}");
            Console.WriteLine($"StatusCode: {smtpEx.StatusCode}");
            Console.WriteLine($"Host: {smtpHost}, Port: {smtpPort}, Username: {smtpUsername}");
            Console.WriteLine(new string('=', 60) + "\n");
            
            // Ne pas throw - juste logger l'erreur pour ne pas faire échouer la requête HTTP
            // Le code est sauvegardé en base, l'utilisateur peut le demander à nouveau
        }
        catch (TaskCanceledException cancelEx)
        {
            _logger.LogError(cancelEx, 
                "⏱️ Timeout lors de l'envoi de l'email de vérification à {Email}. La connexion SMTP a pris trop de temps (>15s).", email);
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("⚠️ TIMEOUT ENVOI EMAIL DE VÉRIFICATION");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Code: {code}");
            Console.WriteLine($"Erreur: Timeout - La connexion SMTP a pris trop de temps (>15s)");
            Console.WriteLine(new string('=', 60) + "\n");
            
            // Ne pas throw - juste logger l'erreur
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erreur inattendue lors de l'envoi de l'email de vérification à {Email}. Type: {Type}, Message: {Message}", 
                email, ex.GetType().Name, ex.Message);
            
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("⚠️ ERREUR ENVOI EMAIL DE VÉRIFICATION");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Email: {email}");
            Console.WriteLine($"Code: {code}");
            Console.WriteLine($"Type d'erreur: {ex.GetType().Name}");
            Console.WriteLine($"Erreur: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Erreur interne: {ex.InnerException.Message}");
            }
            Console.WriteLine(new string('=', 60) + "\n");
            
            // Ne pas throw - juste logger l'erreur
        }
    }

    private async Task SendVerificationEmailViaSendGridApiAsync(string email, string code, string displayName, 
        string apiKey, string fromEmail, string fromName)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var payload = new
            {
                personalizations = new[]
                {
                    new
                    {
                        to = new[] { new { email = email } },
                        subject = "Vérifiez votre adresse email - StudentTime"
                    }
                },
                from = new { email = fromEmail, name = fromName },
                content = new[]
                {
                    new
                    {
                        type = "text/html",
                        value = GenerateEmailVerificationBody(code, displayName)
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("📤 Envoi de la requête à SendGrid API pour {Email}...", email);

            var response = await httpClient.PostAsync(
                "https://api.sendgrid.com/v3/mail/send", 
                content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Email de vérification envoyé avec succès via SendGrid API à {Email}", email);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ Erreur SendGrid API: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                _logger.LogError("💡 Vérifiez que:");
                _logger.LogError("   - La clé API SendGrid est valide et active");
                _logger.LogError("   - L'adresse email 'From' ({FromEmail}) est vérifiée dans SendGrid", fromEmail);
                _logger.LogError("   - La clé API a les permissions d'envoi d'email");
                throw new Exception($"SendGrid API error: {response.StatusCode} - {errorContent}");
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("⏱️ Timeout lors de l'envoi via SendGrid API pour {Email}", email);
            throw;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "❌ Erreur de connexion à SendGrid API pour {Email}: {Message}", email, httpEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erreur inattendue lors de l'envoi via SendGrid API pour {Email}: {Message}", email, ex.Message);
            throw;
        }
    }

    private string GenerateEmailVerificationBody(string code, string displayName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0d6efd; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 5px 5px; }}
        .code-box {{ background-color: white; border: 2px solid #0d6efd; border-radius: 5px; padding: 20px; text-align: center; margin: 20px 0; }}
        .code {{ font-size: 32px; font-weight: bold; color: #0d6efd; letter-spacing: 5px; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>StudentTime</h1>
        </div>
        <div class=""content"">
            <h2>Vérifiez votre adresse email</h2>
            <p>Bonjour {(string.IsNullOrEmpty(displayName) ? "" : displayName)},</p>
            <p>Merci de vous être inscrit sur StudentTime ! Pour activer votre compte, veuillez vérifier votre adresse email en utilisant le code suivant :</p>
            <div class=""code-box"">
                <div class=""code"">{code}</div>
            </div>
            <p>Ce code est valide pendant <strong>24 heures</strong>.</p>
            <p>Si vous n'avez pas créé de compte, ignorez cet email.</p>
            <div class=""footer"">
                <p>Cet email a été envoyé automatiquement, merci de ne pas y répondre.</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }

    private string GeneratePasswordResetEmailBody(string code, string displayName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #0d6efd; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 5px 5px; }}
        .code-box {{ background-color: white; border: 2px solid #0d6efd; border-radius: 5px; padding: 20px; text-align: center; margin: 20px 0; }}
        .code {{ font-size: 32px; font-weight: bold; color: #0d6efd; letter-spacing: 5px; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>StudentTime</h1>
        </div>
        <div class=""content"">
            <h2>Réinitialisation de votre mot de passe</h2>
            <p>Bonjour {(string.IsNullOrEmpty(displayName) ? "" : displayName)},</p>
            <p>Vous avez demandé à réinitialiser votre mot de passe. Utilisez le code suivant :</p>
            <div class=""code-box"">
                <div class=""code"">{code}</div>
            </div>
            <p>Ce code est valide pendant <strong>15 minutes</strong>.</p>
            <p>Si vous n'avez pas demandé cette réinitialisation, ignorez cet email.</p>
            <div class=""footer"">
                <p>Cet email a été envoyé automatiquement, merci de ne pas y répondre.</p>
            </div>
        </div>
    </div>
</body>
</html>";
    }
}

