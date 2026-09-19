using EnterpriseAccessControl.Domain.Enums;

namespace EnterpriseAccessControl.Domain.Common;

/// <summary>
/// Raíz común de los catálogos maestros versionados (RF-030, RF-031, RF-032).
/// </summary>
/// <remarks>
/// Los cinco catálogos —tipo de documento, tipo de sangre, género, tipo de persona y tipo de
/// credencial— comparten exactamente la misma forma y el mismo ciclo de vida, así que comparten
/// también la raíz. Eso permite que un único caso de uso genérico los mantenga sin duplicar cinco
/// veces el mismo CRUD.
///
/// <see cref="Estado"/> nunca se usa para borrar: un valor pasa a <c>INACTIVO</c> y deja de estar
/// disponible para asignaciones nuevas, pero el histórico que ya lo referencia permanece intacto
/// (RF-032, "aplica hacia adelante").
///
/// <see cref="Nombre"/> no se declara <c>required</c> a diferencia de otras entidades: un miembro
/// requerido impide que el tipo satisfaga la restricción genérica <c>new()</c> (CS9040), de la que
/// depende el servicio genérico de mantenimiento. El valor siempre lo asigna el caso de uso.
/// </remarks>
public abstract class EntidadMaestra : EntidadBase
{
    public string Nombre { get; set; } = string.Empty;

    public Estado Estado { get; set; } = Estado.ACTIVO;
}
