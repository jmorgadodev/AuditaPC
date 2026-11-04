namespace AuditaPC.Core.Model;

/// <summary>Vista no genérica de una categoría, para resúmenes e informes.</summary>
public interface ICategoryResult
{
    string Id { get; }
    string Title { get; }
    CheckStatus Status { get; }
    string? Message { get; }
    IReadOnlyList<CheckRecord> Checks { get; }
    long DurationMs { get; }
    string? ErrorType { get; }
}

/// <summary>
/// Resultado de una categoría. El estado de la categoría y los estados de cada
/// comprobación se conservan por separado para no ocultar fallos parciales.
/// </summary>
public sealed class CategoryResult<T> : ICategoryResult where T : class
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public CheckStatus Status { get; set; } = CheckStatus.NotVerifiable;
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<CheckRecord> Checks { get; set; } = new();
    public long DurationMs { get; set; }
    public string? ErrorType { get; set; }

    IReadOnlyList<CheckRecord> ICategoryResult.Checks => Checks;

    public static CategoryResult<T> Unavailable(
        string id,
        string title,
        CheckStatus status,
        string message,
        string? errorType = null,
        long durationMs = 0)
    {
        return new CategoryResult<T>
        {
            Id = id,
            Title = title,
            Status = status,
            Message = message,
            ErrorType = errorType,
            DurationMs = durationMs,
            Checks = new List<CheckRecord>
            {
                new()
                {
                    Id = $"{id}.unavailable",
                    Label = "Consulta de la categoría",
                    Status = status,
                    Detail = message,
                    ErrorType = errorType
                }
            }
        };
    }
}
