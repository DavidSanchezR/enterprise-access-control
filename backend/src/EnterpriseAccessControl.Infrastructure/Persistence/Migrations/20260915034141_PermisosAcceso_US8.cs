using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PermisosAcceso_US8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermisoAcceso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AreaAccesoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Alcance = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PersonaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnidadOrganizativaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompaniaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FechaHoraInicioVigencia = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FechaHoraFinVigencia = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermisoAcceso", x => x.Id);
                    table.CheckConstraint("CK_PermisoAcceso_SujetoSegunAlcance", "(Alcance = 'PERSONA'\n     AND PersonaId IS NOT NULL\n     AND UnidadOrganizativaId IS NULL\n     AND CompaniaId IS NULL)\nOR (Alcance = 'UNIDAD_ORGANIZATIVA'\n     AND PersonaId IS NULL\n     AND UnidadOrganizativaId IS NOT NULL\n     AND CompaniaId IS NULL)\nOR (Alcance = 'COMPANIA'\n     AND PersonaId IS NULL\n     AND UnidadOrganizativaId IS NULL\n     AND CompaniaId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PermisoAcceso_AreaAcceso_AreaAccesoId",
                        column: x => x.AreaAccesoId,
                        principalTable: "AreaAcceso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermisoAcceso_Compania_CompaniaId",
                        column: x => x.CompaniaId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermisoAcceso_Persona_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Persona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PermisoAcceso_UnidadOrganizativa_UnidadOrganizativaId",
                        column: x => x.UnidadOrganizativaId,
                        principalTable: "UnidadOrganizativa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloqueHorarioPermiso",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermisoAccesoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiaSemana = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HoraInicio = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    HoraFin = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloqueHorarioPermiso", x => x.Id);
                    table.CheckConstraint("CK_BloqueHorarioPermiso_HoraFinPosterior", "HoraFin > HoraInicio");
                    table.ForeignKey(
                        name: "FK_BloqueHorarioPermiso_PermisoAcceso_PermisoAccesoId",
                        column: x => x.PermisoAccesoId,
                        principalTable: "PermisoAcceso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BloqueHorarioPermiso_Permiso_Dia",
                table: "BloqueHorarioPermiso",
                columns: new[] { "PermisoAccesoId", "DiaSemana" });

            migrationBuilder.CreateIndex(
                name: "IX_PermisoAcceso_Area_Alcance",
                table: "PermisoAcceso",
                columns: new[] { "AreaAccesoId", "Alcance" });

            migrationBuilder.CreateIndex(
                name: "IX_PermisoAcceso_CompaniaId",
                table: "PermisoAcceso",
                column: "CompaniaId",
                filter: "CompaniaId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PermisoAcceso_PersonaId",
                table: "PermisoAcceso",
                column: "PersonaId",
                filter: "PersonaId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PermisoAcceso_UnidadOrganizativaId",
                table: "PermisoAcceso",
                column: "UnidadOrganizativaId",
                filter: "UnidadOrganizativaId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BloqueHorarioPermiso");

            migrationBuilder.DropTable(
                name: "PermisoAcceso");
        }
    }
}
