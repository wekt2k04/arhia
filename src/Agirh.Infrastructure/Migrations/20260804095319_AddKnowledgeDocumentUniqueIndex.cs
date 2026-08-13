using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agirh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeDocumentUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeDocuments_SourceFile_ChunkIndex",
                table: "KnowledgeDocuments",
                columns: new[] { "SourceFile", "ChunkIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeDocuments_SourceFile_ChunkIndex",
                table: "KnowledgeDocuments");
        }
    }
}
