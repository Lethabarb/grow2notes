using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grow2Notes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Goal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goal", x => x.Id);
                    table.UniqueConstraint("AK_Goal_OrganisationId_Id", x => new { x.OrganisationId, x.Id });
                    table.ForeignKey(
                        name: "FK_Goal_Participant_OrganisationId_ParticipantId",
                        columns: x => new { x.OrganisationId, x.ParticipantId },
                        principalTable: "Participant",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Goal_OrganisationId_ParticipantId_ArchivedAtUtc_SortOrder",
                table: "Goal",
                columns: new[] { "OrganisationId", "ParticipantId", "ArchivedAtUtc", "SortOrder" });

            // A goal is archived, never deleted (design.md §3.7, A2), so the database itself refuses the app's identity
            // a removal of a row, even if its code has a bug (§5.8, §10.6). InitialCreate created the role.
            migrationBuilder.Sql("DENY DELETE ON [dbo].[Goal] TO [grow2notes_runtime];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table would drop the deny with it; revoking it first keeps Down the mirror of Up, as
            // AddParticipant's is. Production never runs Down (design.md §10.6).
            migrationBuilder.Sql("REVOKE DELETE ON [dbo].[Goal] FROM [grow2notes_runtime];");

            migrationBuilder.DropTable(
                name: "Goal");
        }
    }
}
