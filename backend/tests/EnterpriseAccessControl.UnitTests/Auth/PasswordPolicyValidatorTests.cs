using EnterpriseAccessControl.Application.Auth;
using EnterpriseAccessControl.Application.Common.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EnterpriseAccessControl.UnitTests.Auth;

/// <summary>
/// Política de complejidad de contraseña (RF-003, research.md §2), sin base de datos.
/// </summary>
/// <remarks>
/// Los umbrales se inyectan explícitamente en cada prueba en lugar de usar los valores por defecto:
/// así una futura decisión de negocio (Decisiones Pendientes #1) que cambie la configuración no
/// invalida estas pruebas, que verifican el comportamiento del validador, no los números elegidos.
/// </remarks>
public sealed class PasswordPolicyValidatorTests
{
    private static PasswordPolicyValidator Crear(PasswordPolicyOptions? opciones = null) =>
        new(Options.Create(opciones ?? new PasswordPolicyOptions()));

    [Fact]
    public void Validar_contrasena_que_cumple_todos_los_requisitos_es_valida()
    {
        var resultado = Crear().Validar("Contrasena1Segura");

        resultado.EsValida.Should().BeTrue();
        resultado.Errores.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_contrasena_vacia_o_en_blanco_es_invalida(string? password)
    {
        var resultado = Crear().Validar(password);

        resultado.EsValida.Should().BeFalse();
        resultado.Errores.Should().ContainSingle()
            .Which.Should().Contain("obligatoria");
    }

    [Fact]
    public void Validar_contrasena_mas_corta_que_el_minimo_es_invalida()
    {
        var resultado = Crear(new PasswordPolicyOptions { LongitudMinima = 12 }).Validar("Abcdefg1h");

        resultado.EsValida.Should().BeFalse();
        resultado.Errores.Should().Contain(e => e.Contains("12", StringComparison.Ordinal));
    }

    [Fact]
    public void Validar_contrasena_exactamente_en_el_minimo_es_valida()
    {
        // Frontera inclusiva: la regla es "al menos N", no "más de N".
        var resultado = Crear(new PasswordPolicyOptions { LongitudMinima = 10 }).Validar("Abcdefgh1i");

        resultado.EsValida.Should().BeTrue();
    }

    [Fact]
    public void Validar_contrasena_sin_mayuscula_es_invalida()
    {
        var resultado = Crear().Validar("contrasena1segura");

        resultado.EsValida.Should().BeFalse();
        resultado.Errores.Should().Contain(e => e.Contains("mayúscula", StringComparison.Ordinal));
    }

    [Fact]
    public void Validar_contrasena_sin_minuscula_es_invalida()
    {
        var resultado = Crear().Validar("CONTRASENA1SEGURA");

        resultado.EsValida.Should().BeFalse();
        resultado.Errores.Should().Contain(e => e.Contains("minúscula", StringComparison.Ordinal));
    }

    [Fact]
    public void Validar_contrasena_sin_digito_es_invalida()
    {
        var resultado = Crear().Validar("ContrasenaSegura");

        resultado.EsValida.Should().BeFalse();
        resultado.Errores.Should().Contain(e => e.Contains("dígito", StringComparison.Ordinal));
    }

    [Fact]
    public void Validar_acumula_todos_los_incumplimientos_en_una_sola_pasada()
    {
        // El usuario debe ver de una vez todo lo que le falta, no corregir un requisito por intento.
        var resultado = Crear().Validar("corta");

        resultado.EsValida.Should().BeFalse();
        resultado.Errores.Should().HaveCount(3); // longitud, mayúscula y dígito
    }

    [Fact]
    public void Validar_no_exige_los_requisitos_de_complejidad_desactivados()
    {
        var opciones = new PasswordPolicyOptions
        {
            LongitudMinima = 8,
            RequiereMayuscula = false,
            RequiereMinuscula = false,
            RequiereDigito = false,
        };

        var resultado = Crear(opciones).Validar("aaaaaaaa");

        resultado.EsValida.Should().BeTrue();
    }
}
