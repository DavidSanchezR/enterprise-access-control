using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using EnterpriseAccessControl.IntegrationTests.People;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Persistence;

/// <summary>
/// Revisión de los seis triggers de no-solapamiento (T164, T172; research.md §5, Principio IV).
/// </summary>
/// <remarks>
/// Verifica tres cosas sobre el SQL Server del contenedor, que se construye exclusivamente aplicando las
/// migraciones de EF Core:
/// <list type="number">
///   <item>que existen exactamente los seis triggers, habilitados, <c>AFTER INSERT, UPDATE</c>, sobre la
///   tabla correcta, y que llegaron a la base de datos por migraciones versionadas;</item>
///   <item>que cada uno particiona por las columnas que declara research.md §5;</item>
///   <item>que cada uno se dispara de verdad ante un solapamiento escrito saltándose la aplicación.</item>
/// </list>
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class TriggersNoSolapamientoTests(SqlServerFixture fixture)
{
    public static TheoryData<string, string, string[]> Esperados => new()
    {
        { "trg_AsignacionPersonaCompania_NoSolapamiento", "AsignacionPersonaCompania", ["t.[PersonaId] = i.[PersonaId]"] },
        { "trg_RelacionContratistaPrincipal_NoSolapamiento", "RelacionContratistaPrincipal", ["t.[CompaniaContratistaId] = i.[CompaniaContratistaId]", "t.[CompaniaPrincipalId]  = i.[CompaniaPrincipalId]"] },
        { "trg_ContextoOperativoPersonaPrincipal_NoSolapamiento", "ContextoOperativoPersonaPrincipal", ["t.[PersonaId] = i.[PersonaId]", "t.[CompaniaPrincipalId] = i.[CompaniaPrincipalId]"] },
        { "trg_AsignacionPersonaUnidadOrganizativa_NoSolapamiento", "AsignacionPersonaUnidadOrganizativa", ["t.[ContextoOperativoId] = i.[ContextoOperativoId]"] },
        { "trg_AsignacionCredencial_NoSolapamiento", "AsignacionCredencial", ["t.[PersonaId] = i.[PersonaId]", "t.[CompaniaPrincipalId] = i.[CompaniaPrincipalId]", "t.[Estado] = 'ASIGNADO'", "i.[Estado] = 'ASIGNADO'"] },
        // Sesión 2026-09-20 (RF-075): particiona por (UsuarioId, CompaniaId) y excluye al rol GLOBAL.
        { "trg_AsignacionRolAdministrativo_NoSolapamiento", "AsignacionRolAdministrativo", ["t.[UsuarioId] = i.[UsuarioId]", "t.[CompaniaId] = i.[CompaniaId]", "t.[Rol] = 'COMPANY_ADMINISTRATOR'", "i.[Rol] = 'COMPANY_ADMINISTRATOR'"] },
    };

    private sealed record FilaTrigger(string Nombre, string Tabla, bool Deshabilitado, bool EsInsteadOf, string Definicion);

    [Fact]
    public async Task Existen_exactamente_los_seis_triggers_de_no_solapamiento()
    {
        var nombres = (await TriggersAsync()).Select(t => t.Nombre).ToList();

        nombres.Should().BeEquivalentTo(
        [
            "trg_AsignacionPersonaCompania_NoSolapamiento",
            "trg_RelacionContratistaPrincipal_NoSolapamiento",
            "trg_ContextoOperativoPersonaPrincipal_NoSolapamiento",
            "trg_AsignacionPersonaUnidadOrganizativa_NoSolapamiento",
            "trg_AsignacionCredencial_NoSolapamiento",
            "trg_AsignacionRolAdministrativo_NoSolapamiento",
        ]);
    }

    [Theory]
    [MemberData(nameof(Esperados))]
    public async Task Cada_trigger_esta_habilitado_sobre_su_tabla_y_particiona_como_research_5(
        string nombre,
        string tabla,
        string[] fragmentos)
    {
        var trigger = (await TriggersAsync()).Single(t => t.Nombre == nombre);

        trigger.Tabla.Should().Be(tabla);
        trigger.Deshabilitado.Should().BeFalse();
        trigger.EsInsteadOf.Should().BeFalse("research.md §5 exige AFTER INSERT, UPDATE");

        (await EventosAsync(nombre)).Should().BeEquivalentTo(["INSERT", "UPDATE"]);

        foreach (var fragmento in fragmentos)
        {
            trigger.Definicion.Should().Contain(fragmento);
        }

        trigger.Definicion.Should().Contain("THROW 50001");
    }

    [Fact]
    public async Task Los_triggers_llegan_a_la_base_de_datos_por_migraciones_versionadas()
    {
        List<string> aplicadas = [];

        await fixture.Api.ConDbContextAsync(async db =>
            aplicadas = [.. await db.Database.GetAppliedMigrationsAsync()]);

        aplicadas.Should().Contain(m => m.EndsWith("_AddRelacionContratistaPrincipalTrigger", StringComparison.Ordinal));
        aplicadas.Should().Contain(m => m.EndsWith("_AddTriggersNoSolapamientoUS5", StringComparison.Ordinal));

        // El sexto trigger llega en la migración del modelo RBAC, no en una aplicada a mano (T172).
        aplicadas.Should().Contain(
            m => m.EndsWith("_RolesAdministrativos_ZonaHoraria_Etapa1", StringComparison.Ordinal));

        List<string> pendientes = [];
        await fixture.Api.ConDbContextAsync(async db =>
            pendientes = [.. await db.Database.GetPendingMigrationsAsync()]);

        pendientes.Should().BeEmpty();
    }

    // --- Disparo real, saltándose la aplicación -----------------------------------------------------

    [Fact]
    public async Task Pertenencia_solapada_de_la_misma_persona_se_rechaza()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);

        await EsperarRechazoAsync(db => db.Add(new AsignacionPersonaCompania
        {
            PersonaId = escenario.Persona.Id,
            CompaniaId = escenario.PrincipalA.Id,
            FechaHoraInicio = DateTime.UtcNow,
            FechaHoraFin = DateTime.UtcNow.AddMonths(1),
        }));
    }

    [Fact]
    public async Task Relacion_solapada_del_mismo_par_contratista_principal_se_rechaza()
    {
        // EscenarioUs5 ya siembra una relación vigente y abierta Contratista↔Principal A.
        var escenario = await new EscenarioUs5(fixture).MontarAsync();

        await EsperarRechazoAsync(db => db.Add(new RelacionContratistaPrincipal
        {
            CompaniaContratistaId = escenario.Contratista.Id,
            CompaniaPrincipalId = escenario.PrincipalA.Id,
            FechaHoraInicio = DateTime.UtcNow,
            FechaHoraFin = DateTime.UtcNow.AddMonths(1),
        }));
    }

    [Fact]
    public async Task Contexto_solapado_de_la_misma_persona_y_principal_se_rechaza()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);

        await EsperarRechazoAsync(db => db.Add(new ContextoOperativoPersonaPrincipal
        {
            PersonaId = escenario.Persona.Id,
            CompaniaPrincipalId = escenario.PrincipalA.Id,
            FechaHoraInicio = DateTime.UtcNow,
            FechaHoraFin = DateTime.UtcNow.AddMonths(1),
        }));
    }

    [Fact]
    public async Task Unidad_solapada_en_el_mismo_contexto_se_rechaza()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        var contexto = await escenario.ContextoAsync(escenario.PrincipalA.Id);
        var unidad = await escenario.SembrarUnidadAsync(escenario.PrincipalA.Id);

        (await escenario.AsignarUnidadAsync(contexto.Id, unidad)).EnsureSuccessStatusCode();

        await EsperarRechazoAsync(db => db.Add(new AsignacionPersonaUnidadOrganizativa
        {
            PersonaId = escenario.Persona.Id,
            ContextoOperativoId = contexto.Id,
            UnidadOrganizativaId = unidad,
            FechaHoraInicio = DateTime.UtcNow,
            FechaHoraFin = DateTime.UtcNow.AddMonths(1),
        }));
    }

    [Fact]
    public async Task Credencial_ASIGNADO_solapada_de_la_misma_persona_y_principal_se_rechaza()
    {
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);
        await escenario.AsignarCredencialAsync(escenario.PrincipalA.Id);

        await EsperarRechazoAsync(db => db.Add(new AsignacionCredencial
        {
            PersonaId = escenario.Persona.Id,
            CompaniaPrincipalId = escenario.PrincipalA.Id,
            TipoCredencialId = escenario.TipoCredencialId,
            FechaHoraInicio = DateTime.UtcNow,
            FechaHoraFin = DateTime.UtcNow.AddMonths(1),
            Estado = EstadoCredencial.ASIGNADO,
        }));
    }

    [Fact]
    public async Task Tambien_se_disparan_ante_un_UPDATE_que_provoca_el_solapamiento()
    {
        // AFTER INSERT, UPDATE: ampliar una fila existente hasta pisar a otra también se rechaza.
        var escenario = await new EscenarioUs5(fixture).MontarAsync();
        await escenario.PertenenciaVigenteAsync(escenario.Contratista.Id);
        await escenario.ContextoAsync(escenario.PrincipalA.Id);

        var inicio = DateTime.UtcNow;
        var futura = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id, inicio.AddMonths(2), inicio.AddMonths(3));
        var actual = await escenario.AsignarCredencialAsync(
            escenario.PrincipalA.Id, inicio, inicio.AddMonths(1));

        futura.FechaHoraInicio.Should().BeAfter(actual.FechaHoraFin, "premisa: no se solapan");

        await fixture.Api.ConDbContextAsync(async db =>
        {
            var fila = await db.Set<AsignacionCredencial>().SingleAsync(c => c.Id == actual.Id);
            fila.FechaHoraFin = futura.FechaHoraFin;

            var guardar = async () => await db.SaveChangesAsync();
            await EsperarError50001Async(guardar);
        });
    }

    // --- Utilidades ---------------------------------------------------------------------------------

    private Task EsperarRechazoAsync(Action<Infrastructure.Persistence.AppDbContext> escribir) =>
        fixture.Api.ConDbContextAsync(async db =>
        {
            escribir(db);

            var guardar = async () => await db.SaveChangesAsync();
            await EsperarError50001Async(guardar);
        });

    private static async Task EsperarError50001Async(Func<Task> accion)
    {
        var error = await accion.Should().ThrowAsync<DbUpdateException>();

        error.Which.InnerException.Should().BeOfType<SqlException>()
            .Which.Number.Should().Be(50001, "debe ser el THROW del trigger y no otra restricción");
    }

    private async Task<List<FilaTrigger>> TriggersAsync()
    {
        List<FilaTrigger> filas = [];

        await fixture.Api.ConDbContextAsync(async db =>
            filas = await db.Database.SqlQuery<FilaTrigger>($"""
                SELECT tr.name                        AS Nombre,
                       OBJECT_NAME(tr.parent_id)      AS Tabla,
                       tr.is_disabled                 AS Deshabilitado,
                       tr.is_instead_of_trigger       AS EsInsteadOf,
                       OBJECT_DEFINITION(tr.object_id) AS Definicion
                FROM sys.triggers tr
                WHERE tr.parent_class = 1 AND tr.name LIKE 'trg[_]%[_]NoSolapamiento'
                """).ToListAsync());

        return filas;
    }

    private async Task<List<string>> EventosAsync(string trigger)
    {
        List<string> eventos = [];

        await fixture.Api.ConDbContextAsync(async db =>
            eventos = await db.Database.SqlQuery<string>($"""
                SELECT te.type_desc AS Value
                FROM sys.trigger_events te
                JOIN sys.triggers tr ON tr.object_id = te.object_id
                WHERE tr.name = {trigger}
                """).ToListAsync());

        return eventos;
    }
}
