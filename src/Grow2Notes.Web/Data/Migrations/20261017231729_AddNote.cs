using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grow2Notes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Note",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FirstSubmittedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    FlagStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    FlaggedVersionNumber = table.Column<int>(type: "int", nullable: true),
                    DiscardedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    DiscardedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Note", x => x.Id);
                    table.UniqueConstraint("AK_Note_OrganisationId_Id", x => new { x.OrganisationId, x.Id });
                    table.CheckConstraint("CK_Note_Status",
                        "([Status] = 0 AND [CurrentVersionNumber] = 0 AND [FirstSubmittedAtUtc] IS NULL"
                        + " AND [DiscardedAtUtc] IS NULL)"
                        + " OR ([Status] = 1 AND [CurrentVersionNumber] >= 1"
                        + " AND [FirstSubmittedAtUtc] IS NOT NULL AND [DiscardedAtUtc] IS NULL)"
                        + " OR ([Status] = 2 AND [CurrentVersionNumber] = 0 AND [DiscardedAtUtc] IS NOT NULL"
                        + " AND [DiscardedByUserId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_Note_AspNetUsers_OrganisationId_AuthorUserId",
                        columns: x => new { x.OrganisationId, x.AuthorUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Note_AspNetUsers_OrganisationId_DiscardedByUserId",
                        columns: x => new { x.OrganisationId, x.DiscardedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Note_Participant_OrganisationId_ParticipantId",
                        columns: x => new { x.OrganisationId, x.ParticipantId },
                        principalTable: "Participant",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteDraft",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EditorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsFlagged = table.Column<bool>(type: "bit", nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastSeq = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Groups = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Items = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteDraft", x => new { x.NoteId, x.EditorUserId });
                    table.ForeignKey(
                        name: "FK_NoteDraft_AspNetUsers_OrganisationId_EditorUserId",
                        columns: x => new { x.OrganisationId, x.EditorUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteDraft_Note_OrganisationId_NoteId",
                        columns: x => new { x.OrganisationId, x.NoteId },
                        principalTable: "Note",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Note_OrganisationId_AuthorUserId",
                table: "Note",
                columns: new[] { "OrganisationId", "AuthorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Note_OrganisationId_DiscardedByUserId",
                table: "Note",
                columns: new[] { "OrganisationId", "DiscardedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Note_OrganisationId_FlagStatus",
                table: "Note",
                columns: new[] { "OrganisationId", "FlagStatus" },
                filter: "[FlagStatus] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Note_OrganisationId_NoteDate",
                table: "Note",
                columns: new[] { "OrganisationId", "NoteDate" })
                .Annotation("SqlServer:Include", new[] { "Status", "ParticipantId", "AuthorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Note_OrganisationId_ParticipantId_NoteDate",
                table: "Note",
                columns: new[] { "OrganisationId", "ParticipantId", "NoteDate" },
                unique: true,
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_NoteDraft_OrganisationId_EditorUserId",
                table: "NoteDraft",
                columns: new[] { "OrganisationId", "EditorUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteDraft_OrganisationId_NoteId",
                table: "NoteDraft",
                columns: new[] { "OrganisationId", "NoteId" });

            // A note is discarded, never deleted (design.md §5.5 item 5, A29), so the database itself refuses the app's
            // identity a removal of a row, even if its code has a bug (§5.8, §10.6). The app does delete working
            // copies, so NoteDraft has no deny (§5.1, Deletes). InitialCreate created the role.
            migrationBuilder.Sql("DENY DELETE ON [dbo].[Note] TO [grow2notes_runtime];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table would drop the deny with it; revoking it first keeps Down the mirror of Up, as
            // AddGoal's is. Production never runs Down (design.md §10.6).
            migrationBuilder.Sql("REVOKE DELETE ON [dbo].[Note] FROM [grow2notes_runtime];");

            migrationBuilder.DropTable(
                name: "NoteDraft");

            migrationBuilder.DropTable(
                name: "Note");
        }
    }
}
