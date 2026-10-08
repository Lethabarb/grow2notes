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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvent");
        }
    }
}
