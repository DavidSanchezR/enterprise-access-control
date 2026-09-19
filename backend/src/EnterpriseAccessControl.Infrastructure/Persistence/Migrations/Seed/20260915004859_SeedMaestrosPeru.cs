using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAccessControl.Infrastructure.Persistence.Migrations.Seed
{
    /// <summary>
    /// Carga inicial versionada de los catálogos maestros para Perú (RF-031, research.md §8).
    /// </summary>
    /// <remarks>
    /// Se entrega como migración y no como script SQL suelto para que el catálogo evolucione junto
    /// con el esquema, se aplique igual en cualquier entorno —incluido el contenedor de las pruebas
    /// de integración— y quede auditado en el control de versiones.
    ///
    /// Los identificadores son literales fijos, no generados: una semilla debe producir las mismas
    /// filas en todos los entornos para que las referencias entre entornos y los datos de prueba
    /// sigan siendo válidos. Conservan la forma UUID v7 (dígito de versión 7, variante 8) por
    /// coherencia con el resto del modelo (Principio II).
    ///
    /// <c>TipoPersona</c> y <c>TipoCredencial</c> no se siembran: sus valores dependen de cómo
    /// organice el trabajo cada empresa y precargarlos supondría tomar una decisión de negocio que la
    /// especificación no toma (data-model.md, contracts/masters.yaml).
    /// </remarks>
    public partial class SeedMaestrosPeru : Migration
    {
        /// <summary>Instante de la semilla, fijo para que la migración sea determinista.</summary>
        private static readonly DateTime Sembrado = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly string[] Columnas =
            ["Id", "Nombre", "Estado", "CreatedAt", "UpdatedAt", "CreatedById", "UpdatedById"];

        /// <summary>
        /// Construye la matriz rectangular de valores que espera <c>InsertData</c> para varias filas.
        /// </summary>
        /// <remarks>
        /// La sobrecarga multi-fila exige <c>object[,]</c> (matriz rectangular), no <c>object[][]</c>
        /// (escalonada): pasar la segunda compila sin quejarse pero falla al generar el SQL, porque
        /// EF interpreta cada fila como un único valor y ve 4 valores para 7 columnas.
        /// </remarks>
        private static object[,] Filas(params (string Id, string Nombre)[] valores)
        {
            var matriz = new object[valores.Length, Columnas.Length];

            for (var i = 0; i < valores.Length; i++)
            {
                matriz[i, 0] = new Guid(valores[i].Id);
                matriz[i, 1] = valores[i].Nombre;
                matriz[i, 2] = "ACTIVO";
                matriz[i, 3] = Sembrado;
                matriz[i, 4] = Sembrado;

                // La semilla no la ejecuta ningún usuario: la autoría queda nula a propósito, en vez
                // de atribuirla a un identificador ficticio que luego nadie podría interpretar.
                matriz[i, 5] = null;
                matriz[i, 6] = null;
            }

            return matriz;
        }

        // --- Tipos de documento (RF-031) ---
        private const string DniId = "0199b0d0-0001-7000-8000-000000000001";
        private const string CarneExtranjeriaId = "0199b0d0-0001-7000-8000-000000000002";
        private const string PasaporteId = "0199b0d0-0001-7000-8000-000000000003";
        private const string RucId = "0199b0d0-0001-7000-8000-000000000004";

        // --- Tipos de sangre ---
        private const string OPositivoId = "0199b0d0-0002-7000-8000-000000000001";
        private const string ONegativoId = "0199b0d0-0002-7000-8000-000000000002";
        private const string APositivoId = "0199b0d0-0002-7000-8000-000000000003";
        private const string ANegativoId = "0199b0d0-0002-7000-8000-000000000004";
        private const string BPositivoId = "0199b0d0-0002-7000-8000-000000000005";
        private const string BNegativoId = "0199b0d0-0002-7000-8000-000000000006";
        private const string AbPositivoId = "0199b0d0-0002-7000-8000-000000000007";
        private const string AbNegativoId = "0199b0d0-0002-7000-8000-000000000008";

        // --- Géneros ---
        private const string MasculinoId = "0199b0d0-0003-7000-8000-000000000001";
        private const string FemeninoId = "0199b0d0-0003-7000-8000-000000000002";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RUC identifica a una Compañía, no a una Persona; vive en el mismo catálogo porque la
            // unicidad de documento se modela igual en ambas (data-model.md).
            migrationBuilder.InsertData(
                table: "TipoDocumento",
                columns: Columnas,
                values: Filas(
                    (DniId, "DNI"),
                    (CarneExtranjeriaId, "Carné de Extranjería"),
                    (PasaporteId, "Pasaporte"),
                    (RucId, "RUC")));

            migrationBuilder.InsertData(
                table: "TipoSangre",
                columns: Columnas,
                values: Filas(
                    (OPositivoId, "O+"),
                    (ONegativoId, "O-"),
                    (APositivoId, "A+"),
                    (ANegativoId, "A-"),
                    (BPositivoId, "B+"),
                    (BNegativoId, "B-"),
                    (AbPositivoId, "AB+"),
                    (AbNegativoId, "AB-")));

            migrationBuilder.InsertData(
                table: "Genero",
                columns: Columnas,
                values: Filas(
                    (MasculinoId, "Masculino"),
                    (FemeninoId, "Femenino")));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo se retiran las filas de esta semilla, por identificador: cualquier valor que la
            // empresa haya añadido después debe sobrevivir a una reversión del esquema.
            BorrarPorId(migrationBuilder, "Genero", [MasculinoId, FemeninoId]);

            BorrarPorId(
                migrationBuilder,
                "TipoSangre",
                [
                    OPositivoId, ONegativoId, APositivoId, ANegativoId,
                    BPositivoId, BNegativoId, AbPositivoId, AbNegativoId,
                ]);

            BorrarPorId(
                migrationBuilder,
                "TipoDocumento",
                [DniId, CarneExtranjeriaId, PasaporteId, RucId]);
        }

        private static void BorrarPorId(MigrationBuilder migrationBuilder, string tabla, string[] ids)
        {
            foreach (var id in ids)
            {
                migrationBuilder.DeleteData(table: tabla, keyColumn: "Id", keyValue: new Guid(id));
            }
        }
    }
}
