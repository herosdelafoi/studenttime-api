using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTIme.API.Migrations
{
    /// <inheritdoc />
    public partial class FixBooleanTypesForPostgreSQL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convertir les colonnes INTEGER en BOOLEAN pour PostgreSQL
            // Cette migration utilise du SQL brut car EF Core ne peut pas changer le type directement
            // Le SQL ne s'exécutera que sur PostgreSQL (SQLite ignore les commandes PostgreSQL)
            
            // Conversion pour Users.IsActive
            // Note: Cette migration ne s'exécute que sur PostgreSQL
            // SQLite ignore les commandes PostgreSQL (DO $$ ... END $$;)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'Users' 
                        AND column_name = 'IsActive' 
                        AND data_type = 'integer'
                    ) THEN
                        BEGIN
                            ALTER TABLE ""Users"" 
                            ALTER COLUMN ""IsActive"" TYPE boolean USING (CASE WHEN ""IsActive"" = 1 THEN true ELSE false END);
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de IsActive: %', SQLERRM;
                        END;
                    END IF;
                END $$;
            ");
            
            // Conversion pour Users.EmailVerified
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'Users' 
                        AND column_name = 'EmailVerified' 
                        AND data_type = 'integer'
                    ) THEN
                        BEGIN
                            ALTER TABLE ""Users"" 
                            ALTER COLUMN ""EmailVerified"" TYPE boolean USING (CASE WHEN ""EmailVerified"" = 1 THEN true ELSE false END);
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de EmailVerified: %', SQLERRM;
                        END;
                    END IF;
                END $$;
            ");
            
            // Conversion pour TimeEntries.IsDeleted
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'IsDeleted' 
                        AND data_type = 'integer'
                    ) THEN
                        BEGIN
                            ALTER TABLE ""TimeEntries"" 
                            ALTER COLUMN ""IsDeleted"" TYPE boolean USING (CASE WHEN ""IsDeleted"" = 1 THEN true ELSE false END);
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de IsDeleted: %', SQLERRM;
                        END;
                    END IF;
                END $$;
            ");
            
            // Conversion pour PasswordResetCodes.IsUsed
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'PasswordResetCodes' 
                        AND column_name = 'IsUsed' 
                        AND data_type = 'integer'
                    ) THEN
                        BEGIN
                            ALTER TABLE ""PasswordResetCodes"" 
                            ALTER COLUMN ""IsUsed"" TYPE boolean USING (CASE WHEN ""IsUsed"" = 1 THEN true ELSE false END);
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de IsUsed: %', SQLERRM;
                        END;
                    END IF;
                END $$;
            ");
            
            // Re-créer les index avec les bons filtres pour PostgreSQL
            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_IsDeleted",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_UserId_EndTime",
                table: "TimeEntries");

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true,
                filter: "\"GoogleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_IsDeleted",
                table: "TimeEntries",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_UserId_EndTime",
                table: "TimeEntries",
                columns: new[] { "UserId", "EndTime" },
                filter: "\"EndTime\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reconvertir en INTEGER si nécessaire (pour rollback)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'Users' 
                        AND column_name = 'IsActive' 
                        AND data_type = 'boolean'
                    ) THEN
                        ALTER TABLE ""Users"" 
                        ALTER COLUMN ""IsActive"" TYPE integer USING (CASE WHEN ""IsActive"" THEN 1 ELSE 0 END);
                    END IF;
                    
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'Users' 
                        AND column_name = 'EmailVerified' 
                        AND data_type = 'boolean'
                    ) THEN
                        ALTER TABLE ""Users"" 
                        ALTER COLUMN ""EmailVerified"" TYPE integer USING (CASE WHEN ""EmailVerified"" THEN 1 ELSE 0 END);
                    END IF;
                    
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'IsDeleted' 
                        AND data_type = 'boolean'
                    ) THEN
                        ALTER TABLE ""TimeEntries"" 
                        ALTER COLUMN ""IsDeleted"" TYPE integer USING (CASE WHEN ""IsDeleted"" THEN 1 ELSE 0 END);
                    END IF;
                    
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'PasswordResetCodes' 
                        AND column_name = 'IsUsed' 
                        AND data_type = 'boolean'
                    ) THEN
                        ALTER TABLE ""PasswordResetCodes"" 
                        ALTER COLUMN ""IsUsed"" TYPE integer USING (CASE WHEN ""IsUsed"" THEN 1 ELSE 0 END);
                    END IF;
                END $$;
            ");
            
            // Restaurer les index avec les anciens filtres
            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_IsDeleted",
                table: "TimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_UserId_EndTime",
                table: "TimeEntries");

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true,
                filter: "GoogleId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_IsDeleted",
                table: "TimeEntries",
                column: "IsDeleted",
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_UserId_EndTime",
                table: "TimeEntries",
                columns: new[] { "UserId", "EndTime" },
                filter: "EndTime IS NULL");
        }
    }
}
