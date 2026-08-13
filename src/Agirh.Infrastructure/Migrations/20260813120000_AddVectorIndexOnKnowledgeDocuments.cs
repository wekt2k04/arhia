using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agirh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorIndexOnKnowledgeDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server 2025 RTM supporte VECTOR_DISTANCE mais pas encore CREATE VECTOR INDEX
            // dans toutes les CU. On tente la création via EXEC dynamique (parse différé)
            // encapsulé dans TRY/CATCH : la migration réussit toujours, et l'index sera
            // créé automatiquement si la CU courante le supporte.
            // Syntaxe SQL Server 2025 : WITH (metric = 'cosine') — pas de USING HNSW (pgvector).
            migrationBuilder.Sql("""
                BEGIN TRY
                    EXEC('CREATE VECTOR INDEX IX_KnowledgeDocuments_Embedding
                          ON KnowledgeDocuments(Embedding)
                          WITH (metric = ''cosine'')');
                END TRY
                BEGIN CATCH
                    -- CREATE VECTOR INDEX non disponible dans cette CU de SQL Server 2025.
                    -- VECTOR_DISTANCE reste fonctionnel via full table scan.
                END CATCH
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                BEGIN TRY
                    EXEC('DROP INDEX IF EXISTS IX_KnowledgeDocuments_Embedding ON KnowledgeDocuments');
                END TRY
                BEGIN CATCH
                END CATCH
                """);
        }
    }
}
