using AuditaPC.Core.Inventory.Collectors;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Tests;

public sealed class CategoryStatusTests
{
    [Fact]
    public void Problem_dominates_even_when_data_was_obtained()
    {
        var checks = new[]
        {
            CheckRecord.Verified("a", "A"),
            CheckRecord.Problem("b", "B")
        };

        Assert.Equal(CheckStatus.Problem, CategoryStatus.Aggregate(checks, dataObtained: true));
    }

    [Fact]
    public void Data_obtained_with_incomplete_checks_is_partial()
    {
        var checks = new[]
        {
            CheckRecord.Verified("a", "A"),
            CheckRecord.PermissionDenied("b", "B")
        };

        Assert.Equal(CheckStatus.Partial, CategoryStatus.Aggregate(checks, dataObtained: true));
    }

    [Fact]
    public void No_data_prefers_query_error_over_permission_denied()
    {
        var checks = new[]
        {
            CheckRecord.PermissionDenied("a", "A"),
            CheckRecord.Error("b", "B")
        };

        Assert.Equal(CheckStatus.QueryError, CategoryStatus.Aggregate(checks, dataObtained: false));
    }

    [Fact]
    public void No_data_and_only_not_applicable_is_not_applicable()
    {
        var checks = new[] { CheckRecord.NotApplicable("a", "A") };
        Assert.Equal(CheckStatus.NotApplicable, CategoryStatus.Aggregate(checks, dataObtained: false));
    }

    [Fact]
    public void No_data_and_no_checks_is_not_verifiable()
    {
        Assert.Equal(
            CheckStatus.NotVerifiable,
            CategoryStatus.Aggregate(Array.Empty<CheckRecord>(), dataObtained: false));
    }

    [Fact]
    public void No_data_with_access_denied_is_classified_as_elevation_required()
    {
        var checks = new[] { CheckRecord.RequiresElevation("a", "A", "Requiere administrador.") };
        Assert.Equal(CheckStatus.RequiresElevation, CategoryStatus.Aggregate(checks, dataObtained: false));
    }

    [Fact]
    public void Offline_checks_do_not_make_a_locally_obtained_category_incomplete()
    {
        var checks = new[]
        {
            CheckRecord.Verified("a", "A"),
            CheckRecord.NotTestedOffline("b", "B", "No se intentó sin conexión.")
        };

        Assert.Equal(CheckStatus.Verified, CategoryStatus.Aggregate(checks, dataObtained: true));
        Assert.False(CheckStatus.NotTestedOffline.IsIncomplete());
    }

    [Fact]
    public void No_data_with_only_offline_checks_is_not_tested_offline()
    {
        var checks = new[] { CheckRecord.NotTestedOffline("a", "A", "Requiere Internet.") };
        Assert.Equal(CheckStatus.NotTestedOffline, CategoryStatus.Aggregate(checks, dataObtained: false));
    }

    [Fact]
    public void Known_scope_limitations_do_not_count_as_incomplete()
    {
        Assert.False(CheckStatus.KnownLimitation.IsIncomplete());
        Assert.False(CheckStatus.KnownLimitation.RequiresReview());
    }

    [Fact]
    public void Status_helpers_are_explicit()
    {
        Assert.True(CheckStatus.Problem.RequiresReview());
        Assert.True(CheckStatus.NotVerifiable.RequiresReview());
        Assert.True(CheckStatus.Partial.IsIncomplete());
        Assert.True(CheckStatus.RequiresElevation.IsFailure());
        Assert.True(CheckStatus.PermissionDenied.IsFailure());
        Assert.False(CheckStatus.NotDetected.IsIncomplete());
        Assert.Equal("Acceso denegado", CheckStatus.PermissionDenied.ToDisplayText());
        Assert.Equal("Requiere administrador", CheckStatus.RequiresElevation.ToDisplayText());
        Assert.Equal("No evaluado sin conexión", CheckStatus.NotTestedOffline.ToDisplayText());
    }
}
