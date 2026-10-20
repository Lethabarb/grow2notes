using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grow2Notes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteVersionGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NoteVersionItem_OrganisationId_NoteVersionId",
                table: "NoteVersionItem");

            migrationBuilder.CreateTable(
                name: "NoteVersionGroup",
                columns: table => new
                {
                    NoteVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommonItemGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsEveryNote = table.Column<bool>(type: "bit", nullable: false),
                    IsPicked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteVersionGroup", x => new { x.NoteVersionId, x.Position });
                    table.UniqueConstraint(
                        "AK_NoteVersionGroup_OrganisationId_NoteVersionId_Position",
                        x => new { x.OrganisationId, x.NoteVersionId, x.Position });
                    table.CheckConstraint(
                        "CK_NoteVersionGroup_EveryNoteNotPicked",
                        "[IsEveryNote] = 0 OR [IsPicked] = 0");
                    table.ForeignKey(
                        name: "FK_NoteVersionGroup_CommonItemGroup_OrganisationId_CommonItemGroupId",
                        columns: x => new { x.OrganisationId, x.CommonItemGroupId },
                        principalTable: "CommonItemGroup",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteVersionGroup_NoteVersion_OrganisationId_NoteVersionId",
                        columns: x => new { x.OrganisationId, x.NoteVersionId },
                        principalTable: "NoteVersion",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersionItem_OrganisationId_NoteVersionId_GroupPosition",
                table: "NoteVersionItem",
                columns: new[] { "OrganisationId", "NoteVersionId", "GroupPosition" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersionGroup_OrganisationId_CommonItemGroupId",
                table: "NoteVersionGroup",
                columns: new[] { "OrganisationId", "CommonItemGroupId" });

            migrationBuilder.AddForeignKey(
                name: "FK_NoteVersionItem_NoteVersionGroup_OrganisationId_NoteVersionId_GroupPosition",
                table: "NoteVersionItem",
                columns: new[] { "OrganisationId", "NoteVersionId", "GroupPosition" },
                principalTable: "NoteVersionGroup",
                principalColumns: new[] { "OrganisationId", "NoteVersionId", "Position" },
                onDelete: ReferentialAction.Restrict);

            // A note's history is append-only, so the database itself refuses the app's identity a change or removal
            // of a version's groups, even if its code has a bug (design.md §5.8, §10.6). InitialCreate created the
            // role.
            migrationBuilder.Sql("DENY UPDATE, DELETE ON [dbo].[NoteVersionGroup] TO [grow2notes_runtime];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table would drop the deny with it; revoking it first keeps Down the mirror of Up, as
            // AddNoteVersion's is. Production never runs Down (design.md §10.6).
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON [dbo].[NoteVersionGroup] FROM [grow2notes_runtime];");

            migrationBuilder.DropForeignKey(
                name: "FK_NoteVersionItem_NoteVersionGroup_OrganisationId_NoteVersionId_GroupPosition",
                table: "NoteVersionItem");

            migrationBuilder.DropTable(
                name: "NoteVersionGroup");

            migrationBuilder.DropIndex(
                name: "IX_NoteVersionItem_OrganisationId_NoteVersionId_GroupPosition",
                table: "NoteVersionItem");

            migrationBuilder.CreateIndex(
                name: "IX_NoteVersionItem_OrganisationId_NoteVersionId",
                table: "NoteVersionItem",
                columns: new[] { "OrganisationId", "NoteVersionId" });
        }
    }
}
