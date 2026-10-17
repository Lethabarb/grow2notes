using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grow2Notes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Participant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GivenName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FamilyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Participant", x => x.Id);
                    table.UniqueConstraint("AK_Participant_OrganisationId_Id", x => new { x.OrganisationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Participant_Organisation_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Participant_OrganisationId_Status_FamilyName_GivenName",
                table: "Participant",
                columns: new[] { "OrganisationId", "Status", "FamilyName", "GivenName" });

            // A participant is archived, never deleted (design.md §3.7, A29), so the database itself refuses the app's
            // identity a removal of a row, even if its code has a bug (§5.8, §10.6). InitialCreate created the role.
            migrationBuilder.Sql("DENY DELETE ON [dbo].[Participant] TO [grow2notes_runtime];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table would drop the deny with it; revoking it first keeps Down the mirror of Up, as
            // AddAuditEvent's is. Production never runs Down (design.md §10.6).
            migrationBuilder.Sql("REVOKE DELETE ON [dbo].[Participant] FROM [grow2notes_runtime];");

            migrationBuilder.DropTable(
                name: "Participant");
        }
    }
}
