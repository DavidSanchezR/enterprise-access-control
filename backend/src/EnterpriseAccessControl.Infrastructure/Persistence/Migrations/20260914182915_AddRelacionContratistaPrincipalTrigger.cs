using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Trigger de no-solapamiento de <c>RelacionContratistaPrincipal</c>, particionado por el par
    /// (Contratista, Principal) — research.md §5.
    /// </summary>
    /// <remarks>
    /// SQL Server no ofrece ninguna restricción declarativa de exclusión de rangos (el
    /// <c>EXCLUDE USING gist</c> de PostgreSQL no tiene equivalente), y un <c>CHECK</c> no puede
    /// consultar otras filas. El patrón idiomático equivalente es un trigger
    /// <c>AFTER INSERT, UPDATE</c> que aborta la transacción al detectar solapamiento dentro de la
    /// misma partición.
    ///
    /// Es la última línea de defensa frente a una condición de carrera, no el mecanismo primario: la
    /// capa de aplicación cierra la relación previa y devuelve 409 con ProblemDetails legible. En
    /// operación normal este trigger no debería dispararse nunca.
    ///
    /// Va en su propia migración —separada de la creación de las tablas— para que el objeto de base
    /// de datos que materializa el invariante temporal sea visible y revisable por sí solo.
    /// </remarks>
    public partial class AddRelacionContratistaPrincipalTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La partición es el par completo: una Contratista SÍ puede tener relaciones vigentes
            // simultáneas con Principales distintas (RF-051, CS-012); lo que se excluye es más de
            // una vigente con la MISMA Principal.
            //
            // ISNULL(..., '9999-12-31') representa "vigencia abierta" solo dentro de la comparación.
            // No es una fecha centinela almacenada: la columna guarda NULL, y RF-071 —que prohíbe
            // centinelas— aplica a las asociaciones vinculadas a una Persona, no a ésta.
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_RelacionContratistaPrincipal_NoSolapamiento
                ON [RelacionContratistaPrincipal]
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF EXISTS (
                        SELECT 1
                        FROM inserted i
                        JOIN [RelacionContratistaPrincipal] t
                            ON t.[CompaniaContratistaId] = i.[CompaniaContratistaId]
                           AND t.[CompaniaPrincipalId]  = i.[CompaniaPrincipalId]
                           AND t.[Id] <> i.[Id]
                           AND t.[FechaHoraInicio] < ISNULL(i.[FechaHoraFin], '9999-12-31')
                           AND i.[FechaHoraInicio] < ISNULL(t.[FechaHoraFin], '9999-12-31')
                    )
                    BEGIN
                        THROW 50001, 'Solapamiento de vigencia detectado para el mismo par Contratista-Principal.', 1;
                    END
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_RelacionContratistaPrincipal_NoSolapamiento;");
        }
    }
}
