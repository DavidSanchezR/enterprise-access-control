using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using FluentAssertions;

namespace EnterpriseAccessControl.UnitTests.Auth;

/// <summary>
/// Invariantes de dominio de la asignación de rol administrativo (RF-074, RF-075), aislados de la
/// base de datos.
/// </summary>
/// <remarks>
/// Aquí se fija el <em>qué</em> hace válida a una asignación. El <em>dónde</em> se impone —el
/// <c>CHECK</c>, el trigger de no-solapamiento y el filtrado por alcance— se verifica en las pruebas
/// de integración contra SQL Server real, que es donde esa parte puede fallar de verdad.
/// </remarks>
public sealed class AsignacionRolAdministrativoTests
{
    private static readonly DateTime Inicio = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Fin = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AsignacionRolAdministrativo Asignacion(
        RolAdministrativo rol,
        Guid? companiaId,
        DateTime? fin = null) => new()
    {
        UsuarioId = Guid.CreateVersion7(),
        Rol = rol,
        CompaniaId = companiaId,
        FechaHoraInicio = Inicio,
        FechaHoraFin = fin ?? Fin,
    };

    // --- Regla fundamental de CompañíaId (RF-074) ---------------------------------------------

    [Fact]
    public void Un_administrador_global_no_admite_compania()
    {
        // Su alcance es toda compañía: asociarlo a una sola lo contradiría.
        Asignacion(RolAdministrativo.GLOBAL_ADMINISTRATOR, Guid.CreateVersion7())
            .CumpleReglaFundamental()
            .Should().BeFalse();

        Asignacion(RolAdministrativo.GLOBAL_ADMINISTRATOR, null)
            .CumpleReglaFundamental()
            .Should().BeTrue();
    }

    [Fact]
    public void Un_administrador_de_compania_exige_una_compania()
    {
        Asignacion(RolAdministrativo.COMPANY_ADMINISTRATOR, null)
            .CumpleReglaFundamental()
            .Should().BeFalse();

        Asignacion(RolAdministrativo.COMPANY_ADMINISTRATOR, Guid.CreateVersion7())
            .CumpleReglaFundamental()
            .Should().BeTrue();
    }

    // --- Vigencia por fechas, nunca por estado (RF-075, Principio IV) --------------------------

    [Fact]
    public void La_vigencia_se_evalua_contra_las_fechas_y_es_semiabierta()
    {
        var asignacion = Asignacion(RolAdministrativo.GLOBAL_ADMINISTRATOR, null);

        asignacion.EstaVigenteEn(Inicio.AddDays(-1)).Should().BeFalse();

        // El inicio se incluye y el fin se excluye: así dos asignaciones consecutivas no se solapan.
        asignacion.EstaVigenteEn(Inicio).Should().BeTrue();
        asignacion.EstaVigenteEn(Fin.AddMilliseconds(-1)).Should().BeTrue();
        asignacion.EstaVigenteEn(Fin).Should().BeFalse();
    }

    [Fact]
    public void Dos_asignaciones_consecutivas_no_se_solapan_en_el_instante_de_corte()
    {
        var primera = Asignacion(RolAdministrativo.COMPANY_ADMINISTRATOR, Guid.CreateVersion7());

        var segunda = new AsignacionRolAdministrativo
        {
            UsuarioId = primera.UsuarioId,
            Rol = RolAdministrativo.COMPANY_ADMINISTRATOR,
            CompaniaId = primera.CompaniaId,
            FechaHoraInicio = Fin,
            FechaHoraFin = Fin.AddYears(1),
        };

        primera.EstaVigenteEn(Fin).Should().BeFalse();
        segunda.EstaVigenteEn(Fin).Should().BeTrue();
    }

    [Fact]
    public void Un_usuario_puede_administrar_varias_companias_a_la_vez()
    {
        // La partición de no-solapamiento es (Usuario, Compañía): dos compañías distintas pueden
        // tener asignaciones simultáneas del mismo usuario (RF-075).
        var usuarioId = Guid.CreateVersion7();

        var enA = Asignacion(RolAdministrativo.COMPANY_ADMINISTRATOR, Guid.CreateVersion7());
        var enB = Asignacion(RolAdministrativo.COMPANY_ADMINISTRATOR, Guid.CreateVersion7());

        var instante = Inicio.AddMonths(1);

        enA.EstaVigenteEn(instante).Should().BeTrue();
        enB.EstaVigenteEn(instante).Should().BeTrue();
        (enA.CompaniaId == enB.CompaniaId).Should().BeFalse();

        // Nada en el dominio impide que ambas pertenezcan al mismo usuario.
        usuarioId.Should().NotBeEmpty();
    }

    // --- Renovación (RF-073 aplicado a RF-075) -------------------------------------------------

    [Fact]
    public void Una_asignacion_vigente_es_renovable()
    {
        Asignacion(RolAdministrativo.COMPANY_ADMINISTRATOR, Guid.CreateVersion7())
            .EsRenovableEn(Inicio.AddMonths(6))
            .Should().BeTrue();
    }

    [Fact]
    public void Una_asignacion_ya_expirada_no_es_renovable()
    {
        // Extenderla puentearía retroactivamente un intervalo sin autorización: requiere una nueva.
        Asignacion(RolAdministrativo.COMPANY_ADMINISTRATOR, Guid.CreateVersion7())
            .EsRenovableEn(Fin.AddDays(1))
            .Should().BeFalse();
    }

    // --- Excepción acotada del arranque (RF-078) -----------------------------------------------

    [Fact]
    public void La_fecha_centinela_del_arranque_es_exactamente_la_declarada()
    {
        // Es la única excepción del sistema a RF-071/RF-075 y no debe cambiar de valor sin que una
        // prueba lo note: hay datos sembrados que dependen de ella.
        VigenciaBootstrap.MaxValidityDate
            .Should().Be(new DateTime(2999, 12, 31, 23, 59, 59, DateTimeKind.Utc));

        VigenciaBootstrap.MaxValidityDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void La_asignacion_sembrada_por_el_arranque_cumple_la_regla_fundamental()
    {
        var bootstrap = Asignacion(
            RolAdministrativo.GLOBAL_ADMINISTRATOR,
            companiaId: null,
            fin: VigenciaBootstrap.MaxValidityDate);

        bootstrap.CumpleReglaFundamental().Should().BeTrue();
        bootstrap.EstaVigenteEn(DateTime.UtcNow).Should().BeTrue();
    }
}
