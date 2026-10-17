using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grow2Notes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommonItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommonItemGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsEveryNote = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommonItemGroup", x => x.Id);
                    table.UniqueConstraint("AK_CommonItemGroup_OrganisationId_Id", x => new { x.OrganisationId, x.Id });
                    table.CheckConstraint(
                        "CK_CommonItemGroup_EveryNoteActive", "[IsEveryNote] = 0 OR [ArchivedAtUtc] IS NULL");
                    table.ForeignKey(
                        name: "FK_CommonItemGroup_Organisation_OrganisationId",
                        column: x => x.OrganisationId,
                        principalTable: "Organisation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommonItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommonItem", x => x.Id);
                    table.UniqueConstraint("AK_CommonItem_OrganisationId_Id", x => new { x.OrganisationId, x.Id });
                    table.ForeignKey(
                        name: "FK_CommonItem_CommonItemGroup_OrganisationId_GroupId",
                        columns: x => new { x.OrganisationId, x.GroupId },
                        principalTable: "CommonItemGroup",
                        principalColumns: new[] { "OrganisationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommonItem_OrganisationId_GroupId_ArchivedAtUtc_SortOrder",
                table: "CommonItem",
                columns: new[] { "OrganisationId", "GroupId", "ArchivedAtUtc", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CommonItemGroup_OrganisationId",
                table: "CommonItemGroup",
                column: "OrganisationId",
                unique: true,
                filter: "[IsEveryNote] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CommonItemGroup_OrganisationId_ArchivedAtUtc_SortOrder",
                table: "CommonItemGroup",
                columns: new[] { "OrganisationId", "ArchivedAtUtc", "SortOrder" });

            // Each organisation already here, such as the one bootstrapped in test, gets the Every note group that
            // admin bootstrap adds with each new one (design.md §5.3, D82), so every organisation has exactly one.
            // The name is a literal, as a migration keeps what it shipped with. The ID is NEWID(), as SQL Server makes
            // a sequential one only as a column default, which matters little for one row per organisation.
            migrationBuilder.Sql("""
                INSERT INTO [dbo].[CommonItemGroup] ([Id], [OrganisationId], [Name], [SortOrder], [IsEveryNote],
                                                     [CreatedAtUtc])
                SELECT NEWID(), [Id], N'Every note', 0, 1, SYSUTCDATETIME()
                FROM [dbo].[Organisation];
                """);

            // Groups and items are archived, never deleted (design.md §3.7, A29), so the database itself refuses the
            // app's identity a removal of a row, even if its code has a bug (§5.8, §10.6). InitialCreate created the
            // role.
            migrationBuilder.Sql("DENY DELETE ON [dbo].[CommonItemGroup] TO [grow2notes_runtime];");
            migrationBuilder.Sql("DENY DELETE ON [dbo].[CommonItem] TO [grow2notes_runtime];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the tables would drop the denies with them; revoking them first keeps Down the mirror of Up, as
            // AddGoal's is. Production never runs Down (design.md §10.6).
            migrationBuilder.Sql("REVOKE DELETE ON [dbo].[CommonItem] FROM [grow2notes_runtime];");
            migrationBuilder.Sql("REVOKE DELETE ON [dbo].[CommonItemGroup] FROM [grow2notes_runtime];");

            migrationBuilder.DropTable(
                name: "CommonItem");

            migrationBuilder.DropTable(
                name: "CommonItemGroup");
        }
    }
}
