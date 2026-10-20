using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeachingMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeachingMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                              .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TopicId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeachingMaterials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeachingMaterials_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Adaugă index pe UserId și TopicId (opțional, dar recomandat)
            migrationBuilder.CreateIndex(
                name: "IX_TeachingMaterials_UserId",
                table: "TeachingMaterials",
                column: "UserId");
            migrationBuilder.CreateIndex(
                name: "IX_TeachingMaterials_TopicId",
                table: "TeachingMaterials",
                column: "TopicId");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeachingMaterials");

            migrationBuilder.CreateTable(
                name: "UserTopic",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TopicId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTopic", x => new { x.UserId, x.TopicId });
                    table.ForeignKey(
                        name: "FK_UserTopic_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTopic_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTopic_TopicId",
                table: "UserTopic",
                column: "TopicId");
        }
    }
}
