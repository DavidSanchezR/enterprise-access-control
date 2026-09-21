using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Modelo RBAC de roles administrativos y zona horaria por Compañía Principal
    /// (Sesión 2026-09-20, cierre de Etapa 1; RF-074, RF-075, RF-080).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Migración de esquema, sin migración de datos.</b> <c>AlcanceUsuarioCompania</c> se elimina
    /// directamente y sus filas no se transforman ni se conservan: el proyecto es greenfield, sin
    /// datos de producción ni de desarrollo que deban preservarse. Los datos administrativos
    /// necesarios los regenera la rutina de arranque de RF-078. El aviso de posible pérdida de datos
    /// que emite <c>ef migrations add</c> es, por tanto, esperado y aceptado.
    /// </para>
    /// <para>
    /// El <c>CHECK</c> impone la regla fundamental de RF-074 en la base de datos, como tercera línea
    /// de defensa tras el invariante de dominio y la validación de aplicación. El índice clúster
    /// sobre <c>CreatedAt</c> sigue research.md §20: es una entidad de histórico que solo acumula
    /// filas, y clusterizar por una PK GUID fragmentaría las páginas.
    /// </para>
    /// </remarks>
    public partial class RolesAdministrativos_ZonaHoraria_Etapa1 : Migration
    {
        /// <summary>
        /// Impide dos asignaciones COMPANY_ADMINISTRATOR solapadas del mismo par
        /// (usuario, compañía) — RF-075.
        /// </summary>
        /// <remarks>
        /// Misma plantilla que los triggers de research.md §5, con dos diferencias propias de esta
        /// entidad: la partición incluye <c>CompaniaId</c> —un usuario sí puede administrar varias
        /// compañías a la vez— y el filtro excluye <c>GLOBAL_ADMINISTRATOR</c>, exento de esta
        /// partición porque su alcance no se enumera y varios pueden coexistir.
        ///
        /// Es la última línea de defensa ante una condición de carrera, no el mecanismo primario: la
        /// capa de aplicación valida antes de escribir y devuelve 409 legible.
        /// </remarks>
        private const string TriggerNoSolapamiento = """
            CREATE TRIGGER trg_AsignacionRolAdministrativo_NoSolapamiento
            ON [AsignacionRolAdministrativo]
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    JOIN [AsignacionRolAdministrativo] t
                        ON t.[UsuarioId] = i.[UsuarioId]
                       AND t.[CompaniaId] = i.[CompaniaId]
                       AND t.[Id] <> i.[Id]
                       AND t.[FechaHoraInicio] < i.[FechaHoraFin]
                       AND i.[FechaHoraInicio] < t.[FechaHoraFin]
                       AND t.[Rol] = 'COMPANY_ADMINISTRATOR'
                       AND i.[Rol] = 'COMPANY_ADMINISTRATOR'
                )
                BEGIN
                    THROW 50001, 'Solapamiento de vigencia detectado para la misma partición.', 1;
                END
            END;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlcanceUsuarioCompania");

            migrationBuilder.AddColumn<string>(
                name: "ZonaHorariaIana",
                table: "Compania",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AsignacionRolAdministrativo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompaniaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AsignacionRolAdministrativo", x => x.Id)
                        .Annotation("SqlServer:Clustered", false);
                    table.CheckConstraint("CK_AsignacionRolAdministrativo_RolCompania", "([Rol] = 'GLOBAL_ADMINISTRATOR' AND [CompaniaId] IS NULL) OR ([Rol] = 'COMPANY_ADMINISTRATOR' AND [CompaniaId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AsignacionRolAdministrativo_Compania_CompaniaId",
                        column: x => x.CompaniaId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AsignacionRolAdministrativo_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionRolAdministrativo_Compania_FechaHoraFin",
                table: "AsignacionRolAdministrativo",
                columns: new[] { "CompaniaId", "FechaHoraFin" });

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionRolAdministrativo_CreatedAt_Clustered",
                table: "AsignacionRolAdministrativo",
                column: "CreatedAt")
                .Annotation("SqlServer:Clustered", true);

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionRolAdministrativo_Usuario_FechaHoraFin",
                table: "AsignacionRolAdministrativo",
                columns: new[] { "UsuarioId", "FechaHoraFin" });

            migrationBuilder.Sql(TriggerNoSolapamiento);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_AsignacionRolAdministrativo_NoSolapamiento;");

            migrationBuilder.DropTable(
                name: "AsignacionRolAdministrativo");

            migrationBuilder.DropColumn(
                name: "ZonaHorariaIana",
                table: "Compania");

            migrationBuilder.CreateTable(
                name: "AlcanceUsuarioCompania",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompaniaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlcanceUsuarioCompania", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlcanceUsuarioCompania_Compania_CompaniaId",
                        column: x => x.CompaniaId,
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AlcanceUsuarioCompania_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlcanceUsuarioCompania_CompaniaId",
                table: "AlcanceUsuarioCompania",
                column: "CompaniaId");

            migrationBuilder.CreateIndex(
                name: "UX_AlcanceUsuarioCompania_Usuario_Compania",
                table: "AlcanceUsuarioCompania",
                columns: new[] { "UsuarioId", "CompaniaId" },
                unique: true);
        }
    }
}
