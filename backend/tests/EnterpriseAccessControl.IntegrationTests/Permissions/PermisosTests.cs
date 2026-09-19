using System.Net;
using System.Net.Http.Json;
using EnterpriseAccessControl.Application.Common;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.AreaAccess;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Permissions;

/// <summary>
/// Mantenimiento de permisos de acceso contra SQL Server real: vigencia obligatoria, bloques
/// horarios, exclusividad del sujeto y alcance administrativo (RF-020 a RF-022, RF-039, RF-049,
/// RF-071).
/// </summary>
/// <remarks>
/// Cada regla se comprueba en el nivel donde puede fallar de verdad: el borde HTTP (400), el
/// servicio (reglas de negocio) y la base de datos (NOT NULL y CHECK), porque una garantía que solo
/// existe en uno de ellos deja un hueco en los otros.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class PermisosTests(SqlServerFixture fixture)
{
    private Task<EscenarioPermisos> MontarAsync() => new EscenarioPermisos(fixture).MontarAsync();

    // --- Alta y persistencia --------------------------------------------------------------------

    [Theory]
    [InlineData(AlcancePermiso.PERSONA)]
    [InlineData(AlcancePermiso.UNIDAD_ORGANIZATIVA)]
    [InlineData(AlcancePermiso.COMPANIA)]
    public async Task Un_permiso_valido_se_crea_y_se_recupera_con_sus_bloques(AlcancePermiso alcance)
    {
        var escenario = await MontarAsync();

        var peticion = escenario.Peticion(
            alcance,
            bloques:
            [
                new BloqueHorarioRequest(DiaSemana.LUNES, "08:00", "12:00"),
                new BloqueHorarioRequest(DiaSemana.LUNES, "14:00", "18:00"),
                new BloqueHorarioRequest(DiaSemana.MIERCOLES, "08:00", "17:00"),
            ]);

        using var respuesta = await escenario.PostPermisoAsync(peticion);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        respuesta.Headers.Location.Should().NotBeNull();

        var creado = (await respuesta.Content.ReadFromJsonAsync<PermisoAccesoDto>(ApiFactory.Json))!;

        var leido = await escenario.Cliente.GetFromJsonAsync<PermisoAccesoDto>(
            new Uri($"/api/permisos/{creado.Id}", UriKind.Relative), ApiFactory.Json);

        leido!.Alcance.Should().Be(alcance);
        leido.SujetoDe(alcance).Should().NotBeNull();
        leido.FechaHoraInicioVigencia.Should().Be(peticion.FechaHoraInicioVigencia);
        leido.FechaHoraFinVigencia.Should().Be(peticion.FechaHoraFinVigencia);
        leido.BloquesHorarios.Select(b => (b.DiaSemana, b.HoraInicio, b.HoraFin))
            .Should().Equal(
                (DiaSemana.LUNES, "08:00", "12:00"),
                (DiaSemana.LUNES, "14:00", "18:00"),
                (DiaSemana.MIERCOLES, "08:00", "17:00"));
    }

    [Fact]
    public async Task Solo_se_persiste_el_sujeto_del_alcance_declarado()
    {
        var escenario = await MontarAsync();

        var creado = await escenario.CrearPermisoAsync(AlcancePermiso.UNIDAD_ORGANIZATIVA);

        creado.UnidadOrganizativaId.Should().Be(escenario.UnidadId);
        creado.PersonaId.Should().BeNull();
        creado.CompaniaId.Should().BeNull();
    }

    [Fact]
    public async Task Un_permiso_de_alcance_compania_admite_una_contratista()
    {
        // research.md §12: el acceso de todo el personal de una contratista a un área de la
        // Principal a la que presta servicios es un caso válido.
        var escenario = await MontarAsync();

        var creado = await escenario.CrearPermisoAsync(
            AlcancePermiso.COMPANIA, sujetoId: escenario.Contratista.Id);

        creado.CompaniaId.Should().Be(escenario.Contratista.Id);
    }

    // --- Vigencia obligatoria (RF-021, RF-071) --------------------------------------------------

    [Theory]
    [InlineData("PERSONA")]
    [InlineData("UNIDAD_ORGANIZATIVA")]
    [InlineData("COMPANIA")]
    public async Task Omitir_la_fecha_de_fin_se_rechaza_con_400_en_los_tres_alcances(string alcance)
    {
        var escenario = await MontarAsync();

        var cuerpo = new Dictionary<string, object?>
        {
            ["areaAccesoId"] = escenario.Area.Id,
            ["alcance"] = alcance,
            ["fechaHoraInicioVigencia"] = EscenarioPermisos.Instante.AddMonths(-1),
            ["estado"] = "ACTIVO",
            ["bloquesHorarios"] = new[] { new { diaSemana = "LUNES", horaInicio = "08:00", horaFin = "17:00" } },
            ["personaId"] = alcance == "PERSONA" ? escenario.Persona.Id : null,
            ["unidadOrganizativaId"] = alcance == "UNIDAD_ORGANIZATIVA" ? escenario.UnidadId : null,
            ["companiaId"] = alcance == "COMPANIA" ? escenario.PrincipalA.Id : null,
        };

        using var respuesta = await escenario.Cliente.PostAsJsonAsync(
            new Uri("/api/permisos", UriKind.Relative), cuerpo, ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        respuesta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        // El rechazo nombra el campo ausente, no un período inválido derivado de default(DateTime).
        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>(ApiFactory.Json);
        problema!.Errors.Keys.Should().Contain(
            k => string.Equals(k, "fechaHoraFinVigencia", StringComparison.OrdinalIgnoreCase));

        (await PermisosDelAreaAsync(escenario.Area.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task La_columna_de_fin_de_vigencia_es_not_null_en_la_base_de_datos()
    {
        // Segunda línea: aunque el borde HTTP falle, la vigencia abierta no puede escribirse.
        var admiteNulos = true;

        await fixture.Api.ConDbContextAsync(async db =>
        {
            admiteNulos = await db.Database
                .SqlQuery<bool>($"""
                    SELECT c.is_nullable AS Value
                    FROM sys.columns c
                    JOIN sys.tables t ON t.object_id = c.object_id
                    WHERE t.name = 'PermisoAcceso' AND c.name = 'FechaHoraFinVigencia'
                    """)
                .SingleAsync();
        });

        admiteNulos.Should().BeFalse();
    }

    [Fact]
    public async Task Una_vigencia_con_fin_no_posterior_al_inicio_se_rechaza_con_400()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            inicio: EscenarioPermisos.Instante,
            fin: EscenarioPermisos.Instante));

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, "PERIODO_INVALIDO");
    }

    // --- Bloques horarios (RF-022, RF-039) ------------------------------------------------------

    [Theory]
    [InlineData("17:00", "08:00")]
    [InlineData("08:00", "08:00")]
    public async Task Un_bloque_con_fin_no_posterior_al_inicio_se_rechaza_con_400(
        string horaInicio,
        string horaFin)
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            bloques: [new BloqueHorarioRequest(DiaSemana.MARTES, horaInicio, horaFin)]));

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, "PERIODO_INVALIDO");
    }

    [Fact]
    public async Task Dos_bloques_solapados_en_el_mismo_dia_se_rechazan_con_400()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            bloques:
            [
                new BloqueHorarioRequest(DiaSemana.JUEVES, "08:00", "13:00"),
                new BloqueHorarioRequest(DiaSemana.JUEVES, "12:00", "17:00"),
            ]));

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, "SOLAPAMIENTO_VIGENCIA");

        // Validado antes de escribir: ni el permiso ni sus bloques quedan a medias.
        (await PermisosDelAreaAsync(escenario.Area.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Bloques_consecutivos_del_mismo_dia_no_se_consideran_solapados()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            bloques:
            [
                new BloqueHorarioRequest(DiaSemana.JUEVES, "08:00", "12:00"),
                new BloqueHorarioRequest(DiaSemana.JUEVES, "12:00", "17:00"),
            ]));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task El_mismo_horario_en_dias_distintos_no_es_un_solapamiento()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            bloques:
            [
                new BloqueHorarioRequest(DiaSemana.LUNES, "08:00", "17:00"),
                new BloqueHorarioRequest(DiaSemana.MARTES, "08:00", "17:00"),
            ]));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Un_permiso_sin_bloques_horarios_se_rechaza_con_400()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(
            escenario.Peticion(AlcancePermiso.PERSONA, bloques: []));

        respuesta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("8:00")]
    [InlineData("24:00")]
    [InlineData("08:00:00")]
    [InlineData("ocho")]
    public async Task Una_hora_fuera_del_formato_HH_mm_se_rechaza_con_400(string hora)
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            bloques: [new BloqueHorarioRequest(DiaSemana.LUNES, hora, "23:00")]));

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, "VALIDACION_ENTRADA");
    }

    [Fact]
    public async Task El_check_de_la_base_de_datos_impide_un_fin_de_bloque_no_posterior()
    {
        var escenario = await MontarAsync();
        var permiso = await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<BloqueHorarioPermiso>().Add(new BloqueHorarioPermiso
            {
                PermisoAccesoId = permiso.Id,
                DiaSemana = DiaSemana.VIERNES,
                HoraInicio = new TimeOnly(17, 0),
                HoraFin = new TimeOnly(8, 0),
            });

            var guardar = async () => await db.SaveChangesAsync();
            await guardar.Should().ThrowAsync<DbUpdateException>();
        });
    }

    // --- Exclusividad del sujeto ----------------------------------------------------------------

    [Fact]
    public async Task Informar_dos_sujetos_se_rechaza_con_400()
    {
        var escenario = await MontarAsync();

        var peticion = escenario.Peticion(AlcancePermiso.PERSONA) with
        {
            UnidadOrganizativaId = escenario.UnidadId,
        };

        using var respuesta = await escenario.PostPermisoAsync(peticion);

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, "VALIDACION_ENTRADA");
    }

    [Fact]
    public async Task Un_sujeto_que_no_corresponde_al_alcance_se_rechaza_con_400()
    {
        var escenario = await MontarAsync();

        // Alcance PERSONA con solo una compañía informada.
        var peticion = escenario.Peticion(AlcancePermiso.PERSONA) with
        {
            PersonaId = null,
            CompaniaId = escenario.PrincipalA.Id,
        };

        using var respuesta = await escenario.PostPermisoAsync(peticion);

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, "VALIDACION_ENTRADA");
    }

    [Fact]
    public async Task Un_sujeto_inexistente_se_rechaza_con_400()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(
            escenario.Peticion(AlcancePermiso.PERSONA, sujetoId: Guid.CreateVersion7()));

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, "VALIDACION_ENTRADA");
    }

    [Theory]
    [InlineData(AlcancePermiso.PERSONA, true, true, false)]
    [InlineData(AlcancePermiso.UNIDAD_ORGANIZATIVA, true, false, false)]
    [InlineData(AlcancePermiso.COMPANIA, false, false, false)]
    public async Task El_check_de_la_base_de_datos_impide_un_sujeto_incoherente_con_el_alcance(
        AlcancePermiso alcance,
        bool conPersona,
        bool conUnidad,
        bool conCompania)
    {
        // Filas imposibles por el servicio, escritas saltándoselo: el CHECK es lo que las detiene.
        var escenario = await MontarAsync();

        await fixture.Api.ConDbContextAsync(async db =>
        {
            db.Set<PermisoAcceso>().Add(new PermisoAcceso
            {
                AreaAccesoId = escenario.Area.Id,
                Alcance = alcance,
                PersonaId = conPersona ? escenario.Persona.Id : null,
                UnidadOrganizativaId = conUnidad ? escenario.UnidadId : null,
                CompaniaId = conCompania ? escenario.PrincipalA.Id : null,
                FechaHoraInicioVigencia = EscenarioPermisos.Instante.AddMonths(-1),
                FechaHoraFinVigencia = EscenarioPermisos.Instante.AddMonths(1),
            });

            var guardar = async () => await db.SaveChangesAsync();

            (await guardar.Should().ThrowAsync<DbUpdateException>())
                .Which.InnerException!.Message.Should().Contain("CK_PermisoAcceso_SujetoSegunAlcance");
        });
    }

    // --- Actualización ---------------------------------------------------------------------------

    [Fact]
    public async Task Actualizar_cambia_vigencia_estado_y_reemplaza_los_bloques()
    {
        var escenario = await MontarAsync();

        var creado = await escenario.CrearPermisoAsync(escenario.Peticion(
            AlcancePermiso.PERSONA,
            bloques:
            [
                new BloqueHorarioRequest(DiaSemana.LUNES, "08:00", "12:00"),
                new BloqueHorarioRequest(DiaSemana.MARTES, "08:00", "12:00"),
            ]));

        var nuevoFin = EscenarioPermisos.Instante.AddYears(1);

        using var respuesta = await escenario.Cliente.PutAsJsonAsync(
            new Uri($"/api/permisos/{creado.Id}", UriKind.Relative),
            escenario.Peticion(
                AlcancePermiso.PERSONA,
                fin: nuevoFin,
                estado: Estado.INACTIVO,
                bloques: [new BloqueHorarioRequest(DiaSemana.SABADO, "09:00", "13:00")]),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);

        var actualizado = (await respuesta.Content.ReadFromJsonAsync<PermisoAccesoDto>(ApiFactory.Json))!;

        actualizado.Id.Should().Be(creado.Id);
        actualizado.Estado.Should().Be(Estado.INACTIVO);
        actualizado.FechaHoraFinVigencia.Should().Be(nuevoFin);
        actualizado.BloquesHorarios.Should().ContainSingle()
            .Which.DiaSemana.Should().Be(DiaSemana.SABADO);

        // Los bloques anteriores no quedan huérfanos en la base de datos.
        await fixture.Api.ConDbContextAsync(async db =>
            (await db.Set<BloqueHorarioPermiso>().AsNoTracking()
                .CountAsync(b => b.PermisoAccesoId == creado.Id))
            .Should().Be(1));
    }

    [Fact]
    public async Task Actualizar_no_permite_cambiar_el_alcance()
    {
        var escenario = await MontarAsync();
        var creado = await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        using var respuesta = await escenario.Cliente.PutAsJsonAsync(
            new Uri($"/api/permisos/{creado.Id}", UriKind.Relative),
            escenario.Peticion(AlcancePermiso.COMPANIA),
            ApiFactory.Json);

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, "VALIDACION_ENTRADA");

        var intacto = await escenario.Cliente.GetFromJsonAsync<PermisoAccesoDto>(
            new Uri($"/api/permisos/{creado.Id}", UriKind.Relative), ApiFactory.Json);

        intacto!.Alcance.Should().Be(AlcancePermiso.PERSONA);
    }

    [Fact]
    public async Task Actualizar_valida_los_bloques_igual_que_el_alta()
    {
        var escenario = await MontarAsync();
        var creado = await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        using var respuesta = await escenario.Cliente.PutAsJsonAsync(
            new Uri($"/api/permisos/{creado.Id}", UriKind.Relative),
            escenario.Peticion(
                AlcancePermiso.PERSONA,
                bloques:
                [
                    new BloqueHorarioRequest(DiaSemana.LUNES, "08:00", "12:00"),
                    new BloqueHorarioRequest(DiaSemana.LUNES, "11:00", "15:00"),
                ]),
            ApiFactory.Json);

        await EsperarProblemaAsync(respuesta, HttpStatusCode.BadRequest, "SOLAPAMIENTO_VIGENCIA");
    }

    // --- Listado y alcance administrativo (RF-049) ----------------------------------------------

    [Fact]
    public async Task El_listado_filtra_por_area_y_por_estado()
    {
        var escenario = await MontarAsync();

        var activo = await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);
        var inactivo = await escenario.CrearPermisoAsync(
            escenario.Peticion(AlcancePermiso.UNIDAD_ORGANIZATIVA, estado: Estado.INACTIVO));

        var otraArea = await escenario.CrearAreaAsync(escenario.PrincipalA.Id, "Otra planta");
        var deOtraArea = await escenario.CrearPermisoAsync(
            escenario.Peticion(AlcancePermiso.PERSONA, areaId: otraArea.Id));

        var delArea = await ListarAsync(escenario.Cliente, $"areaAccesoId={escenario.Area.Id}");
        delArea.Items.Select(p => p.Id).Should().BeEquivalentTo([activo.Id, inactivo.Id]);
        delArea.Total.Should().Be(2);

        var soloActivos = await ListarAsync(
            escenario.Cliente, $"areaAccesoId={escenario.Area.Id}&estado=ACTIVO");
        soloActivos.Items.Select(p => p.Id).Should().BeEquivalentTo([activo.Id]);

        var porPersona = await ListarAsync(escenario.Cliente, $"personaId={escenario.Persona.Id}");
        porPersona.Items.Select(p => p.Id).Should().BeEquivalentTo([activo.Id, deOtraArea.Id]);
    }

    [Fact]
    public async Task Un_usuario_sin_alcance_sobre_la_principal_del_area_no_ve_ni_crea_permisos()
    {
        var escenario = await MontarAsync();
        var permiso = await escenario.CrearPermisoAsync(AlcancePermiso.PERSONA);

        // Tiene la Principal B y la contratista, pero no la dueña del área (Principal A).
        using var ajeno = await fixture.Api.ClienteDeAreasAsync(
            escenario.PrincipalB.Id, escenario.Contratista.Id);

        using (var lectura = await ajeno.GetAsync(
                   new Uri($"/api/permisos/{permiso.Id}", UriKind.Relative)))
        {
            lectura.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var listado = await ListarAsync(ajeno, $"areaAccesoId={escenario.Area.Id}");
        listado.Items.Should().BeEmpty();
        listado.Total.Should().Be(0);

        using (var alta = await ajeno.PostAsJsonAsync(
                   new Uri("/api/permisos", UriKind.Relative),
                   escenario.Peticion(AlcancePermiso.PERSONA),
                   ApiFactory.Json))
        {
            alta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        using (var cambio = await ajeno.PutAsJsonAsync(
                   new Uri($"/api/permisos/{permiso.Id}", UriKind.Relative),
                   escenario.Peticion(AlcancePermiso.PERSONA, estado: Estado.INACTIVO),
                   ApiFactory.Json))
        {
            cambio.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // El intento fallido no alteró nada.
        var intacto = await escenario.Cliente.GetFromJsonAsync<PermisoAccesoDto>(
            new Uri($"/api/permisos/{permiso.Id}", UriKind.Relative), ApiFactory.Json);
        intacto!.Estado.Should().Be(Estado.ACTIVO);
    }

    [Fact]
    public async Task El_alcance_se_evalua_contra_el_area_y_no_contra_el_sujeto()
    {
        // RF-049: quien administra el área de la Principal A puede otorgar acceso a una contratista
        // aunque no la tenga en su alcance.
        var escenario = await MontarAsync();

        using var soloPrincipalA = await fixture.Api.ClienteDeAreasAsync(escenario.PrincipalA.Id);

        using var respuesta = await soloPrincipalA.PostAsJsonAsync(
            new Uri("/api/permisos", UriKind.Relative),
            escenario.Peticion(AlcancePermiso.COMPANIA, sujetoId: escenario.Contratista.Id),
            ApiFactory.Json);

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Un_permiso_sobre_un_area_inexistente_devuelve_404()
    {
        var escenario = await MontarAsync();

        using var respuesta = await escenario.PostPermisoAsync(
            escenario.Peticion(AlcancePermiso.PERSONA, areaId: Guid.CreateVersion7()));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Utilidades --------------------------------------------------------------------------------

    private static async Task EsperarProblemaAsync(
        HttpResponseMessage respuesta,
        HttpStatusCode estado,
        string codigo)
    {
        respuesta.StatusCode.Should().Be(estado);

        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ApiFactory.Json);
        problema!.Extensions["codigo"]!.ToString().Should().Be(codigo);
    }

    private static async Task<PaginaResponse<PermisoAccesoDto>> ListarAsync(
        HttpClient cliente,
        string consulta) =>
        (await cliente.GetFromJsonAsync<PaginaResponse<PermisoAccesoDto>>(
            new Uri($"/api/permisos?{consulta}", UriKind.Relative), ApiFactory.Json))!;

    private async Task<List<PermisoAcceso>> PermisosDelAreaAsync(Guid areaId)
    {
        List<PermisoAcceso> resultado = [];

        await fixture.Api.ConDbContextAsync(async db => resultado = await db.Set<PermisoAcceso>()
            .AsNoTracking()
            .Where(p => p.AreaAccesoId == areaId)
            .ToListAsync());

        return resultado;
    }
}

internal static class PermisoAccesoDtoExtensions
{
    public static Guid? SujetoDe(this PermisoAccesoDto permiso, AlcancePermiso alcance) => alcance switch
    {
        AlcancePermiso.PERSONA => permiso.PersonaId,
        AlcancePermiso.UNIDAD_ORGANIZATIVA => permiso.UnidadOrganizativaId,
        _ => permiso.CompaniaId,
    };
}
