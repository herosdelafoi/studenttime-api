    using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StudentTime.API.Middleware;
using StudentTime.Core.Interfaces;
using StudentTime.Core.Services;
using StudentTime.Infrastructure.Data;
using StudentTime.Infrastructure.Repositories;
using StudentTime.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== CONFIGURATION DU PORT (Railway utilise la variable PORT) =====
// Le port est configuré via ASPNETCORE_URLS dans le script d'entrée Docker
// Cette configuration est redondante mais reste pour compatibilité
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}
else
{
    // Port par défaut si PORT n'est pas défini
    builder.WebHost.UseUrls("http://+:8080");
}

// ===== SERILOG CONFIGURATION =====
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/app-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
    .CreateLogger();

builder.Host.UseSerilog();

// ===== DATABASE =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var isProduction = builder.Environment.IsProduction();

// Log pour diagnostic
Log.Information("Environment: {Environment}", builder.Environment.EnvironmentName);
Log.Information("DATABASE_URL présent: {HasDatabaseUrl}", !string.IsNullOrEmpty(databaseUrl));
Log.Information("ConnectionStrings__DefaultConnection présent: {HasDefaultConnection}", !string.IsNullOrEmpty(connectionString));

// Conversion de DATABASE_URL (format Railway) vers format .NET si nécessaire
if (!string.IsNullOrEmpty(databaseUrl))
{
    if (databaseUrl.StartsWith("postgresql://"))
    {
        try
        {
            // Format Railway: postgresql://user:password@host:port/database
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            if (userInfo.Length == 2)
            {
                connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.LocalPath.TrimStart('/')};Username={userInfo[0]};Password={Uri.UnescapeDataString(userInfo[1])}";
                Log.Information("Connection string convertie depuis DATABASE_URL pour PostgreSQL");
            }
            else
            {
                Log.Error("Format DATABASE_URL invalide: userInfo.Length = {Length}", userInfo.Length);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erreur lors de la conversion de DATABASE_URL");
        }
    }
    else
    {
        // DATABASE_URL est déjà au format .NET
        connectionString = databaseUrl;
        Log.Information("Utilisation de DATABASE_URL directement");
    }
}

// Fallback si aucune chaîne de connexion n'est définie
if (string.IsNullOrEmpty(connectionString))
{
    if (isProduction)
    {
        throw new InvalidOperationException(
            "Aucune chaîne de connexion définie en production. " +
            "Définissez DATABASE_URL ou ConnectionStrings__DefaultConnection dans les variables d'environnement.");
    }
    connectionString = "Data Source=studenttime.db";
    Log.Warning("Utilisation de SQLite par défaut (développement uniquement)");
}

// Validation de la chaîne de connexion
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("La chaîne de connexion ne peut pas être vide.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    // Utiliser PostgreSQL si la chaîne contient "Host=" (format PostgreSQL)
    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        Log.Information("Configuration de PostgreSQL");
        options.UseNpgsql(connectionString, b => b.MigrationsAssembly("StudentTIme.API"));
    }
    else
    {
        // Sinon utiliser SQLite (développement uniquement)
        if (isProduction)
        {
            throw new InvalidOperationException(
                "SQLite ne peut pas être utilisé en production. " +
                "Configurez PostgreSQL via DATABASE_URL ou ConnectionStrings__DefaultConnection.");
        }
        Log.Information("Configuration de SQLite (développement)");
        options.UseSqlite(connectionString, b => b.MigrationsAssembly("StudentTIme.API"));
    }
});

// ===== REPOSITORIES =====
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
builder.Services.AddScoped<IPasswordResetCodeRepository, PasswordResetCodeRepository>();
builder.Services.AddScoped<IEmailVerificationCodeRepository, EmailVerificationCodeRepository>();

// ===== SERVICES =====
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITimeTrackingService, TimeTrackingService>();

