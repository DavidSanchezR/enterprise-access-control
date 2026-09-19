using EnterpriseAccessControl.ContractTests.Infraestructura;
using EnterpriseAccessControl.Domain.Enums;
using FluentAssertions;

namespace EnterpriseAccessControl.ContractTests;

/// <summary>
/// Conformidad de <c>contracts/credentials.yaml</c> con el modelo de dominio (RF-018, RF-056 a
/// RF-058, RF-061, RF-070, RF-071).
/// </summary>
/// <remarks>
/// Verifica el contrato contra el dominio —los valores del estado, la obligatoriedad de la fecha de
/// fin, la presencia del origen de revocación—. La conformidad de rutas y esquemas contra la API
/// publicada por la Historia 9 está en <see cref="CredencialesApiContractTests"/>.
/// </remarks>
public sealed class CredencialesContractTests
{
    private static readonly ContratoOpenApi Contrato =
        ApiContratoFixture.CargarContrato("credentials.yaml");

    [Fact]
    public void El_contrato_declara_las_operaciones_de_credencial_de_la_Historia_9()
    {
        // Se fija aquí la superficie que US9 deberá implementar, para que ninguna de las tres se
        // pierda por el camino.
        Contrato.Operaciones.Should().BeEquivalentTo(
        [
            ("/api/personas/{personaId}/credenciales", "get"),
            ("/api/personas/{personaId}/credenciales", "post"),
            ("/api/personas/{personaId}/credenciales/{id}/devolver", "post"),
            ("/api/personas/{personaId}/credenciales/{id}", "delete"),
        ]);
    }

    [Fact]
    public void El_estado_del_contrato_coincide_exactamente_con_el_enumerado_del_dominio()
    {
        // Los valores viajan como literales de texto y son los mismos que se persisten en nvarchar:
        // si el contrato y el dominio divergieran, la traducción fallaría en silencio.
        var enElContrato = Contrato.PropiedadesPorEsquema.ContainsKey("EstadoCredencial")
            ? []
            : LeerEnumDelContrato();

        var enElDominio = Enum.GetNames<EstadoCredencial>();

        enElContrato.Should().BeEquivalentTo(enElDominio);
    }

    [Fact]
    public void El_estado_incluye_REVOCADA_como_valor_propio()
    {
        // RF-061: la revocación en cascada necesita un estado distinguible de DEVUELTO (devolución
        // voluntaria) y de ELIMINADO (baja administrativa). Sin él, la auditoría no podría explicar
        // por qué se perdió el acceso.
        LeerEnumDelContrato().Should().Contain("REVOCADA");

        Enum.IsDefined(EstadoCredencial.REVOCADA).Should().BeTrue();
    }

    [Fact]
    public void La_credencial_se_emite_en_el_contexto_de_una_compania_principal()
    {
        // RF-056: una credencial pertenece a una Principal concreta; sin ese campo no podría
        // distinguirse cuál de los contextos simultáneos de la persona la respalda.
        Contrato.PropiedadesPorEsquema["AsignacionCredencial"]
            .Should().Contain("companiaPrincipalId");

        Contrato.PropiedadesPorEsquema["AsignacionCredencialRequest"]
            .Should().Contain("companiaPrincipalId");
    }

    [Fact]
    public void La_fecha_de_fin_es_obligatoria_en_el_alta()
    {
        // RF-071: no existe credencial de vigencia indefinida. Antes de esa decisión, el campo era
        // opcional; esta prueba impide que vuelva a serlo por arrastre.
        var crudo = File.ReadAllText(RutaContrato());

        var bloque = crudo[crudo.IndexOf("AsignacionCredencialRequest:", StringComparison.Ordinal)..];
        var requeridos = bloque[..bloque.IndexOf("properties:", StringComparison.Ordinal)];

        requeridos.Should().Contain("fechaHoraFin");
    }

