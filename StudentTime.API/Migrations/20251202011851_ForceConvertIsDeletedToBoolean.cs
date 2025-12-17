using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTIme.API.Migrations
{
    /// <inheritdoc />
    public partial class ForceConvertIsDeletedToBoolean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Forcer la conversion d'IsDeleted de INTEGER vers BOOLEAN pour PostgreSQL
            // Cette migration utilise du SQL brut car EF Core ne peut pas changer le type directement
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Vérifier et convertir IsDeleted de INTEGER vers BOOLEAN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'IsDeleted' 
                        AND data_type = 'integer'
                    ) THEN
                        BEGIN
                            -- Convertir la colonne INTEGER vers BOOLEAN
                            ALTER TABLE ""TimeEntries"" 
                            ALTER COLUMN ""IsDeleted"" TYPE boolean 
                            USING (CASE WHEN ""IsDeleted"" = 1 THEN true ELSE false END);
                            
                            RAISE NOTICE 'Colonne IsDeleted convertie de INTEGER vers BOOLEAN avec succès';
                        EXCEPTION WHEN OTHERS THEN
                            RAISE EXCEPTION 'Erreur lors de la conversion de IsDeleted: %', SQLERRM;
                        END;
                    ELSIF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'IsDeleted' 
                        AND data_type = 'boolean'
                    ) THEN
                        RAISE NOTICE 'Colonne IsDeleted est déjà de type BOOLEAN, aucune conversion nécessaire';
                    ELSE
                        RAISE NOTICE 'Colonne IsDeleted introuvable dans TimeEntries';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback : reconvertir en INTEGER (non recommandé mais nécessaire pour rollback)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'public'
                        AND table_name = 'TimeEntries' 
                        AND column_name = 'IsDeleted' 
                        AND data_type = 'boolean'
                    ) THEN
                        BEGIN
                            ALTER TABLE ""TimeEntries"" 
                            ALTER COLUMN ""IsDeleted"" TYPE integer 
                            USING (CASE WHEN ""IsDeleted"" THEN 1 ELSE 0 END);
                            
                            RAISE NOTICE 'Colonne IsDeleted reconvertie de BOOLEAN vers INTEGER';
                        EXCEPTION WHEN OTHERS THEN
                            RAISE EXCEPTION 'Erreur lors du rollback de IsDeleted: %', SQLERRM;
                        END;
                    END IF;
                END $$;
            ");
        }
    }
}
