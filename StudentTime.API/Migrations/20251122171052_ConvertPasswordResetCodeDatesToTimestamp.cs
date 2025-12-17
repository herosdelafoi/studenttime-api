using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTIme.API.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPasswordResetCodeDatesToTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convertir les colonnes TEXT en TIMESTAMP pour PostgreSQL
            // Cette migration utilise du SQL brut car EF Core ne peut pas changer le type directement
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Convertir CreatedAt de TEXT vers TIMESTAMP
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'PasswordResetCodes' 
                        AND column_name = 'CreatedAt' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            ALTER TABLE ""PasswordResetCodes"" 
                            ALTER COLUMN ""CreatedAt"" TYPE timestamp with time zone 
                            USING ""CreatedAt""::timestamp with time zone;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de CreatedAt: %', SQLERRM;
                        END;
                    END IF;
                    
                    -- Convertir ExpiresAt de TEXT vers TIMESTAMP
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'PasswordResetCodes' 
                        AND column_name = 'ExpiresAt' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            ALTER TABLE ""PasswordResetCodes"" 
                            ALTER COLUMN ""ExpiresAt"" TYPE timestamp with time zone 
                            USING ""ExpiresAt""::timestamp with time zone;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de ExpiresAt: %', SQLERRM;
                        END;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reconvertir en TEXT si nécessaire (pour rollback)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'PasswordResetCodes' 
                        AND column_name = 'CreatedAt' 
                        AND (data_type = 'timestamp without time zone' OR data_type = 'timestamp with time zone')
                    ) THEN
                        ALTER TABLE ""PasswordResetCodes"" 
                        ALTER COLUMN ""CreatedAt"" TYPE text 
                        USING ""CreatedAt""::text;
                    END IF;
                    
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'PasswordResetCodes' 
                        AND column_name = 'ExpiresAt' 
                        AND (data_type = 'timestamp without time zone' OR data_type = 'timestamp with time zone')
                    ) THEN
                        ALTER TABLE ""PasswordResetCodes"" 
                        ALTER COLUMN ""ExpiresAt"" TYPE text 
                        USING ""ExpiresAt""::text;
                    END IF;
                END $$;
            ");
        }
    }
}
