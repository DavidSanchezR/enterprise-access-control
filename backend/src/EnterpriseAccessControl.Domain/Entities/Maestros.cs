using EnterpriseAccessControl.Domain.Common;

namespace EnterpriseAccessControl.Domain.Entities;

/// <summary>
/// Tipo de documento de identificación (RF-031). Semilla inicial para Perú: DNI, Carné de
/// Extranjería, Pasaporte y RUC — este último para <see cref="Compania"/>.
/// </summary>
public class TipoDocumento : EntidadMaestra;

/// <summary>Tipo de sangre (RF-012). Semilla inicial: O+, O-, A+, A-, B+, B-, AB+, AB-.</summary>
public class TipoSangre : EntidadMaestra;

/// <summary>Género (RF-012). Semilla inicial: Masculino, Femenino.</summary>
public class Genero : EntidadMaestra;

/// <summary>
/// Perfil de persona, por ejemplo Trabajador, Visitante o Proveedor (RF-010).
/// </summary>
/// <remarks>
/// No se entrega con semilla: los perfiles dependen de cómo organice el trabajo cada empresa, y
/// precargar un conjunto supondría una decisión de negocio que la especificación no toma.
/// </remarks>
public class TipoPersona : EntidadMaestra;

/// <summary>
/// Tipo o diseño visual de la credencial: "Credencial Contratista", "Credencial Visitante"…
/// (RF-017, RF-058).
/// </summary>
/// <remarks>
/// **Nunca** representa una tecnología de identificación física (RFID, QR, NFC, código de barras) ni
/// un identificador físico: esa interpretación quedó explícitamente retirada del alcance
/// (RF-058, research.md §9). La impresión y el diseño gráfico están fuera del sistema.
/// </remarks>
public class TipoCredencial : EntidadMaestra;
