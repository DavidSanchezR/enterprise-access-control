using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Personas_US4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Persona",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombres = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Apellidos = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FechaNacimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoDocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GeneroId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorreoElectronico = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TipoSangreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactoEmergencia = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NumeroEmergencia = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persona", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Persona_Genero_GeneroId",
                        column: x => x.GeneroId,
                        principalTable: "Genero",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Persona_TipoDocumento_TipoDocumentoId",
                        column: x => x.TipoDocumentoId,
                        principalTable: "TipoDocumento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Persona_TipoSangre_TipoSangreId",
                        column: x => x.TipoSangreId,
                        principalTable: "TipoSangre",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AsignacionPersonaCompania",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompaniaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaHoraInicio = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    FechaHoraFin = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionPersonaCompania", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionPersonaCompania_Compania_CompaniaId",
                        column: x => x.CompaniaId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionPersonaCompania_Persona_PersonaId",
                        column: x => x.PersonaId,
                        principalTable: "Persona",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionPersonaCompania_CompaniaId",
                table: "AsignacionPersonaCompania",
                column: "CompaniaId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionPersonaCompania_Persona_Vigencia",
                table: "AsignacionPersonaCompania",
                columns: new[] { "PersonaId", "FechaHoraInicio", "FechaHoraFin" });

            migrationBuilder.CreateIndex(
                name: "IX_Persona_Apellidos_Nombres",
                table: "Persona",
                columns: new[] { "Apellidos", "Nombres" });

            migrationBuilder.CreateIndex(
                name: "IX_Persona_GeneroId",
                table: "Persona",
                column: "GeneroId");

            migrationBuilder.CreateIndex(
                name: "IX_Persona_NumeroDocumento",
                table: "Persona",
                column: "NumeroDocumento");

            migrationBuilder.CreateIndex(
                name: "IX_Persona_TipoSangreId",
                table: "Persona",
                column: "TipoSangreId");

            migrationBuilder.CreateIndex(
                name: "UX_Persona_TipoDocumento_NumeroDocumento",
                table: "Persona",
                columns: new[] { "TipoDocumentoId", "NumeroDocumento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsignacionPersonaCompania");

            migrationBuilder.DropTable(
                name: "Persona");
        }
    }
}
