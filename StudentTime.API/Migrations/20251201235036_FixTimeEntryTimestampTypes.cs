using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTIme.API.Migrations
{
    /// <inheritdoc />
    public partial class FixTimeEntryTimestampTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convertir les colonnes DateTime de TimeEntry de TEXT vers TIMESTAMP pour PostgreSQL
            // Cette migration utilise du SQL brut car EF Core ne peut pas changer le type directement
            // Le SQL ne s'exécutera que sur PostgreSQL (SQLite ignore les commandes PostgreSQL)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Convertir StartTime de TEXT vers TIMESTAMP si nécessaire
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'StartTime' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            -- Supprimer les valeurs invalides si nécessaire
                            DELETE FROM ""TimeEntries"" 
                            WHERE ""StartTime"" IS NULL OR ""StartTime"" = '';
                            
                            -- Convertir la colonne
                            ALTER TABLE ""TimeEntries"" 
                            ALTER COLUMN ""StartTime"" TYPE timestamp with time zone 
                            USING CASE 
                                WHEN ""StartTime"" ~ '^\d{4}-\d{2}-\d{2}' THEN ""StartTime""::timestamp with time zone
                                ELSE NOW()
                            END;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de TimeEntries.StartTime: %', SQLERRM;
                        END;
                    END IF;
                    
                    -- Convertir EndTime de TEXT vers TIMESTAMP si nécessaire
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'EndTime' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            -- Supprimer les valeurs invalides si nécessaire
                            DELETE FROM ""TimeEntries"" 
                            WHERE ""EndTime"" IS NOT NULL AND (""EndTime"" = '');
                            
                            -- Convertir la colonne
                            ALTER TABLE ""TimeEntries"" 
                            ALTER COLUMN ""EndTime"" TYPE timestamp with time zone 
                            USING CASE 
                                WHEN ""EndTime"" IS NULL THEN NULL
                                WHEN ""EndTime"" ~ '^\d{4}-\d{2}-\d{2}' THEN ""EndTime""::timestamp with time zone
                                ELSE NULL
                            END;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de TimeEntries.EndTime: %', SQLERRM;
                        END;
                    END IF;
                    
                    -- Convertir CreatedAt de TEXT vers TIMESTAMP si nécessaire
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'CreatedAt' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            -- Supprimer les valeurs invalides si nécessaire
                            DELETE FROM ""TimeEntries"" 
                            WHERE ""CreatedAt"" IS NULL OR ""CreatedAt"" = '';
                            
                            -- Convertir la colonne
                            ALTER TABLE ""TimeEntries"" 
                            ALTER COLUMN ""CreatedAt"" TYPE timestamp with time zone 
                            USING CASE 
                                WHEN ""CreatedAt"" ~ '^\d{4}-\d{2}-\d{2}' THEN ""CreatedAt""::timestamp with time zone
                                ELSE NOW()
                            END;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de TimeEntries.CreatedAt: %', SQLERRM;
                        END;
                    END IF;
                    
                    -- Convertir UpdatedAt de TEXT vers TIMESTAMP si nécessaire
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'UpdatedAt' 
                        AND data_type = 'text'
                    ) THEN
                        BEGIN
                            -- Supprimer les valeurs invalides si nécessaire
                            DELETE FROM ""TimeEntries"" 
                            WHERE ""UpdatedAt"" IS NULL OR ""UpdatedAt"" = '';
                            
                            -- Convertir la colonne
                            ALTER TABLE ""TimeEntries"" 
                            ALTER COLUMN ""UpdatedAt"" TYPE timestamp with time zone 
                            USING CASE 
                                WHEN ""UpdatedAt"" ~ '^\d{4}-\d{2}-\d{2}' THEN ""UpdatedAt""::timestamp with time zone
                                ELSE NOW()
                            END;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la conversion de TimeEntries.UpdatedAt: %', SQLERRM;
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
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'StartTime' 
                        AND (data_type = 'timestamp without time zone' OR data_type = 'timestamp with time zone')
                    ) THEN
                        ALTER TABLE ""TimeEntries"" 
                        ALTER COLUMN ""StartTime"" TYPE text 
                        USING ""StartTime""::text;
                    END IF;
                    
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'EndTime' 
                        AND (data_type = 'timestamp without time zone' OR data_type = 'timestamp with time zone')
                    ) THEN
                        ALTER TABLE ""TimeEntries"" 
                        ALTER COLUMN ""EndTime"" TYPE text 
                        USING ""EndTime""::text;
                    END IF;
                    
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'CreatedAt' 
                        AND (data_type = 'timestamp without time zone' OR data_type = 'timestamp with time zone')
                    ) THEN
                        ALTER TABLE ""TimeEntries"" 
                        ALTER COLUMN ""CreatedAt"" TYPE text 
                        USING ""CreatedAt""::text;
                    END IF;
                    
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'UpdatedAt' 
                        AND (data_type = 'timestamp without time zone' OR data_type = 'timestamp with time zone')
                    ) THEN
                        ALTER TABLE ""TimeEntries"" 
                        ALTER COLUMN ""UpdatedAt"" TYPE text 
                        USING ""UpdatedAt""::text;
                    END IF;
                END $$;
            ");
        }
    }
}
