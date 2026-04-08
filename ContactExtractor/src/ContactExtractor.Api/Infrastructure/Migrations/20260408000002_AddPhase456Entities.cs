using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContactExtractor.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase456Entities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 4: UserId on UploadSessions
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "UploadSessions",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            // Phase 5: New Contact fields
            migrationBuilder.AddColumn<bool>(
                name: "IsValidEmail",
                table: "Contacts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsValidPhone",
                table: "Contacts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DuplicateGroupId",
                table: "Contacts",
                type: "TEXT",
                nullable: true);

            // Phase 5: DuplicateGroups table
            migrationBuilder.CreateTable(
                name: "DuplicateGroups",
                columns: table => new
                {
                    Id         = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId     = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Similarity = table.Column<double>(type: "REAL", nullable: false),
                    Resolved   = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt  = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateGroups", x => x.Id);
                });

            // FK from Contacts to DuplicateGroups
            migrationBuilder.CreateIndex(
                name: "IX_Contacts_DuplicateGroupId",
                table: "Contacts",
                column: "DuplicateGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contacts_DuplicateGroups_DuplicateGroupId",
                table: "Contacts",
                column: "DuplicateGroupId",
                principalTable: "DuplicateGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Phase 5: Tags table
            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id        = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name      = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Color     = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    UserId    = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            // Phase 5: ContactTags join table (EF Core many-to-many)
            migrationBuilder.CreateTable(
                name: "ContactTags",
                columns: table => new
                {
                    ContactsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagsId     = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactTags", x => new { x.ContactsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ContactTags_Contacts_ContactsId",
                        column: x => x.ContactsId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContactTags_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactTags_TagsId",
                table: "ContactTags",
                column: "TagsId");

            // Phase 6: AuditLog table
            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id         = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId     = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Action     = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId   = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details    = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Timestamp  = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_UserId",
                table: "AuditLog",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Timestamp",
                table: "AuditLog",
                column: "Timestamp");

            // Phase 6: WebhookConfigs table
            migrationBuilder.CreateTable(
                name: "Webhooks",
                columns: table => new
                {
                    Id        = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId    = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Url       = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Event     = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Secret    = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsActive  = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webhooks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_UserId",
                table: "Webhooks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ContactTags");
            migrationBuilder.DropTable(name: "Tags");
            migrationBuilder.DropTable(name: "AuditLog");
            migrationBuilder.DropTable(name: "Webhooks");

            migrationBuilder.DropForeignKey(
                name: "FK_Contacts_DuplicateGroups_DuplicateGroupId",
                table: "Contacts");
            migrationBuilder.DropIndex(
                name: "IX_Contacts_DuplicateGroupId",
                table: "Contacts");
            migrationBuilder.DropTable(name: "DuplicateGroups");

            migrationBuilder.DropColumn(name: "IsValidEmail",    table: "Contacts");
            migrationBuilder.DropColumn(name: "IsValidPhone",    table: "Contacts");
            migrationBuilder.DropColumn(name: "DuplicateGroupId", table: "Contacts");
            migrationBuilder.DropColumn(name: "UserId",          table: "UploadSessions");
        }
    }
}
