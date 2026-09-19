using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Companias_UnidadesOrganizativas_US2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RelacionContratistaPrincipal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompaniaContratistaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompaniaPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaHoraInicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FechaHoraFin = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelacionContratistaPrincipal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RelacionContratistaPrincipal_Compania_CompaniaContratistaId",
                        column: x => x.CompaniaContratistaId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RelacionContratistaPrincipal_Compania_CompaniaPrincipalId",
                        column: x => x.CompaniaPrincipalId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnidadOrganizativa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnidadSuperiorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadOrganizativa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnidadOrganizativa_UnidadOrganizativa_UnidadSuperiorId",
                        column: x => x.UnidadSuperiorId,
                        principalTable: "UnidadOrganizativa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompaniaPrincipalUnidadOrganizativaRaiz",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompaniaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnidadOrganizativaRaizId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompaniaPrincipalUnidadOrganizativaRaiz", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompaniaPrincipalUnidadOrganizativaRaiz_Compania_CompaniaId",
                        column: x => x.CompaniaId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompaniaPrincipalUnidadOrganizativaRaiz_UnidadOrganizativa_UnidadOrganizativaRaizId",
                        column: x => x.UnidadOrganizativaRaizId,
                        principalTable: "UnidadOrganizativa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompaniaPrincipalUnidadOrganizativaRaiz_CompaniaId",
                table: "CompaniaPrincipalUnidadOrganizativaRaiz",
                column: "CompaniaId");

            migrationBuilder.CreateIndex(
                name: "UX_CompaniaPrincipalUnidadOrganizativaRaiz_Raiz",
                table: "CompaniaPrincipalUnidadOrganizativaRaiz",
                column: "UnidadOrganizativaRaizId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RelacionContratistaPrincipal_CompaniaPrincipalId",
                table: "RelacionContratistaPrincipal",
                column: "CompaniaPrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_RelacionContratistaPrincipal_Par_Inicio",
                table: "RelacionContratistaPrincipal",
                columns: new[] { "CompaniaContratistaId", "CompaniaPrincipalId", "FechaHoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_UnidadOrganizativa_UnidadSuperiorId",
                table: "UnidadOrganizativa",
                column: "UnidadSuperiorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompaniaPrincipalUnidadOrganizativaRaiz");

            migrationBuilder.DropTable(
                name: "RelacionContratistaPrincipal");

            migrationBuilder.DropTable(
                name: "UnidadOrganizativa");
        }
    }
}
