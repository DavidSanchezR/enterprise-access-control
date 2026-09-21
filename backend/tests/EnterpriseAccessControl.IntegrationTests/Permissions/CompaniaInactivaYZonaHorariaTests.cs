using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Common;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// CS-039 (estado de la compañía) y CS-040 (zona horaria por Principal) — RF-079, RF-080.
/// </summary>
/// <remarks>
/// Ambas reglas comparten el mismo tramo del algoritmo de evaluación y se implementaron juntas
/// (D4 y D5). La denegación por compañía INACTIVA es **dinámica**: no escribe nada, así que
/// reactivar la compañía restablece el acceso sin ninguna otra intervención.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class CompaniaInactivaYZonaHorariaTests(SqlServerFixture fixture)
{
    // --- CS-039: Compañía INACTIVA (RF-079) ---------------------------------------------------

    [Fact]
    public async Task CS039_inactivar_la_principal_deniega_y_reactivarla_restablece_el_acceso()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        var concedido = await escenario.EvaluarAsync();
        concedido.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);

        // Estado antes de inactivar, para comprobar después que nada se modificó.
        var (contextos, credenciales, permisos) = await ContarDependientesAsync(escenario);

        await CambiarEstadoAsync(escenario.PrincipalA.Id, Estado.INACTIVO);

        var denegado = await escenario.EvaluarAsync();

        denegado.Resultado.Should().Be(ResultadoEvaluacion.DENEGADO);
        denegado.MotivoDenegacion.Should().Be(MotivoDenegacion.COMPANIA_INACTIVA);

        // Sin cascada de escritura: la denegación surge solo de la evaluación dinámica (D4).
        var despues = await ContarDependientesAsync(escenario);
        despues.Should().Be((contextos, credenciales, permisos));

        await CambiarEstadoAsync(escenario.PrincipalA.Id, Estado.ACTIVO);

        var restablecido = await escenario.EvaluarAsync();
        restablecido.Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
    }

    [Fact]
    public async Task CS039_inactivar_la_compania_de_pertenencia_tambien_deniega_con_el_mismo_motivo()
    {
        // El paso 6 comprueba la compañía de pertenencia vigente de la persona, no solo la Principal
        // propietaria del área (RF-079).
        var escenario = await new EscenarioPermisos(fixture).MontarAsync(comoContratista: true);
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        (await escenario.EvaluarAsync()).Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);

        await CambiarEstadoAsync(escenario.Contratista.Id, Estado.INACTIVO);

        var denegado = await escenario.EvaluarAsync();

        denegado.Resultado.Should().Be(ResultadoEvaluacion.DENEGADO);
        denegado.MotivoDenegacion.Should().Be(MotivoDenegacion.COMPANIA_INACTIVA);

        await CambiarEstadoAsync(escenario.Contratista.Id, Estado.ACTIVO);

        (await escenario.EvaluarAsync()).Resultado.Should().Be(ResultadoEvaluacion.CONCEDIDO);
    }

    // --- CS-040: zona horaria por Principal (RF-080) ------------------------------------------

    [Fact]
    public async Task CS040_la_respuesta_informa_la_zona_de_la_principal_evaluada()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        await CambiarZonaAsync(escenario.PrincipalA.Id, "America/Santiago");

        var resultado = await escenario.EvaluarAsync();

        // Antes de D5 esta respuesta informaba siempre la zona global del proceso.
        resultado.EvaluadoEnZonaHoraria.Should().Be("America/Santiago");
    }

    [Fact]
    public async Task CS040_cambiar_la_zona_no_altera_ningun_instante_persistido()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();
        await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        var antes = await InstantesDelContextoAsync(escenario);

        await CambiarZonaAsync(escenario.PrincipalA.Id, "America/Santiago");

        var despues = await InstantesDelContextoAsync(escenario);

        // Los instantes se persisten en UTC: la zona solo cambia su representación local (Principio IV).
        despues.Should().Be(antes);
    }

    [Fact]
    public async Task CS040_dos_principales_con_zonas_distintas_evaluan_de_forma_independiente()
    {
        var escenario = await new EscenarioPermisos(fixture).MontarAsync();

        await CambiarZonaAsync(escenario.PrincipalA.Id, "America/Lima");
        await CambiarZonaAsync(escenario.PrincipalB.Id, "Pacific/Auckland");

        using var ambito = fixture.Api.Services.CreateScope();
        var reloj = ambito.ServiceProvider.GetRequiredService<IRelojEmpresarial>();

        var instante = new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc);

        var horaLima = reloj.HoraLocal(instante, "America/Lima");
        var horaAuckland = reloj.HoraLocal(instante, "Pacific/Auckland");

        // El mismo instante UTC cae en horas locales distintas para cada Principal: es exactamente lo
        // que un único reloj de proceso no podía representar antes de D5.
        horaLima.Should().NotBe(horaAuckland);

        reloj.ZonaEfectiva("America/Lima").Should().Be("America/Lima");

        // Una zona no resoluble cae en el respaldo global, no falla la evaluación.
        reloj.ZonaEfectiva(null).Should().NotBeNullOrEmpty();
        reloj.EsZonaValida("No/Existe").Should().BeFalse();
    }

    // --- Utilidades ----------------------------------------------------------------------------

    private Task CambiarEstadoAsync(Guid companiaId, Estado estado) =>
        fixture.Api.ConDbContextAsync(async db =>
        {
            var compania = await db.Set<Compania>().SingleAsync(c => c.Id == companiaId);
            compania.Estado = estado;
            await db.SaveChangesAsync();
        });

    private Task CambiarZonaAsync(Guid companiaId, string zonaIana) =>
        fixture.Api.ConDbContextAsync(async db =>
        {
            var compania = await db.Set<Compania>().SingleAsync(c => c.Id == companiaId);
            compania.ZonaHorariaIana = zonaIana;
            await db.SaveChangesAsync();
        });

    private async Task<(int Contextos, int Credenciales, int Permisos)> ContarDependientesAsync(
        EscenarioPermisos escenario)
    {
        var resultado = (0, 0, 0);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var contextos = await db.Set<ContextoOperativoPersonaPrincipal>()
                .CountAsync(c => c.PersonaId == escenario.Persona.Id);

            var credenciales = await db.Set<AsignacionCredencial>()
                .CountAsync(c => c.PersonaId == escenario.Persona.Id);

            var permisos = await db.Set<PermisoAcceso>()
                .CountAsync(p => p.AreaAccesoId == escenario.Area.Id);

            resultado = (contextos, credenciales, permisos);
        });

        return resultado;
    }

    private async Task<(DateTime Inicio, DateTime Fin)> InstantesDelContextoAsync(
        EscenarioPermisos escenario)
    {
        var resultado = (DateTime.MinValue, DateTime.MinValue);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var contexto = await db.Set<ContextoOperativoPersonaPrincipal>()
                .AsNoTracking()
                .SingleAsync(c => c.Id == escenario.ContextoId);

            resultado = (contexto.FechaHoraInicio, contexto.FechaHoraFin);
        });

        return resultado;
    }
}
