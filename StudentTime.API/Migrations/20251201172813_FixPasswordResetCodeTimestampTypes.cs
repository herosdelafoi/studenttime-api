using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTIme.API.Migrations
{
    /// <inheritdoc />
    public partial class FixPasswordResetCodeTimestampTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Forcer la conversion des colonnes TEXT en TIMESTAMP pour PostgreSQL
            // Cette migration corrige le problème où les colonnes sont encore en TEXT
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Convertir CreatedAt de TEXT vers TIMESTAMP si nécessaire
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'PasswordResetCodes' 
                        AND column_name = 'CreatedAt' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            -- Supprimer les valeurs invalides si nécessaire
                            DELETE FROM ""PasswordResetCodes"" 
                            WHERE ""CreatedAt"" IS NULL OR ""CreatedAt"" = '';
                            
                            -- Convertir la colonne
                            ALTER TABLE ""PasswordResetCodes"" 
                            ALTER COLUMN ""CreatedAt"" TYPE timestamp with time zone 
                            USING CASE 
                                WHEN ""CreatedAt"" ~ '^\d{4}-\d{2}-\d{2}' THEN ""CreatedAt""::timestamp with time zone
                                ELSE NOW()
                            END;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de CreatedAt: %', SQLERRM;
                        END;
                    END IF;
                    
                    -- Convertir ExpiresAt de TEXT vers TIMESTAMP si nécessaire
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'PasswordResetCodes' 
                        AND column_name = 'ExpiresAt' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            -- Supprimer les valeurs invalides si nécessaire
                            DELETE FROM ""PasswordResetCodes"" 
                            WHERE ""ExpiresAt"" IS NULL OR ""ExpiresAt"" = '';
                            
                            -- Convertir la colonne
                            ALTER TABLE ""PasswordResetCodes"" 
                            ALTER COLUMN ""ExpiresAt"" TYPE timestamp with time zone 
                            USING CASE 
                                WHEN ""ExpiresAt"" ~ '^\d{4}-\d{2}-\d{2}' THEN ""ExpiresAt""::timestamp with time zone
                                ELSE NOW() + INTERVAL '15 minutes'
                            END;
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
            // Rollback : reconvertir en TEXT (non recommandé mais nécessaire pour rollback)
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
