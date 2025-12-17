using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTIme.API.Migrations
{
    /// <inheritdoc />
    public partial class MarkExistingUsersAsVerified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Marquer tous les utilisateurs existants comme vérifiés (Option A)
            // Cela permet aux anciens utilisateurs de lier leur compte Google automatiquement
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Marquer tous les utilisateurs existants comme vérifiés
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                        AND table_name = 'Users'
                    ) THEN
                        UPDATE ""Users""
                        SET ""EmailVerified"" = TRUE
                        WHERE ""EmailVerified"" = FALSE;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback : remettre tous les utilisateurs comme non vérifiés
            // (Non recommandé mais nécessaire pour rollback)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                        AND table_name = 'Users'
                    ) THEN
                        UPDATE ""Users""
                        SET ""EmailVerified"" = FALSE
                        WHERE ""EmailVerified"" = TRUE;
                    END IF;
                END $$;
            ");
        }
    }
}
