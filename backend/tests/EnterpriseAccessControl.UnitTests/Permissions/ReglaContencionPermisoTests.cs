using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Enums;
using FluentAssertions;

namespace EnterpriseAccessControl.UnitTests.Permissions;

/// <summary>
/// Matriz de D4 de VF-007 (RF-082): qué escrituras de <c>PermisoAcceso</c> se someten a contención.
/// </summary>
/// <remarks>
/// La tabla se escribe caso por caso en lugar de recalcularse con la misma fórmula que se prueba: una
/// prueba que repite la implementación no puede detectar un error en ella.
/// </remarks>
public sealed class ReglaContencionPermisoTests
{
    [Theory]
    // Alta: se contiene si nace ACTIVO, no si nace INACTIVO.
    [InlineData(null, Estado.ACTIVO, false, true)]
    [InlineData(null, Estado.ACTIVO, true, true)]
    [InlineData(null, Estado.INACTIVO, false, false)]
    [InlineData(null, Estado.INACTIVO, true, false)]
    // ACTIVO → ACTIVO: solo si cambia la vigencia (cambiar solo bloques no se contiene).
    [InlineData(Estado.ACTIVO, Estado.ACTIVO, false, false)]
    [InlineData(Estado.ACTIVO, Estado.ACTIVO, true, true)]
    // ACTIVO → INACTIVO: desactivar nunca se contiene, cambie o no la vigencia.
    [InlineData(Estado.ACTIVO, Estado.INACTIVO, false, false)]
    [InlineData(Estado.ACTIVO, Estado.INACTIVO, true, false)]
    // INACTIVO → ACTIVO: reactivar siempre se contiene.
    [InlineData(Estado.INACTIVO, Estado.ACTIVO, false, true)]
    [InlineData(Estado.INACTIVO, Estado.ACTIVO, true, true)]
    // INACTIVO → INACTIVO: sigue sin conceder acceso.
    [InlineData(Estado.INACTIVO, Estado.INACTIVO, false, false)]
    [InlineData(Estado.INACTIVO, Estado.INACTIVO, true, false)]
    public void Con_alcance_persona_se_contiene_solo_lo_que_concede_o_amplia_acceso(
        Estado? previo,
        Estado resultante,
        bool fechasCambian,
        bool esperado)
    {
        ReglaContencionPermiso
            .RequiereContencion(AlcancePermiso.PERSONA, previo, resultante, fechasCambian)
            .Should().Be(esperado);
    }

    public static TheoryData<AlcancePermiso, Estado?, Estado, bool> CombinacionesNoPersona()
    {
        var datos = new TheoryData<AlcancePermiso, Estado?, Estado, bool>();

        foreach (var alcance in new[] { AlcancePermiso.UNIDAD_ORGANIZATIVA, AlcancePermiso.COMPANIA })
        {
            foreach (var previo in new Estado?[] { null, Estado.ACTIVO, Estado.INACTIVO })
            {
                foreach (var resultante in new[] { Estado.ACTIVO, Estado.INACTIVO })
                {
                    foreach (var fechasCambian in new[] { false, true })
                    {
                        datos.Add(alcance, previo, resultante, fechasCambian);
                    }
                }
            }
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(CombinacionesNoPersona))]
    public void Los_alcances_unidad_y_compania_nunca_se_contienen(
        AlcancePermiso alcance,
        Estado? previo,
        Estado resultante,
        bool fechasCambian)
    {
        // D2: sin una persona concreta no hay pertenencia que sirva de límite.
        ReglaContencionPermiso
            .RequiereContencion(alcance, previo, resultante, fechasCambian)
            .Should().BeFalse();
    }

    // Los casos de FechasCambian (tolerancia de 1 ms, research.md §35.3) se retiraron en T292 (VF-004): con
    // fechas civiles, "cambian las fechas" lo decide VigenciaDiariaPermiso.ResolverExtremo, probado en
    // VigenciaDiariaPermisoTests. La matriz de RequiereContencion (D4) no cambia.
}
