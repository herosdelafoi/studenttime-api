using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Infrastructure;
using StudentTime.Core.Entities;

namespace StudentTime.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly bool _isPostgreSQL;
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        // Détecter PostgreSQL au moment de la construction en vérifiant les extensions
        _isPostgreSQL = DetectPostgreSQL(options);
    }
    
    private bool DetectPostgreSQL(DbContextOptions<AppDbContext> options)
    {
        try
        {
            // Vérifier via les extensions du provider
            var extension = options.Extensions.FirstOrDefault(e => 
                e.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
            if (extension != null)
                return true;
            
            // Vérifier aussi via Database.ProviderName si disponible
            try
            {
                var providerName = Database.ProviderName ?? "";
                if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) || 
                    providerName.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    // Méthode pour détecter PostgreSQL (accessible après la configuration)
    private bool IsPostgreSQL()
    {
        try
        {
            // Vérifier le provider via Database.ProviderName ou GetDbConnection
            var providerName = Database.ProviderName ?? "";
            if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) || 
                providerName.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                return true;
            
            // Vérifier aussi via le type de connexion
            var connection = Database.GetDbConnection();
            return connection?.GetType().Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch
        {
            // Si on ne peut pas accéder, assumer SQLite par défaut
            return false;
        }
    }
    
    // Méthode helper pour détecter PostgreSQL dans OnModelCreating
    // Utilise le champ _isPostgreSQL qui a été détecté au moment de la construction
    private bool IsPostgreSQL(ModelBuilder modelBuilder)
    {
        return _isPostgreSQL;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuration User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            // Filtre compatible SQLite et PostgreSQL
            entity.HasIndex(e => e.GoogleId).IsUnique().HasFilter("\"GoogleId\" IS NOT NULL");
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.GoogleId).HasMaxLength(255);
            
            // Configuration booléenne : configuration conditionnelle selon le provider
            // PostgreSQL : utiliser BOOLEAN directement (après migration)
            // SQLite : utiliser INTEGER avec conversion
            bool isPostgresUser = IsPostgreSQL(modelBuilder);
            if (isPostgresUser)
            {
                // PostgreSQL : Npgsql gère automatiquement bool C# → BOOLEAN PostgreSQL
                // Pas besoin de conversion explicite
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.EmailVerified).IsRequired();
            }
            else
            {
                // SQLite : utiliser INTEGER avec conversion
                entity.Property(e => e.IsActive)
                    .IsRequired()
                    .HasConversion(
                        v => v ? 1 : 0,  // C# bool → DB INTEGER
                        v => v != 0);     // DB INTEGER → C# bool
                
                entity.Property(e => e.EmailVerified)
                    .IsRequired()
                    .HasConversion(
                        v => v ? 1 : 0,
                        v => v != 0);
            }
            
            // Conversion DateTime vers TEXT (ISO 8601) pour SQLite
            entity.Property(e => e.CreatedAt)
                .HasConversion(
                    v => v.ToString("o"),
                    v => DateTime.Parse(v).ToUniversalTime());
            entity.Property(e => e.LastLoginAt)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToString("o") : null,
                    v => v != null ? DateTime.Parse(v).ToUniversalTime() : (DateTime?)null);
        });

        // Configuration TimeEntry
        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.StartTime);
            
            // Filtre compatible SQLite et PostgreSQL
            entity.HasIndex(e => new { e.UserId, e.EndTime }).HasFilter("\"EndTime\" IS NULL");
            // Index sur IsDeleted sans filtre pour éviter les problèmes de compatibilité
            // Le filtre sera ajouté par la migration FixTimeEntryIndexFilter après conversion BOOLEAN
            entity.HasIndex(e => e.IsDeleted);
            
            // Configuration booléenne : configuration conditionnelle selon le provider
            // PostgreSQL : utiliser BOOLEAN directement (après migration)
            // SQLite : utiliser INTEGER avec conversion
            bool isPostgresTimeEntry = IsPostgreSQL(modelBuilder);
            if (isPostgresTimeEntry)
            {
                // PostgreSQL : Npgsql gère automatiquement bool C# → BOOLEAN PostgreSQL
                // Pas besoin de conversion explicite
                entity.Property(e => e.IsDeleted).IsRequired();
            }
            else
            {
                // SQLite : utiliser INTEGER avec conversion
                entity.Property(e => e.IsDeleted)
                    .IsRequired()
                    .HasConversion(
                        v => v ? 1 : 0,  // C# bool → DB INTEGER
                        v => v != 0);     // DB INTEGER → C# bool
            }
            
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            
            // Conversions DateTime : configuration conditionnelle selon le provider
            // PostgreSQL : laisser Npgsql gérer automatiquement (après migration, colonnes en TIMESTAMP)
            // SQLite : utiliser conversion TEXT
            if (isPostgresTimeEntry)
            {
                // PostgreSQL : Npgsql gère automatiquement DateTime → TIMESTAMP
                // Les migrations ont converti les colonnes en TIMESTAMP
                // Pas besoin de conversion explicite
                entity.Property(e => e.StartTime);
                entity.Property(e => e.EndTime);
                entity.Property(e => e.CreatedAt);
                entity.Property(e => e.UpdatedAt);
            }
            else
            {
                // SQLite : utiliser conversion TEXT
                entity.Property(e => e.StartTime)
                    .HasConversion(
                        v => v.ToString("o"),
                        v => DateTime.Parse(v).ToUniversalTime());
                entity.Property(e => e.EndTime)
                    .HasConversion(
                        v => v.HasValue ? v.Value.ToString("o") : null,
                        v => v != null ? DateTime.Parse(v).ToUniversalTime() : (DateTime?)null);
                entity.Property(e => e.CreatedAt)
                    .HasConversion(
                        v => v.ToString("o"),
                        v => DateTime.Parse(v).ToUniversalTime());
                entity.Property(e => e.UpdatedAt)
                    .HasConversion(
                        v => v.ToString("o"),
                        v => DateTime.Parse(v).ToUniversalTime());
            }
            
            // Relation
            entity.HasOne(e => e.User)
                .WithMany(u => u.TimeEntries)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuration PasswordResetCode
        modelBuilder.Entity<PasswordResetCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Email, e.Code });
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
            
            // Configuration booléenne : configuration conditionnelle selon le provider
            bool isPostgresPasswordReset = IsPostgreSQL(modelBuilder);
            if (isPostgresPasswordReset)
            {
                // PostgreSQL : Npgsql gère automatiquement bool C# → BOOLEAN PostgreSQL
                entity.Property(e => e.IsUsed).IsRequired();
            }
            else
            {
                // SQLite : utiliser INTEGER avec conversion
                entity.Property(e => e.IsUsed)
                    .IsRequired()
                    .HasConversion(
                        v => v ? 1 : 0,
                        v => v != 0);
            }
            
            // Conversion DateTime : configuration conditionnelle selon le provider
            // PostgreSQL : laisser Npgsql gérer automatiquement (après migration, colonnes en TIMESTAMP)
            // SQLite : utiliser conversion TEXT
            if (isPostgresPasswordReset)
            {
                // PostgreSQL : Npgsql gère automatiquement DateTime → TIMESTAMP
                // Les migrations ont converti les colonnes en TIMESTAMP
                // Pas besoin de conversion explicite
                entity.Property(e => e.CreatedAt);
                entity.Property(e => e.ExpiresAt);
            }
            else
            {
                // SQLite : utiliser conversion TEXT
                entity.Property(e => e.CreatedAt)
                    .HasConversion(
                        v => v.ToString("o"),
                        v => DateTime.Parse(v).ToUniversalTime());
                entity.Property(e => e.ExpiresAt)
                    .HasConversion(
                        v => v.ToString("o"),
                        v => DateTime.Parse(v).ToUniversalTime());
            }
        });

        // Configuration EmailVerificationCode
        modelBuilder.Entity<EmailVerificationCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Email, e.Code });
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
            
            // Configuration booléenne : configuration conditionnelle selon le provider
            bool isPostgresEmailVerification = IsPostgreSQL(modelBuilder);
            if (isPostgresEmailVerification)
            {
                // PostgreSQL : Npgsql gère automatiquement bool C# → BOOLEAN PostgreSQL
                entity.Property(e => e.IsUsed).IsRequired();
            }
            else
            {
                // SQLite : utiliser INTEGER avec conversion
                entity.Property(e => e.IsUsed)
                    .IsRequired()
                    .HasConversion(
                        v => v ? 1 : 0,
                        v => v != 0);
            }
            
            // Conversion DateTime : configuration conditionnelle selon le provider
            // PostgreSQL : laisser Npgsql gérer automatiquement (après migration, colonnes en TIMESTAMP)
            // SQLite : utiliser conversion TEXT
            if (isPostgresEmailVerification)
            {
                // PostgreSQL : Npgsql gère automatiquement DateTime → TIMESTAMP
                // Les migrations ont converti les colonnes en TIMESTAMP
                // Pas besoin de conversion explicite
                entity.Property(e => e.CreatedAt);
                entity.Property(e => e.ExpiresAt);
            }
            else
            {
                // SQLite : utiliser conversion TEXT
                entity.Property(e => e.CreatedAt)
                    .HasConversion(
                        v => v.ToString("o"),
                        v => DateTime.Parse(v).ToUniversalTime());
                entity.Property(e => e.ExpiresAt)
                    .HasConversion(
                        v => v.ToString("o"),
                        v => DateTime.Parse(v).ToUniversalTime());
            }
        });
    }
}

