using System.Net;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;

namespace EnterpriseAccessControl.IntegrationTests.People;

/// <summary>
/// Cambio de compañía de pertenencia: la nueva cierra la anterior y revoca en cascada sus tres
/// tipos de dependientes (CS-025, CS-027, RF-061).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class RevocacionPorCambioCompaniaTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Una_nueva_pertenencia_revoca_contextos_unidades_y_credenciales()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        // Cambio de compañía: la persona pasa a la Principal A directamente.
        await escenario.PertenenciaVigenteAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddYears(1));

        var contextos = await escenario.ContextosEnBaseAsync();
        var unidades = await escenario.UnidadesEnBaseAsync();
        var credenciales = await escenario.CredencialesEnBaseAsync();

        // Las tres entidades dependientes se revocan juntas, en la misma transacción.
        contextos.Should().OnlyContain(c =>
            c.Estado == Estado.INACTIVO
            && c.MotivoFin == MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA
            && c.RevocadoPorPertenenciaId == pertenencia.Id);

        unidades.Should().OnlyContain(u =>
            u.Estado == Estado.INACTIVO
            && u.MotivoFin == MotivoFinRevocacion.REVOCACION_CESE_PERTENENCIA
            && u.RevocadoPorPertenenciaId == pertenencia.Id);

        credenciales.Should().OnlyContain(c =>
            c.Estado == EstadoCredencial.REVOCADA
            && c.RevocadoPorPertenenciaId == pertenencia.Id);
    }

    [Fact]
    public async Task La_pertenencia_anterior_se_cierra_por_reemplazo()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddYears(1));

        var anterior = (await escenario.PertenenciasEnBaseAsync())
            .Single(p => p.Id == pertenencia.Id);

        anterior.Estado.Should().Be(EstadoPertenencia.FINALIZADA);

        // El motivo distingue el reemplazo del cese explícito: son causas distintas y la auditoría
        // debe poder diferenciarlas.
        anterior.MotivoFin.Should().Be(MotivoFinPertenencia.REEMPLAZO_ASIGNACION);
    }

    [Fact]
    public async Task La_credencial_revocada_no_lleva_motivo_de_fin_separado()
    {
        var (escenario, _, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddYears(1));

        var credencial = (await escenario.CredencialesEnBaseAsync())[0];

        // research.md §14.2: el valor REVOCADA del estado ya expresa la causa; añadir un MotivoFin
        // duplicaría el dato.
        credencial.Estado.Should().Be(EstadoCredencial.REVOCADA);
        credencial.RevocadoPorPertenenciaId.Should().NotBeNull();
    }

    [Fact]
    public async Task La_cascada_es_atomica_con_el_cambio_de_pertenencia()
    {
        var (escenario, _, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddYears(1));

        // No debe existir ningún estado intermedio observable: si la pertenencia quedó cerrada, sus
        // dependientes ya están revocados. Un trabajo asíncrono dejaría esa ventana abierta.
        var pertenencias = await escenario.PertenenciasEnBaseAsync();
        var cerrada = pertenencias.Single(p => p.Estado == EstadoPertenencia.FINALIZADA);

        (await escenario.ContextosEnBaseAsync())
            .Should().OnlyContain(c => c.RevocadoPorPertenenciaId == cerrada.Id);
    }

    [Fact]
    public async Task La_fecha_de_revocacion_coincide_con_el_cierre_de_la_pertenencia()
    {
        var (escenario, pertenencia, _) =
            await CascadaSoporte.MontarConDependientesAsync(fixture, ambasPrincipales: false);

        using var _c = escenario.Cliente;

        var inicioNueva = DateTime.UtcNow.AddDays(1);

        await escenario.PertenenciaVigenteAsync(
            escenario.PrincipalA.Id, inicioNueva, DateTime.UtcNow.AddYears(1));

        var cerrada = (await escenario.PertenenciasEnBaseAsync()).Single(p => p.Id == pertenencia.Id);
        var contexto = (await escenario.ContextosEnBaseAsync())[0];

        // El dependiente hereda exactamente la fecha efectiva del cierre.
        contexto.FechaHoraFin.Should().Be(cerrada.FechaHoraFin);
    }

    [Fact]
    public async Task Un_cambio_de_compania_no_elimina_ningun_registro()
    {
        var (escenario, _, dependientes) =
            await CascadaSoporte.MontarConDependientesAsync(fixture);

        using var _c = escenario.Cliente;

        await escenario.PertenenciaVigenteAsync(
            escenario.PrincipalA.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddYears(1));

        // CS-026: revocar es cerrar, nunca borrar. Los dos contextos, sus dos unidades y sus dos
        // credenciales siguen ahí.
        (await escenario.ContextosEnBaseAsync()).Should().HaveCount(dependientes.Count);
        (await escenario.UnidadesEnBaseAsync()).Should().HaveCount(dependientes.Count);
        (await escenario.CredencialesEnBaseAsync()).Should().HaveCount(dependientes.Count);
    }
}
