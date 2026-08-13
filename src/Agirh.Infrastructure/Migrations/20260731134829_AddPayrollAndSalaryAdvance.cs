using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agirh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollAndSalaryAdvance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "KnowledgeDocuments");

            migrationBuilder.AddColumn<byte[]>(
                name: "Embedding",
                table: "KnowledgeDocuments",
                type: "vector(768)",
                nullable: false);

            migrationBuilder.CreateTable(
                name: "PayrollProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NetSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: false),
                    MaxAdvancePercentage = table.Column<decimal>(type: "decimal(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollProfiles_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalaryAdvanceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmountRequested = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryAdvanceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryAdvanceRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollProfiles_EmployeeId",
                table: "PayrollProfiles",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvanceRequests_EmployeeId",
                table: "SalaryAdvanceRequests",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollProfiles");

            migrationBuilder.DropTable(
                name: "SalaryAdvanceRequests");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "KnowledgeDocuments");

            migrationBuilder.AddColumn<byte[]>(
                name: "Embedding",
                table: "KnowledgeDocuments",
                type: "varbinary(max)",
                nullable: false);
        }
    }
}