// ===== AUTHENTICATION =====
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
        
        // Configurer pour lire le token depuis les cookies HttpOnly
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Priorité 1: Lire depuis le cookie HttpOnly
                if (context.Request.Cookies.ContainsKey("accessToken"))
                {
                    context.Token = context.Request.Cookies["accessToken"];
                    Log.Debug("Token lu depuis le cookie HttpOnly");
                }
                // Priorité 2: Fallback sur le header Authorization (pour compatibilité temporaire)
                else if (context.Request.Headers.ContainsKey("Authorization"))
                {
                    var authHeader = context.Request.Headers["Authorization"].ToString();
                    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Token = authHeader.Substring("Bearer ".Length).Trim();
                        Log.Debug("Token lu depuis le header Authorization (fallback)");
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        // Lire depuis la configuration ou les variables d'environnement
        var configOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        var envOrigins = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
        
        var allowedOrigins = new List<string>();
        
        // Ajouter les origines depuis la configuration
        if (configOrigins != null && configOrigins.Length > 0)
        {
            allowedOrigins.AddRange(configOrigins);
        }
        
        // Ajouter les origines depuis les variables d'environnement (séparées par des virgules)
        if (!string.IsNullOrEmpty(envOrigins))
        {
            allowedOrigins.AddRange(envOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        
        // Fallback pour le développement
        if (allowedOrigins.Count == 0)
        {
            allowedOrigins.Add("http://localhost:5173");
        }
        
        Log.Information("CORS - Origines autorisées: {Origins}", string.Join(", ", allowedOrigins));
        
        policy.WithOrigins(allowedOrigins.ToArray())
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// ===== CONTROLLERS & SWAGGER =====
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configuration UTF-8 explicite pour éviter les problèmes d'encodage (é → Ã©)
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StudentTime API",
        Version = "v1",
        Description = "API pour l'application de suivi de temps d'étude"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ===== DATABASE MIGRATION =====
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Log.Information("Application des migrations de base de données...");
        
        // Obtenir les migrations en attente
        var pendingMigrations = db.Database.GetPendingMigrations().ToList();
        if (pendingMigrations.Any())
        {
            Log.Information("Migrations en attente: {Count}", pendingMigrations.Count);
            foreach (var migration in pendingMigrations)
            {
                Log.Information("  - {Migration}", migration);
            }
        }
        else
        {
            Log.Information("Aucune migration en attente");
        }
        
        // Appliquer les migrations
        db.Database.Migrate();
        Log.Information("✅ Migrations appliquées avec succès");
        
        // Vérifier que la migration MarkExistingUsersAsVerified a bien été appliquée
        var appliedMigrations = db.Database.GetAppliedMigrations().ToList();
        if (appliedMigrations.Contains("20251201192648_MarkExistingUsersAsVerified"))
        {
            Log.Information("✅ Migration MarkExistingUsersAsVerified est appliquée");
        }
        else
        {
            Log.Warning("⚠️ Migration MarkExistingUsersAsVerified n'est pas encore appliquée");
        }
        
        // Diagnostic : Vérifier le type de colonne IsDeleted dans TimeEntries (PostgreSQL uniquement)
        try
        {
            var dbConnectionString = db.Database.GetConnectionString();
            if (dbConnectionString != null && dbConnectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("Vérification du type de colonne IsDeleted dans TimeEntries...");
                var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = @"
                    SELECT data_type 
                    FROM information_schema.columns 
                    WHERE table_schema = 'public' 
                    AND table_name = 'TimeEntries' 
                    AND column_name = 'IsDeleted'";
                db.Database.OpenConnection();
                try
                {
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        var dataType = reader.GetString(0);
                        Log.Information("Type de colonne IsDeleted: {DataType}", dataType);
                    }
                    reader.Close();
                }
                finally
                {
                    db.Database.CloseConnection();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Impossible de vérifier le type de colonne IsDeleted");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Erreur lors de l'application des migrations. Détails: {Message}", ex.Message);
        Log.Error(ex, "Stack trace: {StackTrace}", ex.StackTrace);
        // Ne pas faire échouer l'application si la migration échoue
        // Cela permet de diagnostiquer le problème via les logs
    }
}

// ===== MIDDLEWARE PIPELINE =====
// Activer Swagger en production pour la documentation publique
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "StudentTime API v1");
    c.RoutePrefix = string.Empty; // Swagger UI à la racine (optionnel)
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
else
{
    // Redirection HTTPS uniquement en production
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

// Middleware pour forcer UTF-8 dans toutes les réponses JSON
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        // Forcer UTF-8 pour éviter les problèmes d'encodage (é → Ã©)
        if (context.Response.ContentType != null && context.Response.ContentType.Contains("application/json"))
        {
            context.Response.ContentType = "application/json; charset=utf-8";
        }
        
        // COOP pour Google OAuth
        if (!context.Response.Headers.ContainsKey("Cross-Origin-Opener-Policy"))
        {
            context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin-allow-popups");
        }
        
        return Task.CompletedTask;
    });

    await next();
});

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ===== HEALTH CHECK =====
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
