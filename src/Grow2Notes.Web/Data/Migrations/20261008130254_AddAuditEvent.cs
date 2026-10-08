using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grow2Notes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvent",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(
                        type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(
                        type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_OrganisationId_ActorUserId_OccurredAtUtc",
                table: "AuditEvent",
                columns: new[] { "OrganisationId", "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_OrganisationId_OccurredAtUtc",
                table: "AuditEvent",
                columns: new[] { "OrganisationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvent_OrganisationId_ParticipantId_OccurredAtUtc",
                table: "AuditEvent",
                columns: new[] { "OrganisationId", "ParticipantId", "OccurredAtUtc" },
                filter: "[ParticipantId] IS NOT NULL");

            // The audit log is append-only, so the database itself refuses the app's identity a change or removal of a
            // row, even if its code has a bug (design.md §5.8, §10.6). InitialCreate created the role.
            migrationBuilder.Sql("DENY UPDATE, DELETE ON [dbo].[AuditEvent] TO [grow2notes_runtime];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table would drop the deny with it; revoking it first keeps Down the mirror of Up, as
            // InitialCreate's is. Production never runs Down (design.md §10.6).
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON [dbo].[AuditEvent] FROM [grow2notes_runtime];");

            migrationBuilder.DropTable(
                name: "AuditEvent");
        }
    }
}
