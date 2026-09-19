using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AreaAccesoTipoPersona_US7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AreaAccesoTipoPersona",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaAccesoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoPersonaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AreaAccesoTipoPersona", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AreaAccesoTipoPersona_AreaAcceso_AreaAccesoId",
                        column: x => x.AreaAccesoId,
                        principalTable: "AreaAcceso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AreaAccesoTipoPersona_TipoPersona_TipoPersonaId",
                        column: x => x.TipoPersonaId,
                        principalTable: "TipoPersona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AreaAccesoTipoPersona_TipoPersonaId",
                table: "AreaAccesoTipoPersona",
                column: "TipoPersonaId");

            migrationBuilder.CreateIndex(
                name: "UX_AreaAccesoTipoPersona_Area_TipoPersona",
                table: "AreaAccesoTipoPersona",
                columns: new[] { "AreaAccesoId", "TipoPersonaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AreaAccesoTipoPersona");
        }
    }
}
