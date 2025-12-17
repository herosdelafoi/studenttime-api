using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTIme.API.Migrations
{
    /// <inheritdoc />
    public partial class FixTimeEntryIndexFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mettre à jour le filtre d'index pour IsDeleted dans TimeEntries
            // Après la conversion INTEGER → BOOLEAN, le filtre doit utiliser false au lieu de 0
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Vérifier si la colonne IsDeleted est de type BOOLEAN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'IsDeleted' 
                        AND data_type = 'boolean'
                    ) THEN
                        BEGIN
                            -- Supprimer l'ancien index
                            DROP INDEX IF EXISTS ""IX_TimeEntries_IsDeleted"";
                            
                            -- Recréer l'index avec le bon filtre pour BOOLEAN
                            CREATE INDEX ""IX_TimeEntries_IsDeleted""
                            ON ""TimeEntries"" (""IsDeleted"")
                            WHERE ""IsDeleted"" = false;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors de la mise à jour du filtre d''index IsDeleted: %', SQLERRM;
                        END;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback : remettre le filtre avec 0 (pour INTEGER)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Vérifier si la colonne IsDeleted existe
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'IsDeleted'
                    ) THEN
                        BEGIN
                            -- Supprimer l'index
                            DROP INDEX IF EXISTS ""IX_TimeEntries_IsDeleted"";
                            
                            -- Recréer l'index avec le filtre pour INTEGER
                            CREATE INDEX ""IX_TimeEntries_IsDeleted""
                            ON ""TimeEntries"" (""IsDeleted"")
                            WHERE ""IsDeleted"" = 0;
                        EXCEPTION WHEN OTHERS THEN
                            RAISE NOTICE 'Erreur lors du rollback du filtre d''index IsDeleted: %', SQLERRM;
                        END;
                    END IF;
                END $$;
            ");
        }
    }
}
