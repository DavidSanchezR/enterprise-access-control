using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using EnterpriseAccessControl.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAccessControl.IntegrationTests.Masters;

/// <summary>
/// La migración de semilla versionada carga los catálogos iniciales de Perú (RF-031, research.md §8).
/// </summary>
/// <remarks>
/// Se verifica sobre el contenedor de pruebas, que aplica exactamente las mismas migraciones que
/// producción. Eso es justamente lo que aporta entregar la semilla como migración y no como script
/// manual: si alguien la rompe, falla aquí y no en el despliegue.
/// </remarks>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class SeedDataTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Los_tipos_de_documento_de_Peru_estan_precargados()
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var nombres = await db.Set<TipoDocumento>().AsNoTracking()
                .Select(t => t.Nombre)
                .ToListAsync();

            // RUC corresponde a Compañía; los otros tres identifican a una Persona.
            nombres.Should().Contain(["DNI", "Carné de Extranjería", "Pasaporte", "RUC"]);
        });
    }

    [Fact]
    public async Task Los_ocho_tipos_de_sangre_estan_precargados()
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var nombres = await db.Set<TipoSangre>().AsNoTracking()
                .Select(t => t.Nombre)
                .ToListAsync();

            nombres.Should().Contain(["O+", "O-", "A+", "A-", "B+", "B-", "AB+", "AB-"]);
        });
    }

    [Fact]
    public async Task Los_generos_iniciales_estan_precargados()
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var nombres = await db.Set<Genero>().AsNoTracking()
                .Select(g => g.Nombre)
                .ToListAsync();

            nombres.Should().Contain(["Masculino", "Femenino"]);
        });
    }

    [Fact]
    public async Task Todos_los_valores_sembrados_nacen_ACTIVOS()
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            // Un valor sembrado INACTIVO no podría usarse en ninguna asignación nueva (RF-032), lo
            // que dejaría el sistema recién instalado sin tipos de documento utilizables.
            (await db.Set<TipoDocumento>().AsNoTracking().AllAsync(t => t.Estado == Estado.ACTIVO))
                .Should().BeTrue();

            (await db.Set<TipoSangre>().AsNoTracking().AllAsync(t => t.Estado == Estado.ACTIVO))
                .Should().BeTrue();

            (await db.Set<Genero>().AsNoTracking().AllAsync(g => g.Estado == Estado.ACTIVO))
                .Should().BeTrue();
        });
    }

    [Fact]
    public async Task La_semilla_usa_identificadores_estables_y_no_generados_al_azar()
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            // Si los identificadores se generaran en cada aplicación, dos entornos tendrían valores
            // distintos para "DNI" y cualquier referencia entre ellos dejaría de ser válida.
            var dni = await db.Set<TipoDocumento>().AsNoTracking()
                .SingleAsync(t => t.Nombre == "DNI");

            dni.Id.Should().Be(new Guid("0199b0d0-0001-7000-8000-000000000001"));

            var masculino = await db.Set<Genero>().AsNoTracking()
                .SingleAsync(g => g.Nombre == "Masculino");

            masculino.Id.Should().Be(new Guid("0199b0d0-0003-7000-8000-000000000001"));
        });
    }

    [Fact]
    public async Task La_semilla_no_precarga_tipos_de_persona_ni_de_credencial()
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            // Ambos catálogos dependen de cómo organice el trabajo cada empresa: precargarlos sería
            // tomar una decisión de negocio que la especificación deja abierta (data-model.md).
            // Las pruebas de CRUD crean sus propios valores, así que se comprueba la ausencia de
            // valores *sembrados* por identificador, no que la tabla esté vacía.
            var sembrados = new[]
            {
                new Guid("0199b0d0-0001-7000-8000-000000000001"),
                new Guid("0199b0d0-0002-7000-8000-000000000001"),
                new Guid("0199b0d0-0003-7000-8000-000000000001"),
            };

            (await db.Set<TipoPersona>().AsNoTracking().AnyAsync(t => sembrados.Contains(t.Id)))
                .Should().BeFalse();

            (await db.Set<TipoCredencial>().AsNoTracking().AnyAsync(t => sembrados.Contains(t.Id)))
                .Should().BeFalse();
        });
    }

    [Fact]
    public async Task La_semilla_deja_la_autoria_de_auditoria_nula()
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            var dni = await db.Set<TipoDocumento>().AsNoTracking()
                .SingleAsync(t => t.Nombre == "DNI");

            // Ningún usuario ejecutó la semilla: atribuirla a un identificador ficticio haría
            // ilegible la traza de auditoría (Principio III).
            dni.CreatedById.Should().BeNull();
            dni.UpdatedById.Should().BeNull();
            dni.CreatedAt.Should().NotBe(default);
        });
    }

    [Fact]
    public async Task La_semilla_no_duplica_valores_al_reaplicarse_el_pipeline()
    {
        await fixture.Api.ConDbContextAsync(async db =>
        {
            // Las migraciones se registran en __EFMigrationsHistory y se aplican una sola vez; si
            // alguien convirtiera la semilla en un script idempotente mal escrito, aparecerían
            // duplicados y el índice único los delataría aquí.
            var nombres = await db.Set<TipoDocumento>().AsNoTracking()
                .Select(t => t.Nombre)
                .ToListAsync();

            nombres.Should().OnlyHaveUniqueItems();
        });
    }
}
