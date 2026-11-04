using System.Security.Principal;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Inventory.Collectors;

public static class CollectorErrors
{
    public static CheckRecord FromWmi(
        string id,
        string label,
        WmiQueryException exception,
        bool notFoundIsNotApplicable = false)
    {
        var detail = exception.Message;
        if (exception.Kind == WmiFailureKind.NotFound && notFoundIsNotApplicable)
        {
            return CheckRecord.NotApplicable(id, label, detail, "WMI/CIM");
        }

        return exception.Kind switch
        {
            WmiFailureKind.AccessDenied => AccessDenied(
                id, label, detail, "WMI/CIM"),
            WmiFailureKind.NotFound => CheckRecord.NotVerifiable(
                id, label, detail, "WMI/CIM"),
            WmiFailureKind.Timeout => CheckRecord.Timeout(
                id, label, detail, "WMI/CIM"),
            _ => CheckRecord.Error(
                id, label, detail, exception.GetType().Name, "WMI/CIM")
        };
    }

    public static CheckRecord FromRegistry(string id, string label, RegistryReadException exception) =>
        exception.AccessDenied
            ? AccessDenied(id, label, exception.Message, "Registro de Windows")
            : CheckRecord.Error(id, label, exception.Message, exception.GetType().Name, "Registro de Windows");

    public static CheckRecord AccessDenied(
        string id,
        string label,
        string? detail = null,
        string? source = null)
    {
        var reason = string.IsNullOrWhiteSpace(detail) ? "Windows denegó el acceso a la consulta." : detail.Trim();
        if (!IsCurrentProcessElevated())
        {
            return CheckRecord.RequiresElevation(
                id,
                label,
                reason + " Esta consulta requiere ejecutar AuditaPC como administrador.",
                source);
        }

        return CheckRecord.PermissionDenied(
            id,
            label,
            reason + " La sesión ya está elevada; puede existir una restricción del proveedor, ACL o política de Windows.",
            source);
    }

    public static CheckRecord FromException(string id, string label, Exception exception, string? source = null)
    {
        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Error desconocido.";
        }

        if (message.Length > 400)
        {
            message = message[..400] + "…";
        }

        return CheckRecord.Error(id, label, message, exception.GetType().Name, source);
    }

    private static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
