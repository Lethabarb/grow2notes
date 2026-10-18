using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grow2Notes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoteVersion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsFlagged = table.Column<bool>(type: "bit", nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteVersion", x => x.Id);
                    table.UniqueConstraint("AK_NoteVersion_OrganisationId_Id", x => new { x.OrganisationId, x.Id });
                    table.ForeignKey(
                        name: "FK_NoteVersion_AspNetUsers_OrganisationId_CreatedByUserId",
                        columns: x => new { x.OrganisationId, x.CreatedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteVersion_Note_OrganisationId_NoteId",
                        columns: x => new { x.OrganisationId, x.NoteId },
                        principalTable: "Note",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteVersionItem",
                columns: table => new
                {
                    NoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Section = table.Column<byte>(type: "tinyint", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CommonItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GroupPosition = table.Column<int>(type: "int", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsTicked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteVersionItem", x => new { x.NoteVersionId, x.Section, x.Position });
                    table.CheckConstraint("CK_NoteVersionItem_Section",
                        "([Section] = 1 AND [GoalId] IS NOT NULL AND [CommonItemId] IS NULL"
                        + " AND [GroupPosition] IS NULL)"
                        + " OR ([Section] = 2 AND [CommonItemId] IS NOT NULL AND [GoalId] IS NULL"
                        + " AND [GroupPosition] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_NoteVersionItem_CommonItem_OrganisationId_CommonItemId",
                        columns: x => new { x.OrganisationId, x.CommonItemId },
                        principalTable: "CommonItem",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteVersionItem_Goal_OrganisationId_GoalId",
                        columns: x => new { x.OrganisationId, x.GoalId },
                        principalTable: "Goal",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteVersionItem_NoteVersion_OrganisationId_NoteVersionId",
                        columns: x => new { x.OrganisationId, x.NoteVersionId },
                        principalTable: "NoteVersion",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersion_NoteId_VersionNumber",
                table: "NoteVersion",
                columns: new[] { "NoteId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersion_OrganisationId_CreatedByUserId",
                table: "NoteVersion",
                columns: new[] { "OrganisationId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersion_OrganisationId_IdempotencyKey",
                table: "NoteVersion",
                columns: new[] { "OrganisationId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersion_OrganisationId_NoteId",
                table: "NoteVersion",
                columns: new[] { "OrganisationId", "NoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersionItem_OrganisationId_CommonItemId",
                table: "NoteVersionItem",
                columns: new[] { "OrganisationId", "CommonItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersionItem_OrganisationId_GoalId",
                table: "NoteVersionItem",
                columns: new[] { "OrganisationId", "GoalId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersionItem_OrganisationId_NoteVersionId",
                table: "NoteVersionItem",
                columns: new[] { "OrganisationId", "NoteVersionId" });

            // A note's history is append-only, so the database itself refuses the app's identity a change or removal
            // of a version or its rows, even if its code has a bug (design.md §5.8, §10.6). InitialCreate created the
            // role.
            migrationBuilder.Sql("DENY UPDATE, DELETE ON [dbo].[NoteVersion] TO [grow2notes_runtime];");
            migrationBuilder.Sql("DENY UPDATE, DELETE ON [dbo].[NoteVersionItem] TO [grow2notes_runtime];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the tables would drop the denies with them; revoking them first keeps Down the mirror of Up, as
            // AddNote's is. Production never runs Down (design.md §10.6).
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON [dbo].[NoteVersionItem] FROM [grow2notes_runtime];");
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON [dbo].[NoteVersion] FROM [grow2notes_runtime];");

            migrationBuilder.DropTable(
                name: "NoteVersionItem");

            migrationBuilder.DropTable(
                name: "NoteVersion");
        }
    }
}
