using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTIme.API.Migrations
{
    /// <inheritdoc />
    public partial class FixEmailVerificationCodeBooleanType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convertir EmailVerificationCodes.IsUsed de INTEGER en BOOLEAN pour PostgreSQL
            // Et convertir CreatedAt/ExpiresAt de TEXT en TIMESTAMP
            // Cette migration utilise du SQL brut car EF Core ne peut pas changer le type directement
            // Le SQL ne s'exécutera que sur PostgreSQL (SQLite ignore les commandes PostgreSQL)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Convertir IsUsed de INTEGER vers BOOLEAN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'EmailVerificationCodes' 
                        AND column_name = 'IsUsed' 
                        AND data_type = 'integer'
                    ) THEN
                        BEGIN
                            ALTER TABLE ""EmailVerificationCodes"" 
                            ALTER COLUMN ""IsUsed"" TYPE boolean USING (CASE WHEN ""IsUsed"" = 1 THEN true ELSE false END);
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de EmailVerificationCodes.IsUsed: %', SQLERRM;
                        END;
                    END IF;
                    
                    -- Convertir CreatedAt de TEXT vers TIMESTAMP si nécessaire
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'EmailVerificationCodes' 
                        AND column_name = 'CreatedAt' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            -- Supprimer les valeurs invalides si nécessaire
                            DELETE FROM ""EmailVerificationCodes"" 
                            WHERE ""CreatedAt"" IS NULL OR ""CreatedAt"" = '';
                            
                            -- Convertir la colonne
                            ALTER TABLE ""EmailVerificationCodes"" 
                            ALTER COLUMN ""CreatedAt"" TYPE timestamp with time zone 
                            USING CASE 
                                WHEN ""CreatedAt"" ~ '^\d{4}-\d{2}-\d{2}' THEN ""CreatedAt""::timestamp with time zone
                                ELSE NOW()
                            END;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de EmailVerificationCodes.CreatedAt: %', SQLERRM;
                        END;
                    END IF;
                    
                    -- Convertir ExpiresAt de TEXT vers TIMESTAMP si nécessaire
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'EmailVerificationCodes' 
                        AND column_name = 'ExpiresAt' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            -- Supprimer les valeurs invalides si nécessaire
                            DELETE FROM ""EmailVerificationCodes"" 
                            WHERE ""ExpiresAt"" IS NULL OR ""ExpiresAt"" = '';
                            
                            -- Convertir la colonne
                            ALTER TABLE ""EmailVerificationCodes"" 
                            ALTER COLUMN ""ExpiresAt"" TYPE timestamp with time zone 
                            USING CASE 
                                WHEN ""ExpiresAt"" ~ '^\d{4}-\d{2}-\d{2}' THEN ""ExpiresAt""::timestamp with time zone
                                ELSE NOW() + INTERVAL '24 hours'
                            END;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de EmailVerificationCodes.ExpiresAt: %', SQLERRM;
                        END;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback : reconvertir en INTEGER et TEXT si nécessaire
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'EmailVerificationCodes' 
                        AND column_name = 'IsUsed' 
                        AND data_type = 'boolean'
                    ) THEN
                        ALTER TABLE ""EmailVerificationCodes"" 
                        ALTER COLUMN ""IsUsed"" TYPE integer USING (CASE WHEN ""IsUsed"" THEN 1 ELSE 0 END);
                    END IF;
                    
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'EmailVerificationCodes' 
                        AND column_name = 'CreatedAt' 
                        AND (data_type = 'timestamp without time zone' OR data_type = 'timestamp with time zone')
                    ) THEN
                        ALTER TABLE ""EmailVerificationCodes"" 
                        ALTER COLUMN ""CreatedAt"" TYPE text 
                        USING ""CreatedAt""::text;
                    END IF;
                    
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'EmailVerificationCodes' 
                        AND column_name = 'ExpiresAt' 
                        AND (data_type = 'timestamp without time zone' OR data_type = 'timestamp with time zone')
                    ) THEN
                        ALTER TABLE ""EmailVerificationCodes"" 
                        ALTER COLUMN ""ExpiresAt"" TYPE text 
                        USING ""ExpiresAt""::text;
                    END IF;
                END $$;
            ");
        }
    }
}
