using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AreasAcceso_US6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AreaAcceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AreaSuperiorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompaniaPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaAcceso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AreaAcceso_AreaAcceso_AreaSuperiorId",
                        column: x => x.AreaSuperiorId,
                        principalTable: "AreaAcceso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AreaAcceso_Compania_CompaniaPrincipalId",
                        column: x => x.CompaniaPrincipalId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AreaAcceso_AreaSuperiorId",
                table: "AreaAcceso",
                column: "AreaSuperiorId");

            migrationBuilder.CreateIndex(
                name: "IX_AreaAcceso_Principal_AreaSuperior",
                table: "AreaAcceso",
                columns: new[] { "CompaniaPrincipalId", "AreaSuperiorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AreaAcceso");
        }
    }
}
