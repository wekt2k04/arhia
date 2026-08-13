using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agirh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquePendingSalaryAdvanceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalaryAdvanceRequests_EmployeeId",
                table: "SalaryAdvanceRequests");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvanceRequests_EmployeeId",
                table: "SalaryAdvanceRequests",
                column: "EmployeeId",
                unique: true,
                filter: "Status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalaryAdvanceRequests_EmployeeId",
                table: "SalaryAdvanceRequests");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdvanceRequests_EmployeeId",
                table: "SalaryAdvanceRequests",
                column: "EmployeeId");
        }
    }
}
