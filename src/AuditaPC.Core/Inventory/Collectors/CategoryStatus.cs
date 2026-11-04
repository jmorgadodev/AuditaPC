using AuditaPC.Core.Model;

namespace AuditaPC.Core.Inventory.Collectors;

public static class CategoryStatus
{
    public static CheckStatus Aggregate(
        IEnumerable<CheckRecord> checks,
        bool dataObtained,
        bool primaryNegative = false)
    {
        var list = checks.ToList();

        if (list.Any(c => c.Status == CheckStatus.Problem))
        {
            return CheckStatus.Problem;
        }

        if (dataObtained)
        {
            if (primaryNegative)
            {
                return CheckStatus.NotDetected;
            }

            return list.Any(c => c.Status.IsIncomplete())
                ? CheckStatus.Partial
                : CheckStatus.Verified;
        }

        if (list.Any(c => c.Status == CheckStatus.QueryError))
        {
            return CheckStatus.QueryError;
        }

        if (list.Any(c => c.Status == CheckStatus.Timeout))
        {
            return CheckStatus.Timeout;
        }

        if (list.Any(c => c.Status == CheckStatus.PermissionDenied))
        {
            return CheckStatus.PermissionDenied;
        }

        if (list.Any(c => c.Status == CheckStatus.RequiresElevation))
        {
            return CheckStatus.RequiresElevation;
        }

        if (list.Any(c => c.Status == CheckStatus.NotVerifiable))
        {
            return CheckStatus.NotVerifiable;
        }

        if (list.Count > 0 && list.All(c => c.Status is CheckStatus.NotTestedOffline or CheckStatus.KnownLimitation))
        {
            return list.Any(c => c.Status == CheckStatus.NotTestedOffline)
                ? CheckStatus.NotTestedOffline
                : CheckStatus.KnownLimitation;
        }

        if (list.Count > 0 && list.All(c => c.Status == CheckStatus.NotApplicable))
        {
            return CheckStatus.NotApplicable;
        }

        if (list.Any(c => c.Status == CheckStatus.NotDetected))
        {
            return CheckStatus.NotDetected;
        }

        return CheckStatus.NotVerifiable;
    }
}