    [Fact]
    public void Asignar_no_cierra_automaticamente_la_credencial_previa()
    {
        // Sesión 2026-09-15, decisión A: el contrato llegó a decir que el POST "cierra automáticamente
        // cualquier credencial ASIGNADO previa". Esta prueba impide que esa cláusula reaparezca y fija
        // que el solapamiento se rechaza con 409.
        var crudo = File.ReadAllText(RutaContrato());

        var post = crudo[crudo.IndexOf("  /api/personas/{personaId}/credenciales:", StringComparison.Ordinal)..];
        post = post[..post.IndexOf("  /api/personas/{personaId}/credenciales/{id}/devolver:", StringComparison.Ordinal)];

        post.Should().NotContain("Cierra automáticamente");
        post.Should().Contain("NO cierra, finaliza, devuelve, elimina ni revoca");
        post.Should().Contain("SOLAPAMIENTO_VIGENCIA");
        Contrato.Estados[("/api/personas/{personaId}/credenciales", "post")].Should().Contain("409");
    }

    [Fact]
    public void La_credencial_registra_la_pertenencia_que_la_revoco()
    {
        // research.md §14.2: responde "qué credenciales revocó esta pertenencia" sin heurísticas de
        // fecha o compañía.
        Contrato.PropiedadesPorEsquema["AsignacionCredencial"]
            .Should().Contain("revocadoPorPertenenciaId");
    }

    [Fact]
    public void El_alta_no_acepta_estado_ni_origen_de_revocacion()
    {
        // Ambos son consecuencia de acciones del sistema, no datos que el cliente pueda fijar:
        // aceptarlos permitiría crear una credencial ya "revocada" o atribuirla a una pertenencia
        // ajena.
        var enElAlta = Contrato.PropiedadesPorEsquema["AsignacionCredencialRequest"];

        enElAlta.Should().NotContain("estado");
        enElAlta.Should().NotContain("revocadoPorPertenenciaId");
        enElAlta.Should().NotContain("personaId", "la persona viaja en la ruta, no en el cuerpo");
    }

    [Fact]
    public void Ningun_campo_representa_un_identificador_fisico()
    {
        // RF-058: TipoCredencial es el diseño visual del fotocheck, nunca una tecnología de
        // identificación física. El contrato SÍ nombra RFID, QR y NFC, pero lo hace en su
        // descripción para excluirlas explícitamente; lo que no debe existir es un *campo* que las
        // represente, porque eso volvería a meterlas en el modelo.
        var campos = Contrato.PropiedadesPorEsquema["AsignacionCredencial"]
            .Concat(Contrato.PropiedadesPorEsquema["AsignacionCredencialRequest"])
            .ToList();

        foreach (var sospechoso in new[] { "rfid", "nfc", "qr", "barras", "identificadorfisico", "serie" })
        {
            campos.Should().NotContain(
                campo => campo.Contains(sospechoso, StringComparison.OrdinalIgnoreCase),
                "RF-058 retiró del alcance todo identificador físico de credencial");
        }
    }

    [Fact]
    public void El_contrato_declara_explicitamente_la_exclusion_de_RF_058()
    {
        // La exclusión está redactada en el contrato a propósito: es la corrección de una lectura
        // previa que sí modelaba la tecnología física. Que el texto siga ahí evita que se reintroduzca
        // por olvido.
        var crudo = File.ReadAllText(RutaContrato());

        crudo.Should().Contain("RF-058");
        crudo.Should().Contain("tecnología de identificación física");
    }

    private static List<string> LeerEnumDelContrato()
    {
        // El enumerado no tiene "properties", así que no aparece en PropiedadesPorEsquema: se lee
        // del texto del contrato.
        var crudo = File.ReadAllText(RutaContrato());

        var inicio = crudo.IndexOf("enum: [ASIGNADO", StringComparison.Ordinal);
        inicio.Should().BeGreaterThan(-1, "el contrato debe declarar EstadoCredencial");

        var fin = crudo.IndexOf(']', inicio);

        return [.. crudo[(inicio + "enum: [".Length)..fin]
            .Split(',')
            .Select(v => v.Trim())];
    }

    private static string RutaContrato()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            var candidato = Path.Combine(
                directorio.FullName,
                "specs", "001-control-acceso-empresarial", "contracts", "credentials.yaml");

            if (File.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        throw new FileNotFoundException("No se encontró contracts/credentials.yaml.");
    }
}
