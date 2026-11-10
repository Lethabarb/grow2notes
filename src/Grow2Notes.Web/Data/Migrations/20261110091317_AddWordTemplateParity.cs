using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grow2Notes.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWordTemplateParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasMedicationLine",
                table: "Participant",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Detail",
                table: "NoteVersionItem",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMediumOrLongTerm",
                table: "NoteVersionItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsDetail",
                table: "NoteVersionItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLateNotice",
                table: "NoteVersion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsNotIn",
                table: "NoteVersion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSick",
                table: "NoteVersion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Medication",
                table: "NoteVersion",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLateNotice",
                table: "NoteDraft",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsNotIn",
                table: "NoteDraft",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSick",
                table: "NoteDraft",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Medication",
                table: "NoteDraft",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMediumOrLongTerm",
                table: "Goal",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NeedsDetail",
                table: "CommonItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NoteVersionItem_Detail",
                table: "NoteVersionItem",
                sql: "([Section] = 2 OR [NeedsDetail] = 0)"
                    + " AND ([Detail] IS NULL OR ([Section] = 2 AND [NeedsDetail] = 1 AND [IsTicked] = 1))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NoteVersionItem_Term",
                table: "NoteVersionItem",
                sql: "[Section] = 1 OR [IsMediumOrLongTerm] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NoteVersionItem_Detail",
                table: "NoteVersionItem");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NoteVersionItem_Term",
                table: "NoteVersionItem");

            migrationBuilder.DropColumn(
                name: "HasMedicationLine",
                table: "Participant");

            migrationBuilder.DropColumn(
                name: "Detail",
                table: "NoteVersionItem");

            migrationBuilder.DropColumn(
                name: "IsMediumOrLongTerm",
                table: "NoteVersionItem");

            migrationBuilder.DropColumn(
                name: "NeedsDetail",
                table: "NoteVersionItem");

            migrationBuilder.DropColumn(
                name: "IsLateNotice",
                table: "NoteVersion");

            migrationBuilder.DropColumn(
                name: "IsNotIn",
                table: "NoteVersion");

            migrationBuilder.DropColumn(
                name: "IsSick",
                table: "NoteVersion");

            migrationBuilder.DropColumn(
                name: "Medication",
                table: "NoteVersion");

            migrationBuilder.DropColumn(
                name: "IsLateNotice",
                table: "NoteDraft");

            migrationBuilder.DropColumn(
                name: "IsNotIn",
                table: "NoteDraft");

            migrationBuilder.DropColumn(
                name: "IsSick",
                table: "NoteDraft");

            migrationBuilder.DropColumn(
                name: "Medication",
                table: "NoteDraft");

            migrationBuilder.DropColumn(
                name: "IsMediumOrLongTerm",
                table: "Goal");

            migrationBuilder.DropColumn(
                name: "NeedsDetail",
                table: "CommonItem");
        }
    }
}
