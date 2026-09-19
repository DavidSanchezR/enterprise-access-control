using EnterpriseAccessControl.Application.Credentials;
using EnterpriseAccessControl.Application.People;
using EnterpriseAccessControl.Application.Permissions;
using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseAccessControl.IntegrationTests.Persistence;

/// <summary>
/// Revisión de las columnas de fin de vigencia obligatorias (RF-071) y del uso único de
/// <see cref="ContencionTemporalValidator"/> para la contención temporal (RF-072) — T165.
/// </summary>
/// <remarks>
/// RF-071 y RF-072 son reglas distintas y alcanzan a conjuntos distintos: la fecha de fin es
/// obligatoria en seis entidades, pero solo tres dependen temporalmente de la pertenencia. La revisión
/// fija ambos conjuntos, de modo que ampliar o reducir cualquiera de ellos sea un cambio visible.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class VigenciaObligatoriaYContencionTests(SqlServerFixture fixture)
{
    public static TheoryData<Type, string, string> ColumnasDeFin => new()
    {
        { typeof(AsignacionPersonaCompania), "AsignacionPersonaCompania", nameof(AsignacionPersonaCompania.FechaHoraFin) },
        { typeof(ContextoOperativoPersonaPrincipal), "ContextoOperativoPersonaPrincipal", nameof(ContextoOperativoPersonaPrincipal.FechaHoraFin) },
        { typeof(AsignacionPersonaUnidadOrganizativa), "AsignacionPersonaUnidadOrganizativa", nameof(AsignacionPersonaUnidadOrganizativa.FechaHoraFin) },
        { typeof(AsignacionCredencial), "AsignacionCredencial", nameof(AsignacionCredencial.FechaHoraFin) },
        { typeof(AsignacionTipoPersona), "AsignacionTipoPersona", nameof(AsignacionTipoPersona.FechaHoraFin) },
        { typeof(PermisoAcceso), "PermisoAcceso", nameof(PermisoAcceso.FechaHoraFinVigencia) },
    };

    [Theory]
    [MemberData(nameof(ColumnasDeFin))]
    public async Task La_fecha_de_fin_es_NOT_NULL_en_el_dominio_el_modelo_y_la_base_de_datos(
        Type entidad,
        string tabla,
        string columna)
    {
        // Dominio: DateTime y no DateTime?, así que ni siquiera en memoria existe la vigencia abierta.
        entidad.GetProperty(columna)!.PropertyType.Should().Be<DateTime>();

        using var ambito = fixture.Api.Services.CreateScope();
        var db = ambito.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();

        // Modelo de EF Core.
        db.Model.FindEntityType(entidad)!.FindProperty(columna)!.IsNullable.Should().BeFalse();

        // Base de datos real, construida solo con migraciones.
        var admiteNulos = await db.Database.SqlQuery<bool>($"""
            SELECT c.is_nullable AS Value
            FROM sys.columns c
            JOIN sys.tables t ON t.object_id = c.object_id
            WHERE t.name = {tabla} AND c.name = {columna}
            """).SingleAsync();

        admiteNulos.Should().BeFalse($"{tabla}.{columna} debe ser NOT NULL (RF-071)");
    }

    [Fact]
    public void Las_seis_entidades_de_RF_071_estan_todas_revisadas()
    {
        ColumnasDeFin.Should().HaveCount(6);
    }

    [Fact]
    public void RelacionContratistaPrincipal_conserva_su_fin_opcional_porque_RF_071_no_le_aplica()
    {
        // Contrapunto: vincula dos compañías, no a una persona.
        typeof(RelacionContratistaPrincipal).GetProperty(nameof(RelacionContratistaPrincipal.FechaHoraFin))!
            .PropertyType.Should().Be<DateTime?>();
    }

    public static TheoryData<Type> DependientesDeLaPertenencia => new()
    {
        typeof(ContextoOperativoService),
        typeof(AsignacionUnidadOrganizativaService),
        typeof(CredencialService),
    };

    [Theory]
    [MemberData(nameof(DependientesDeLaPertenencia))]
    public void Las_tres_dependientes_de_la_pertenencia_validan_la_contencion_con_el_validador_comun(Type servicio)
    {
        DependeDelValidador(servicio).Should().BeTrue(
            "{0} debe delegar la contención de RF-072 en ContencionTemporalValidator", servicio.Name);
    }

    public static TheoryData<Type> NoSujetosAContencion => new()
    {
        typeof(EstadoEfectivoService),
        typeof(PermisoAccesoService),
    };

    [Theory]
    [MemberData(nameof(NoSujetosAContencion))]
    public void Perfiles_y_permisos_no_aplican_contencion_porque_no_dependen_de_la_pertenencia(Type servicio)
    {
        // RF-072 excluye expresamente a AsignaciónTipoPersona y a PermisoAcceso: aplicarles la
        // contención inventaría una dependencia que el modelo de dominio no establece.
        DependeDelValidador(servicio).Should().BeFalse();
    }

    [Fact]
    public void El_validador_es_el_unico_punto_que_emite_el_error_de_contencion()
    {
        // Revisión estática sobre el código fuente de Application: el código de error de RF-072 solo
        // puede lanzarse desde el validador común. Una copia de la regla en un servicio lo delataría.
        var aplicacion = Path.Combine(RaizBackend(), "src", "EnterpriseAccessControl.Application");

        var emisores = Directory.GetFiles(aplicacion, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("CodigosError.FueraDeContencionTemporal", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        emisores.Should().BeEquivalentTo(["ContencionTemporalValidator.cs"]);
    }

    private static bool DependeDelValidador(Type servicio) =>
        servicio.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Any(p => p.ParameterType == typeof(ContencionTemporalValidator));

    private static string RaizBackend()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "EnterpriseAccessControl.slnx")))
        {
            directorio = directorio.Parent;
        }

        return directorio?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del backend.");
    }
}
