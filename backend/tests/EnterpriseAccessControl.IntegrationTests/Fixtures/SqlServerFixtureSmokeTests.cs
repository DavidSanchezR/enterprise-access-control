using EnterpriseAccessControl.Domain.Entities;
using EnterpriseAccessControl.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EnterpriseAccessControl.IntegrationTests.Fixtures;

/// <summary>
/// Verifica que la infraestructura de pruebas de la fase Foundational funciona de extremo a extremo
/// antes de que las historias de usuario dependan de ella (T023).
/// </summary>
[Collection(SqlServerFixtureDefinition.Name)]
public sealed class SqlServerFixtureSmokeTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Las_migraciones_se_aplican_sobre_el_contenedor()
    {
        await using var db = fixture.CrearDbContext();

        var aplicadas = await db.Database.GetAppliedMigrationsAsync();

        aplicadas.Should().NotBeEmpty("el fixture debe crear el esquema aplicando migraciones reales");
    }

    [Fact]
    public async Task El_interceptor_de_auditoria_estampa_los_metadatos_sin_intervencion_manual()
    {
        var usuarioId = Guid.CreateVersion7();
        await using var db = fixture.CrearDbContext(usuarioId);

        var compania = new Compania
        {
            Nombre = "Minera ABC",
            TipoDocumentoId = Guid.CreateVersion7(),
            NumeroDocumento = $"RUC{Random.Shared.Next(100000, 999999)}",
            TipoCompania = TipoCompania.PRINCIPAL_MANDANTE,
        };

        db.Set<Compania>().Add(compania);
        await db.SaveChangesAsync();

        // CS-005: el estampado ocurre sin que el caso de uso lo solicite (Principio III).
        compania.CreatedAt.Should().NotBe(default);
        compania.UpdatedAt.Should().NotBe(default);
        compania.CreatedById.Should().Be(usuarioId);
        compania.UpdatedById.Should().Be(usuarioId);
    }

    [Fact]
    public async Task El_enum_de_negocio_se_persiste_como_texto_legible()
    {
        await using var db = fixture.CrearDbContext();

        var compania = new Compania
        {
            Nombre = "Servicios ACME",
            TipoDocumentoId = Guid.CreateVersion7(),
            NumeroDocumento = $"RUC{Random.Shared.Next(100000, 999999)}",
            TipoCompania = TipoCompania.CONTRATISTA,
        };

        db.Set<Compania>().Add(compania);
        await db.SaveChangesAsync();

        // research.md §17: el valor guardado debe ser el literal del contrato, no un entero.
        var valorEnBd = await db.Database
            .SqlQuery<string>($"SELECT TipoCompania AS Value FROM Compania WHERE Id = {compania.Id}")
            .SingleAsync();

        valorEnBd.Should().Be("CONTRATISTA");
    }

    [Fact]
    public async Task El_documento_de_compania_es_unico_por_tipo_y_numero()
    {
        await using var db = fixture.CrearDbContext();

        var tipoDocumentoId = Guid.CreateVersion7();
        var numero = $"RUC{Random.Shared.Next(100000, 999999)}";

        db.Set<Compania>().Add(new Compania
        {
            Nombre = "Primera",
            TipoDocumentoId = tipoDocumentoId,
            NumeroDocumento = numero,
            TipoCompania = TipoCompania.PRINCIPAL_MANDANTE,
        });
        await db.SaveChangesAsync();

        db.Set<Compania>().Add(new Compania
        {
            Nombre = "Duplicada",
            TipoDocumentoId = tipoDocumentoId,
            NumeroDocumento = numero,
            TipoCompania = TipoCompania.CONTRATISTA,
        });

        var accion = async () => await db.SaveChangesAsync();

        await accion.Should().ThrowAsync<DbUpdateException>(
            "el índice único (TipoDocumentoId, NumeroDocumento) debe rechazar el duplicado");
    }
}
