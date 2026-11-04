using System.Net;
using System.Net.Http;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Inventory.Collectors;

/// <summary>
/// Comprueba solo conectividad HTTPS con los servicios oficiales de activación.
/// No envía identificadores de producto, no inicia sesión y no activa licencias.
/// </summary>
public static class MicrosoftLicenseConnectivity
{
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        UseDefaultCredentials = false,
        UseCookies = false
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private static readonly Uri WindowsActivationEndpoint =
        new("https://activation.sls.microsoft.com/");

    private static readonly Uri OfficeLicensingEndpoint =
        new("https://ols.officeapps.live.com/");

    public static async Task<CheckRecord> CheckWindowsAsync(CancellationToken cancellationToken)
    {
        var result = await CheckEndpointAsync(WindowsActivationEndpoint, cancellationToken)
            .ConfigureAwait(false);

        return result.Reachable
            ? CheckRecord.Detected(
                "os.license.connectivity",
                "Conectividad con activación de Windows",
                $"El servicio respondió por HTTPS (HTTP {(int)result.StatusCode}). Esto confirma conectividad, no la titularidad de la licencia.",
                result.Host)
            : CheckRecord.NotVerifiable(
                "os.license.connectivity",
                "Conectividad con activación de Windows",
                result.Detail,
                result.Host);
    }

    public static async Task<CheckRecord> CheckOfficeAsync(CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(
            CheckEndpointAsync(OfficeLicensingEndpoint, cancellationToken),
            CheckEndpointAsync(WindowsActivationEndpoint, cancellationToken)).ConfigureAwait(false);

        var reachable = results.Count(result => result.Reachable);
        var detail = string.Join(" · ", results.Select(result =>
            result.Reachable
                ? $"{result.Host}: responde por HTTPS (HTTP {(int)result.StatusCode})"
                : $"{result.Host}: no accesible"));

        if (reachable == results.Length)
        {
            return CheckRecord.Detected(
                "office.license.connectivity",
                "Conectividad con licenciamiento de Office",
                detail + ". Esto no confirma la titularidad ni vigencia de una suscripción.",
                "Servicios de licenciamiento Microsoft");
        }

        return CheckRecord.NotVerifiable(
            "office.license.connectivity",
            "Conectividad con licenciamiento de Office",
            detail + ". Revisa conectividad, proxy o reglas de salida HTTPS.",
            "Servicios de licenciamiento Microsoft");
    }

    private static async Task<EndpointResult> CheckEndpointAsync(
        Uri endpoint,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.UserAgent.ParseAdd("AuditaPC/1.1");
            using var response = await Client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);

            return new EndpointResult(
                endpoint.Host,
                Reachable: true,
                response.StatusCode,
                $"El servicio {endpoint.Host} respondió por HTTPS.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new EndpointResult(
                endpoint.Host,
                Reachable: false,
                0,
                $"No se pudo conectar con {endpoint.Host} por HTTPS: {ex.Message}");
        }
    }

    private sealed record EndpointResult(
        string Host,
        bool Reachable,
        HttpStatusCode StatusCode,
        string Detail);
}
