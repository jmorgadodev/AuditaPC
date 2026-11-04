namespace AuditaPC.Core.Model;

/// <summary>
/// Resultado de una comprobación. Los valores originales se conservan para
/// mantener compatibilidad con auditorías guardadas en versiones anteriores.
/// </summary>
public enum CheckStatus
{
    Verified = 0,
    Detected = 1,
    NotDetected = 2,
    Problem = 3,
    Partial = 4,
    NotVerifiable = 5,
    PermissionDenied = 6,
    NotApplicable = 7,
    QueryError = 8,
    Timeout = 9,
    RequiresElevation = 10,
    NotTestedOffline = 11,
    KnownLimitation = 12
}

public static class CheckStatusExtensions
{
    public static string ToDisplayText(this CheckStatus status) => status switch
    {
        CheckStatus.Verified => "Verificado",
        CheckStatus.Detected => "Detectado",
        CheckStatus.NotDetected => "No detectado",
        CheckStatus.Problem => "Problema",
        CheckStatus.Partial => "Parcial",
        CheckStatus.NotVerifiable => "No verificable",
        CheckStatus.RequiresElevation => "Requiere administrador",
        CheckStatus.PermissionDenied => "Acceso denegado",
        CheckStatus.NotTestedOffline => "No evaluado sin conexión",
        CheckStatus.KnownLimitation => "Alcance limitado",
        CheckStatus.NotApplicable => "No aplicable",
        CheckStatus.QueryError => "Error de consulta",
        CheckStatus.Timeout => "Tiempo agotado",
        _ => "Desconocido"
    };

    public static bool RequiresReview(this CheckStatus status) =>
        status is CheckStatus.Problem or CheckStatus.Partial or CheckStatus.NotVerifiable
            or CheckStatus.RequiresElevation or CheckStatus.PermissionDenied
            or CheckStatus.QueryError or CheckStatus.Timeout;

    public static bool IsIncomplete(this CheckStatus status) =>
        status is CheckStatus.NotVerifiable or CheckStatus.RequiresElevation
            or CheckStatus.PermissionDenied or CheckStatus.QueryError
            or CheckStatus.Timeout or CheckStatus.Partial;

    public static bool IsFailure(this CheckStatus status) =>
        status is CheckStatus.RequiresElevation or CheckStatus.PermissionDenied
            or CheckStatus.QueryError or CheckStatus.Timeout;

    public static bool IsNegative(this CheckStatus status) =>
        status is CheckStatus.NotDetected or CheckStatus.NotApplicable;

    public static int Severity(this CheckStatus status) => status switch
    {
        CheckStatus.Problem => 100,
        CheckStatus.QueryError => 85,
        CheckStatus.Timeout => 80,
        CheckStatus.PermissionDenied => 75,
        CheckStatus.RequiresElevation => 70,
        CheckStatus.NotVerifiable => 60,
        CheckStatus.Partial => 55,
        CheckStatus.Detected => 40,
        CheckStatus.NotTestedOffline => 38,
        CheckStatus.KnownLimitation => 35,
        CheckStatus.Verified => 30,
        CheckStatus.NotDetected => 20,
        CheckStatus.NotApplicable => 10,
        _ => 0
    };
}
