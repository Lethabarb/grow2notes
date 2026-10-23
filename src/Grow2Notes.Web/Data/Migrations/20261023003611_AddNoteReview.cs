using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grow2Notes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NoteReview",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteReview", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteReview_AspNetUsers_OrganisationId_ReviewedByUserId",
                        columns: x => new { x.OrganisationId, x.ReviewedByUserId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NoteReview_Note_OrganisationId_NoteId",
                        columns: x => new { x.OrganisationId, x.NoteId },
                        principalTable: "Note",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NoteReview_NoteId_ReviewedAtUtc",
                table: "NoteReview",
                columns: new[] { "NoteId", "ReviewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteReview_OrganisationId_NoteId",
                table: "NoteReview",
                columns: new[] { "OrganisationId", "NoteId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteReview_OrganisationId_ReviewedAtUtc",
                table: "NoteReview",
                columns: new[] { "OrganisationId", "ReviewedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteReview_OrganisationId_ReviewedByUserId",
                table: "NoteReview",
                columns: new[] { "OrganisationId", "ReviewedByUserId" });

            // A note's reviews are append-only, so the database itself refuses the app's identity a change or removal
            // of one, even if its code has a bug (design.md §5.3, §10.6). InitialCreate created the role.
            migrationBuilder.Sql("DENY UPDATE, DELETE ON [dbo].[NoteReview] TO [grow2notes_runtime];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the table would drop the deny with it; revoking it first keeps Down the mirror of Up, as
            // AddNoteVersion's is. Production never runs Down (design.md §10.6).
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON [dbo].[NoteReview] FROM [grow2notes_runtime];");

            migrationBuilder.DropTable(
                name: "NoteReview");
        }
    }
}
