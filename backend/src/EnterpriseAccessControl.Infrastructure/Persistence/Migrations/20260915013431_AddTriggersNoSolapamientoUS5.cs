using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Triggers de no-solapamiento de las asociaciones temporales de persona (research.md §5).
    /// </summary>
    /// <remarks>
    /// Cada entidad se particiona por una clave distinta, y esa elección es la que hace posible la
    /// simultaneidad multi-Principal que exige el negocio:
    /// <list type="bullet">
    ///   <item><c>AsignaciónPersonaCompañía</c> → <c>PersonaId</c>: una sola pertenencia activa por
    ///   persona (RF-014).</item>
    ///   <item><c>ContextoOperativoPersonaPrincipal</c> → <c>(PersonaId, CompañíaPrincipalId)</c>:
    ///   un contexto por Principal, pero varios Principales a la vez (RF-052, CS-030).</item>
    ///   <item><c>AsignaciónPersonaUnidadOrganizativa</c> → <c>ContextoOperativoId</c>: una unidad
    ///   por contexto, no una por persona (RF-055, CS-014).</item>
    ///   <item><c>AsignaciónCredencial</c> → <c>(PersonaId, CompañíaPrincipalId)</c> y solo entre
    ///   filas <c>ASIGNADO</c>: una credencial activa por Principal, simultáneas entre Principales
    ///   distintas (RF-057, CS-016).</item>
    /// </list>
    ///
    /// Son la última línea de defensa ante una condición de carrera, no el mecanismo primario: la
    /// capa de aplicación valida antes de escribir y devuelve 409 legible — cerrando la asociación
    /// previa en pertenencia, contexto y unidad organizativa, y rechazando sin tocar la previa en
    /// credenciales (Sesión 2026-09-15, decisión A). En operación normal no deberían dispararse.
    /// </remarks>
    public partial class AddTriggersNoSolapamientoUS5 : Migration
    {
        /// <summary>
        /// Plantilla común: aborta si la fila entrante se cruza con otra de la misma partición.
        /// </summary>
        /// <remarks>
        /// Todas las fechas de fin son NOT NULL (RF-071), así que —a diferencia del trigger de
        /// <c>RelacionContratistaPrincipal</c>— aquí no hace falta la rama <c>ISNULL</c> para
        /// representar vigencia abierta: esa vigencia ya no existe en estas entidades.
        /// </remarks>
        private static string Trigger(string nombre, string tabla, string particion, string filtro = "")
            => $"""
                CREATE TRIGGER {nombre}
                ON [{tabla}]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [{tabla}] t
                            ON {particion}
                           AND t.[Id] <> i.[Id]
                           AND t.[FechaHoraInicio] < i.[FechaHoraFin]
                           AND i.[FechaHoraInicio] < t.[FechaHoraFin]
                           {filtro}
                    )
                    BEGIN
                        THROW 50001, 'Solapamiento de vigencia detectado para la misma partición.', 1;
                    END
                END;
                """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Trigger(
                "trg_AsignacionPersonaCompania_NoSolapamiento",
                "AsignacionPersonaCompania",
                "t.[PersonaId] = i.[PersonaId]"));

            migrationBuilder.Sql(Trigger(
                "trg_ContextoOperativoPersonaPrincipal_NoSolapamiento",
                "ContextoOperativoPersonaPrincipal",
                "t.[PersonaId] = i.[PersonaId] AND t.[CompaniaPrincipalId] = i.[CompaniaPrincipalId]"));

            migrationBuilder.Sql(Trigger(
                "trg_AsignacionPersonaUnidadOrganizativa_NoSolapamiento",
                "AsignacionPersonaUnidadOrganizativa",
                "t.[ContextoOperativoId] = i.[ContextoOperativoId]"));

            // Solo entre credenciales ASIGNADO: una DEVUELTA o REVOCADA no reserva la ventana.
            migrationBuilder.Sql(Trigger(
                "trg_AsignacionCredencial_NoSolapamiento",
                "AsignacionCredencial",
                "t.[PersonaId] = i.[PersonaId] AND t.[CompaniaPrincipalId] = i.[CompaniaPrincipalId]",
                "AND t.[Estado] = 'ASIGNADO' AND i.[Estado] = 'ASIGNADO'"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var nombre in new[]
                     {
                         "trg_AsignacionCredencial_NoSolapamiento",
                         "trg_AsignacionPersonaUnidadOrganizativa_NoSolapamiento",
                         "trg_ContextoOperativoPersonaPrincipal_NoSolapamiento",
                         "trg_AsignacionPersonaCompania_NoSolapamiento",
                     })
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {nombre};");
            }
        }
    }
}
