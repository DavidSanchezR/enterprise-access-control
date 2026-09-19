using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Historicos_US5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "AsignacionPersonaCompania",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MotivoFin",
                table: "AsignacionPersonaCompania",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AsignacionCredencial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompaniaPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoCredencialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaHoraInicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FechaHoraFin = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RevocadoPorPertenenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionCredencial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionCredencial_AsignacionPersonaCompania_RevocadoPorPertenenciaId",
                        column: x => x.RevocadoPorPertenenciaId,
                        principalTable: "AsignacionPersonaCompania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionCredencial_Compania_CompaniaPrincipalId",
                        column: x => x.CompaniaPrincipalId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionCredencial_Persona_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Persona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionCredencial_TipoCredencial_TipoCredencialId",
                        column: x => x.TipoCredencialId,
                        principalTable: "TipoCredencial",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionTipoPersona",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoPersonaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaHoraInicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FechaHoraFin = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionTipoPersona", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionTipoPersona_Persona_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Persona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionTipoPersona_TipoPersona_TipoPersonaId",
                        column: x => x.TipoPersonaId,
                        principalTable: "TipoPersona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContextoOperativoPersonaPrincipal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompaniaPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PersonaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaHoraInicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FechaHoraFin = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MotivoFin = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RevocadoPorPertenenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextoOperativoPersonaPrincipal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContextoOperativoPersonaPrincipal_AsignacionPersonaCompania_RevocadoPorPertenenciaId",
                        column: x => x.RevocadoPorPertenenciaId,
                        principalTable: "AsignacionPersonaCompania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContextoOperativoPersonaPrincipal_Compania_CompaniaPrincipalId",
                        column: x => x.CompaniaPrincipalId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContextoOperativoPersonaPrincipal_Persona_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Persona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionPersonaUnidadOrganizativa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContextoOperativoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnidadOrganizativaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PersonaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaHoraInicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FechaHoraFin = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MotivoFin = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RevocadoPorPertenenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionPersonaUnidadOrganizativa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionPersonaUnidadOrganizativa_AsignacionPersonaCompania_RevocadoPorPertenenciaId",
                        column: x => x.RevocadoPorPertenenciaId,
                        principalTable: "AsignacionPersonaCompania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionPersonaUnidadOrganizativa_ContextoOperativoPersonaPrincipal_ContextoOperativoId",
                        column: x => x.ContextoOperativoId,
                        principalTable: "ContextoOperativoPersonaPrincipal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionPersonaUnidadOrganizativa_Persona_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Persona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionPersonaUnidadOrganizativa_UnidadOrganizativa_UnidadOrganizativaId",
                        column: x => x.UnidadOrganizativaId,
                        principalTable: "UnidadOrganizativa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionCredencial_CompaniaPrincipalId",
                table: "AsignacionCredencial",
                column: "CompaniaPrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionCredencial_Persona_Principal_Estado",
                table: "AsignacionCredencial",
                columns: new[] { "PersonaId", "CompaniaPrincipalId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionCredencial_RevocadoPorPertenenciaId",
                table: "AsignacionCredencial",
                column: "RevocadoPorPertenenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionCredencial_TipoCredencialId",
                table: "AsignacionCredencial",
                column: "TipoCredencialId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionPersonaUnidadOrganizativa_RevocadoPorPertenenciaId",
                table: "AsignacionPersonaUnidadOrganizativa",
                column: "RevocadoPorPertenenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionPersonaUnidadOrganizativa_UnidadOrganizativaId",
                table: "AsignacionPersonaUnidadOrganizativa",
                column: "UnidadOrganizativaId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionUO_Contexto_Inicio",
                table: "AsignacionPersonaUnidadOrganizativa",
                columns: new[] { "ContextoOperativoId", "FechaHoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionUO_PersonaId",
                table: "AsignacionPersonaUnidadOrganizativa",
                column: "PersonaId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionTipoPersona_Persona_Inicio",
                table: "AsignacionTipoPersona",
                columns: new[] { "PersonaId", "FechaHoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionTipoPersona_TipoPersonaId",
                table: "AsignacionTipoPersona",
                column: "TipoPersonaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContextoOperativo_Persona_Principal_Inicio",
                table: "ContextoOperativoPersonaPrincipal",
                columns: new[] { "PersonaId", "CompaniaPrincipalId", "FechaHoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_ContextoOperativo_RevocadoPorPertenenciaId",
                table: "ContextoOperativoPersonaPrincipal",
                column: "RevocadoPorPertenenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_ContextoOperativoPersonaPrincipal_CompaniaPrincipalId",
                table: "ContextoOperativoPersonaPrincipal",
                column: "CompaniaPrincipalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionCredencial");

            migrationBuilder.DropTable(
                name: "AsignacionPersonaUnidadOrganizativa");

            migrationBuilder.DropTable(
                name: "AsignacionTipoPersona");

            migrationBuilder.DropTable(
                name: "ContextoOperativoPersonaPrincipal");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "AsignacionPersonaCompania");

            migrationBuilder.DropColumn(
                name: "MotivoFin",
                table: "AsignacionPersonaCompania");
        }
    }
}
