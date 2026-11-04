using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using AuditaPC.Core.Model;

namespace AuditaPC.Core.Reporting;

/// <summary>
/// Informe HTML autónomo: búsqueda local, impresión y estilos incorporados.
/// No utiliza bibliotecas, fuentes ni recursos externos.
/// </summary>
public static class HtmlReportGenerator
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-CL");

    public static string Build(AuditReportData data, ReportOptions? options = null)
    {
        options ??= new ReportOptions();
        var builder = new StringBuilder(256 * 1024);
        var title = $"Informe de auditoría — {data.Manifest.CompanyName} — " +
                    $"{data.Manifest.StartDate:dd-MM-yyyy}";

        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"es\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; img-src data:; base-uri 'none'; form-action 'none'\">");
        builder.AppendLine($"<title>{H(title)}</title>");
        builder.AppendLine("<style>");
        builder.AppendLine(Css);
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");

        AppendTopbar(builder, data);
        AppendNavigation(builder);
        builder.AppendLine("<main id=\"main-content\">");
        AppendCover(builder, data);
        AppendExecutiveSummary(builder, data);
        AppendFieldNotes(builder, data);
        AppendInventory(builder, data);
        builder.AppendLine("<section class=\"container equipment-list\" id=\"fichas\">");
        builder.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">04 / Detalle por activo</p><h2>Fichas técnicas</h2>");
        builder.AppendLine("<p class=\"section-lead\">Abre cada equipo para consultar su configuración, estado y evidencias.</p></div></div>");
        if (data.Computers.Count == 0)
            builder.AppendLine("<div class=\"empty-panel\">No hay equipos para mostrar.</div>");
        foreach (var computer in OrderedComputers(data))
            AppendComputer(builder, data, computer, options);
        builder.AppendLine("</section>");

        AppendLicenses(builder, data);
        AppendFindings(builder, data);
        AppendActionPlan(builder, data);
        builder.AppendLine("</main>");
        AppendHowToRead(builder, data);
        AppendFooter(builder, data);
        builder.AppendLine("<script>");
        builder.AppendLine(InteractiveScript);
        builder.AppendLine("</script>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    private const string InteractiveScript = """
        (function () {
            const input = document.getElementById("inventory-search");
            const state = document.getElementById("inventory-state");
            const count = document.getElementById("inventory-count");
            const empty = document.getElementById("inventory-empty");
            const clear = document.getElementById("clear-filter");
            const print = document.getElementById("print-report");
            const csv = document.getElementById("export-inventory");
            const guide = document.getElementById("report-guide");
            const openGuide = document.getElementById("open-report-guide");
            const closeGuide = document.getElementById("close-report-guide");
            const rows = Array.from(document.querySelectorAll("#inventory-body tr[data-equipment-row]"));
            const cards = Array.from(document.querySelectorAll("details.equipment-card"));

            const normalize = value => (value || "").normalize("NFD")
                .replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase();

            function update() {
                const term = normalize(input ? input.value.trim() : "");
                const selectedState = state ? state.value : "all";
                let visible = 0;

                rows.forEach(row => {
                    const matchesText = normalize(row.dataset.search).includes(term);
                    const matchesState = selectedState === "all" || row.dataset.state === selectedState;
                    const show = matchesText && matchesState;
                    row.hidden = !show;
                    const card = document.getElementById(row.dataset.equipmentRow);
                    if (card) card.hidden = !show;
                    if (show) visible += 1;
                });

                if (count) count.textContent = visible + " de " + rows.length + " equipos";
                if (empty) empty.hidden = visible !== 0 || rows.length === 0;
            }

            function csvCell(value) {
                let text = String(value || "");
                if (/^[\s]*[=+\-@]/.test(text)) text = "'" + text;
                return '"' + text.replace(/"/g, '""') + '"';
            }

            function exportCsv() {
                const headers = ["Activo", "Equipo", "Fabricante / modelo", "Sistema operativo", "Responsable", "Ubicación", "Estado", "Capturas"];
                const visibleRows = rows.filter(row => !row.hidden);
                const lines = [headers.map(csvCell).join(";")];
                visibleRows.forEach(row => {
                    const cells = Array.from(row.querySelectorAll("td")).slice(0, 8)
                        .map(cell => cell.innerText.trim().replace(/\s+/g, " "));
                    lines.push(cells.map(csvCell).join(";"));
                });
                const blob = new Blob(["\ufeff" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
                const url = URL.createObjectURL(blob);
                const link = document.createElement("a");
                link.href = url;
                link.download = "Inventario_AuditaPC.csv";
                document.body.appendChild(link);
                link.click();
                link.remove();
                window.setTimeout(() => URL.revokeObjectURL(url), 1000);
            }

            if (input) input.addEventListener("input", update);
            if (state) state.addEventListener("change", update);
            if (clear) clear.addEventListener("click", function () {
                if (input) input.value = "";
                if (state) state.value = "all";
                update();
                if (input) input.focus();
            });
            if (print) print.addEventListener("click", () => window.print());
            if (csv) csv.addEventListener("click", exportCsv);
            if (guide && openGuide) {
                openGuide.addEventListener("click", () => {
                    if (typeof guide.showModal === "function") guide.showModal();
                    else guide.setAttribute("open", "");
                });
                if (closeGuide) closeGuide.addEventListener("click", () => guide.close());
                guide.addEventListener("click", event => {
                    if (event.target === guide) guide.close();
                });
            }

            document.querySelectorAll("[data-equipment-link]").forEach(link => {
                link.addEventListener("click", function () {
                    const card = document.getElementById(link.dataset.equipmentLink);
                    if (card) card.open = true;
                });
            });

            let previouslyClosed = [];
            window.addEventListener("beforeprint", function () {
                previouslyClosed = Array.from(document.querySelectorAll("details:not([open])"));
                previouslyClosed.forEach(item => item.open = true);
            });
            window.addEventListener("afterprint", function () {
                previouslyClosed.forEach(item => item.open = false);
                previouslyClosed = [];
            });

            update();
        })();
        """;

    private static void AppendTopbar(StringBuilder builder, AuditReportData data)
    {
        builder.AppendLine("<header class=\"topbar\"><div class=\"container topinner\">");
        builder.AppendLine("<div class=\"brand-lockup\"><span class=\"brand-mark\" aria-hidden=\"true\">A</span>");
        builder.AppendLine("<span class=\"brand-text\"><strong>AuditaPC</strong><small>Levantamiento técnico</small></span>");
        builder.AppendLine("<span class=\"brand-divider\"></span><span class=\"client-name\">");
        builder.AppendLine($"<strong>{H(data.Manifest.CompanyName)}</strong><small>Empresa auditada</small></span></div>");
        builder.AppendLine("<div class=\"top-status\">Informe técnico de equipos<br>");
        builder.AppendLine($"<strong>{data.Manifest.StartDate.ToString("dd 'de' MMMM 'de' yyyy", Culture)}</strong></div>");
        builder.AppendLine("</div></header>");
    }

    private static void AppendNavigation(StringBuilder builder)
    {
        builder.AppendLine("<nav class=\"nav\" aria-label=\"Secciones del informe\"><div class=\"container navinner\">");
        builder.AppendLine("<a href=\"#resumen\">Resumen</a><a href=\"#notas\">Notas de terreno</a>");
        builder.AppendLine("<a href=\"#inventario\">Inventario</a><a href=\"#fichas\">Fichas técnicas</a>");
        builder.AppendLine("<a href=\"#licencias\">Licencias</a><a href=\"#hallazgos\">Hallazgos</a>");
        builder.AppendLine("<a href=\"#acciones\">Plan de acción</a>");
        builder.AppendLine("<button type=\"button\" class=\"guide-trigger\" id=\"open-report-guide\" aria-haspopup=\"dialog\" aria-controls=\"report-guide\">Cómo leer el informe</button>");
        builder.AppendLine("</div></nav>");
    }

    private static void AppendCover(StringBuilder builder, AuditReportData data)
    {
        var manifest = data.Manifest;
        builder.AppendLine("<header class=\"cover container\">");
        builder.AppendLine("<div class=\"cover-main\">");
        builder.AppendLine("<p class=\"eyebrow\">AuditaPC / Servicios e infraestructura TI</p>");
        builder.AppendLine("<h1>Levantamiento técnico e inventario de equipos</h1>");
        builder.AppendLine($"<p class=\"cover-lead\">Informe de estado e inventario tecnológico para <strong>{H(manifest.CompanyName)}</strong>. " +
                           "Consolida identificación de activos, configuración, seguridad, software, licenciamiento y observaciones de la visita.</p>");
        builder.AppendLine("<div class=\"cover-rule\">");
        builder.AppendLine($"<span>{manifest.StartDate.ToString("dddd dd.MM.yyyy", Culture)}</span>");
        builder.AppendLine($"<span>Técnico: {H(Value(manifest.Technician))}</span>");
        builder.AppendLine($"<span>{data.Computers.Count.ToString(Culture)} equipos considerados</span>");
        builder.AppendLine($"<span>Auditoría {H(ShortId(manifest.Id))}</span>");
        builder.AppendLine("</div></div>");
        builder.AppendLine("<div class=\"cover-bottom\"><span>Documento de levantamiento · Datos automáticos y notas manuales identificados por separado.</span>");
        builder.AppendLine("<button class=\"action-btn primary\" id=\"print-report\" type=\"button\">Imprimir / Guardar PDF</button></div>");
        builder.AppendLine("</header>");
    }


    private static void AppendExecutiveSummary(StringBuilder builder, AuditReportData data)
    {
        builder.AppendLine("<div class=\"container\">");
        builder.AppendLine("<section class=\"summary section-space\" id=\"resumen\">");
        builder.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">01 / Visión general</p>");
        builder.AppendLine("<h2>Resumen ejecutivo</h2><p class=\"section-lead\">Estado de la auditoría y puntos que requieren atención.</p></div>");
        builder.AppendLine($"<span class=\"section-meta\">Generado {data.GeneratedAt.ToString("dd-MM-yyyy HH:mm", Culture)}</span></div>");
        if (!string.IsNullOrWhiteSpace(data.Manifest.Notes))
            builder.AppendLine($"<p class=\"lede\">{H(data.Manifest.Notes)}</p>");

        builder.AppendLine("<div class=\"kpis\">");
        AppendKpi(builder, data.Computers.Count.ToString(Culture), "equipos incluidos");
        AppendKpi(builder, data.ComputersWithProblems.ToString(Culture), "equipos con problemas",
            data.ComputersWithProblems > 0 ? "problem" : "ok");
        AppendKpi(builder, data.ComputersWithIncompleteData.ToString(Culture), "equipos que requieren revisión",
            data.ComputersWithIncompleteData > 0 ? "warn" : "ok");
        AppendKpi(builder, data.TotalPhotos.ToString(Culture), "fotografías adjuntas");
        builder.AppendLine("</div>");

        builder.AppendLine("<div class=\"intro-grid\">");
        builder.AppendLine("<article class=\"panel\"><h3>Síntesis de la visita</h3>");
        var visitSummary = string.IsNullOrWhiteSpace(data.Manifest.VisitSummary)
            ? "No se registró una síntesis general de la visita."
            : data.Manifest.VisitSummary;
        builder.AppendLine($"<p>{H(visitSummary)}</p></article>");
        builder.AppendLine("<aside class=\"panel\"><h3>Datos de la auditoría</h3><dl class=\"factlist\">");
        AppendMeta(builder, "Cliente", data.Manifest.CompanyName);
        AppendMeta(builder, "Técnico", data.Manifest.Technician);
        AppendMeta(builder, "Fecha", data.Manifest.StartDate.ToString("dd-MM-yyyy", Culture));
        AppendMeta(builder, "Equipos", data.Computers.Count.ToString(Culture));
        AppendMeta(builder, "Identificador", ShortId(data.Manifest.Id));
        builder.AppendLine("</dl></aside></div>");

        var counts = data.StatusCounts;
        var total = Math.Max(1, counts.Values.Sum());
        builder.AppendLine("<div class=\"overview-panel panel\"><div class=\"panel-heading\"><div><h3>Resultado de las comprobaciones</h3>");
        builder.AppendLine("<p class=\"fine\">Distribución de estados de la captura vigente de cada equipo.</p></div>");
        builder.AppendLine($"<strong>{counts.Values.Sum().ToString(Culture)} <small>comprobaciones</small></strong></div>");
        builder.AppendLine("<div class=\"status-overview\">");
        foreach (var status in StatusOrder)
        {
            counts.TryGetValue(status, out var count);
            if (count == 0) continue;
            var percent = count * 100d / total;
            builder.AppendLine("<div class=\"status-overview-row\">");
            builder.AppendLine($"<span>{Chip(status)}</span><span class=\"num\">{count.ToString(Culture)}</span>");
            builder.AppendLine($"<span class=\"bar\"><span class=\"bar-fill {StatusClass(status)}\" style=\"width:{percent.ToString("0.0", CultureInfo.InvariantCulture)}%\"></span></span>");
            builder.AppendLine("</div>");
        }
        if (counts.Count == 0)
            builder.AppendLine("<p class=\"empty\">Todavía no hay comprobaciones disponibles.</p>");
        builder.AppendLine("</div></div>");

        if (data.Warnings.Count > 0)
        {
            builder.AppendLine("<div class=\"notice warn\"><strong>Avisos al generar el informe</strong><ul>");
            foreach (var warning in data.Warnings)
                builder.AppendLine($"<li>{H(warning)}</li>");
            builder.AppendLine("</ul></div>");
        }
        builder.AppendLine("</section>");
        builder.AppendLine("</div>");
    }

    private static void AppendInventory(StringBuilder builder, AuditReportData data)
    {
        builder.AppendLine("<div class=\"container\">");
        builder.AppendLine("<section class=\"inventory-section section-space\" id=\"inventario\">");
        builder.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">03 / Gestión de activos</p>");
        builder.AppendLine("<h2>Inventario consolidado</h2><p class=\"section-lead\">Busca, filtra y abre la ficha técnica de cada equipo.</p></div>");
        builder.AppendLine($"<span class=\"section-meta\">{data.Computers.Count.ToString(Culture)} equipos</span></div>");
        builder.AppendLine("<div class=\"inventory-toolbar\">");
        builder.AppendLine("<label class=\"search-field\"><span>Buscar equipo</span><input id=\"inventory-search\" type=\"search\" placeholder=\"Nombre, serie, activo, modelo, responsable o ubicación\" autocomplete=\"off\"></label>");
        builder.AppendLine("<label class=\"filter-field\"><span>Estado</span><select id=\"inventory-state\"><option value=\"all\">Todos los estados</option><option value=\"problems\">Con problemas</option><option value=\"review\">Requieren revisión</option><option value=\"ok\">Sin incidencias</option><option value=\"missing\">Sin captura</option></select></label>");
        builder.AppendLine("<button class=\"action-btn secondary\" id=\"clear-filter\" type=\"button\">Limpiar filtros</button>");
        builder.AppendLine("<button class=\"action-btn secondary\" id=\"export-inventory\" type=\"button\">Exportar vista CSV</button>");
        builder.AppendLine("</div>");
        builder.AppendLine($"<p class=\"inventory-count\" id=\"inventory-count\" role=\"status\" aria-live=\"polite\">{data.Computers.Count.ToString(Culture)} equipos</p>");
        builder.AppendLine("<div class=\"table-wrap\"><table class=\"inventory-table\"><thead><tr>");
        builder.AppendLine("<th>Activo</th><th>Equipo</th><th>Fabricante / modelo</th><th>Sistema operativo</th><th>Responsable</th><th>Ubicación</th><th>Estado</th><th>Capturas</th>");
        builder.AppendLine("</tr></thead><tbody id=\"inventory-body\">");

        foreach (var computer in OrderedComputers(data))
        {
            var manual = computer.Record.Manual;
            var state = computer.Inventory is null ? "missing"
                : computer.Checks.Any(check => check.Status == CheckStatus.Problem) ? "problems"
                : computer.Checks.Any(check => check.Status.IsIncomplete()) ? "review" : "ok";
            var stateLabel = state switch
            {
                "missing" => "Sin captura",
                "problems" => "Con problemas",
                "review" => "Requiere revisión",
                _ => "Sin incidencias"
            };
            var search = Join(computer.DisplayName, computer.Serial, computer.Manufacturer, computer.Model,
                computer.OperatingSystem, manual.AssetCode, manual.Responsible, manual.Location);
            var anchor = Anchor(computer);
            builder.AppendLine($"<tr data-equipment-row=\"{anchor}\" data-state=\"{state}\" data-search=\"{H(search)}\">");
            builder.AppendLine($"<td class=\"mono\">{H(manual.AssetCode)}</td>");
            builder.AppendLine($"<td><a class=\"inventory-link\" href=\"#{anchor}\" data-equipment-link=\"{anchor}\">{H(computer.DisplayName)}</a></td>");
            builder.AppendLine($"<td>{H(Join(computer.Manufacturer, computer.Model))}</td>");
            builder.AppendLine($"<td>{H(computer.OperatingSystem)}</td>");
            builder.AppendLine($"<td>{H(manual.Responsible)}</td><td>{H(manual.Location)}</td>");
            builder.AppendLine($"<td><span class=\"state-pill state-{state}\">{H(stateLabel)}</span></td>");
            builder.AppendLine($"<td class=\"num\">{computer.Record.Captures.Count.ToString(Culture)}</td></tr>");
        }

        if (data.Computers.Count == 0)
            builder.AppendLine("<tr><td colspan=\"8\" class=\"empty\">La auditoría todavía no contiene equipos.</td></tr>");
        builder.AppendLine("</tbody></table></div>");
        builder.AppendLine("<p class=\"empty inventory-empty\" id=\"inventory-empty\" hidden>No hay equipos que coincidan con los filtros.</p>");
        builder.AppendLine("</section></div>");
    }

    private static void AppendLicenses(StringBuilder builder, AuditReportData data)
    {
        builder.AppendLine("<div class=\"container\"><section class=\"section-space\" id=\"licencias\">");
        builder.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">05 / Servicios y licenciamiento</p><h2>Licenciamiento detectado</h2>");
        builder.AppendLine("<p class=\"section-lead\">Información leída desde los equipos. La activación local no acredita por sí sola titularidad ni vigencia contractual.</p></div></div>");
        builder.AppendLine("<div class=\"table-wrap\"><table class=\"data\"><thead><tr><th>Equipo</th><th>Producto</th><th>Estado local</th><th>Canal / descripción</th><th>Fuente</th></tr></thead><tbody>");
        var rows = 0;
        foreach (var computer in OrderedComputers(data))
        {
            var osLicense = computer.Inventory?.OperatingSystem.Data?.License;
            if (osLicense is not null &&
                (!string.IsNullOrWhiteSpace(osLicense.ProductName)
                 || !string.IsNullOrWhiteSpace(osLicense.LicenseStatusText)
                 || !string.IsNullOrWhiteSpace(osLicense.Channel)
                 || !string.IsNullOrWhiteSpace(osLicense.Source)
                 || !string.IsNullOrWhiteSpace(osLicense.QueryNote)))
            {
                rows++;
                builder.AppendLine($"<tr><td>{H(computer.DisplayName)}</td><td>{H(osLicense.ProductName ?? "Windows")}</td>");
                builder.AppendLine($"<td>{H(osLicense.LicenseStatusText)}</td><td>{H(osLicense.Channel)}</td><td>{H(osLicense.Source ?? osLicense.QueryNote)}</td></tr>");
            }

            var office = computer.Inventory?.Office.Data;
            if (office is null) continue;
            foreach (var license in office.Licenses)
            {
                rows++;
                builder.AppendLine($"<tr><td>{H(computer.DisplayName)}</td><td>{H(license.ProductName)}</td>");
                builder.AppendLine($"<td>{H(license.LicenseStatusText)}</td><td>{H(license.Description)}</td><td>{H(license.Source ?? license.QueryNote ?? "Detección local de Office")}</td></tr>");
            }

            if (!string.IsNullOrWhiteSpace(office.SubscriptionNote))
            {
                rows++;
                builder.AppendLine($"<tr><td>{H(computer.DisplayName)}</td><td>Microsoft 365</td><td>Nota de suscripción</td>");
                builder.AppendLine($"<td colspan=\"2\">{H(office.SubscriptionNote)}</td></tr>");
            }
            if (!string.IsNullOrWhiteSpace(office.DetectionNote))
            {
                rows++;
                builder.AppendLine($"<tr><td>{H(computer.DisplayName)}</td><td>Microsoft Office</td><td>Nota de detección</td>");
                builder.AppendLine($"<td colspan=\"2\">{H(office.DetectionNote)}</td></tr>");
            }
        }
        if (rows == 0)
            builder.AppendLine("<tr><td colspan=\"5\" class=\"empty\">No hay datos de licenciamiento local disponibles en las capturas incluidas.</td></tr>");
        builder.AppendLine("</tbody></table></div>");
        builder.AppendLine("<p class=\"fine\">Las comprobaciones que requieren conexión o permisos especiales se identifican como no verificadas; no se interpretan como licencias vencidas.</p>");
        builder.AppendLine("</section></div>");
    }

    private static void AppendFindings(StringBuilder builder, AuditReportData data)
    {
        builder.AppendLine("<div class=\"container\"><section class=\"section-space\" id=\"hallazgos\">");
        builder.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">06 / Evaluación técnica</p><h2>Hallazgos y verificaciones pendientes</h2>");
        builder.AppendLine("<p class=\"section-lead\">Se muestran problemas detectados, resultados parciales y comprobaciones que no pudieron completarse.</p></div></div>");
        builder.AppendLine("<div class=\"table-wrap\"><table class=\"data\"><thead><tr><th>Equipo</th><th>Área</th><th>Comprobación</th><th>Resultado</th><th>Detalle</th></tr></thead><tbody>");
        var rows = 0;
        foreach (var computer in OrderedComputers(data))
        {
            if (computer.Inventory is not null)
            {
                foreach (var category in computer.Inventory.Categories())
                {
                    foreach (var check in category.Checks)
                    {
                        if (!check.Status.RequiresReview()
                            && check.Status is not (CheckStatus.NotTestedOffline or CheckStatus.KnownLimitation))
                            continue;
                        rows++;
                        builder.AppendLine($"<tr><td>{H(computer.DisplayName)}</td><td>{H(category.Title)}</td><td>{H(check.Label)}</td>");
                        builder.AppendLine($"<td>{Chip(check.Status)}</td><td>{H(check.Detail)}</td></tr>");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(computer.Record.Manual.IssuesFound))
            {
                rows++;
                builder.AppendLine($"<tr><td>{H(computer.DisplayName)}</td><td>Inspección en terreno</td><td>Problemas informados por el técnico</td>");
                builder.AppendLine($"<td><span class=\"state-pill state-review\">Registro manual</span></td><td>{H(computer.Record.Manual.IssuesFound)}</td></tr>");
            }
        }
        if (!string.IsNullOrWhiteSpace(data.Manifest.FindingsNotes))
        {
            rows++;
            builder.AppendLine($"<tr><td>Auditoría completa</td><td>Inspección en terreno</td><td>Hallazgos generales</td>");
            builder.AppendLine($"<td><span class=\"state-pill state-review\">Registro manual</span></td><td>{H(data.Manifest.FindingsNotes)}</td></tr>");
        }
        if (rows == 0)
            builder.AppendLine("<tr><td colspan=\"5\" class=\"empty\">No se registraron hallazgos ni verificaciones pendientes en los datos disponibles.</td></tr>");
        builder.AppendLine("</tbody></table></div>");
        builder.AppendLine("</section></div>");
    }

    private static void AppendActionPlan(StringBuilder builder, AuditReportData data)
    {
        builder.AppendLine("<div class=\"container\"><section class=\"section-space\" id=\"acciones\">");
        builder.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">07 / Continuidad operativa</p><h2>Plan de acción y recomendaciones</h2>");
        builder.AppendLine("<p class=\"section-lead\">Acciones pendientes y recomendaciones registradas por el técnico durante la visita.</p></div></div>");
        var count = 0;
        if (!string.IsNullOrWhiteSpace(data.Manifest.ActionPlanNotes))
        {
            count++;
            builder.AppendLine("<article class=\"action-card\"><span class=\"action-kind\">Plan general</span>");
            builder.AppendLine($"<p>{H(data.Manifest.ActionPlanNotes)}</p></article>");
        }
        foreach (var computer in OrderedComputers(data))
        {
            var manual = computer.Record.Manual;
            if (string.IsNullOrWhiteSpace(manual.PendingWork) && string.IsNullOrWhiteSpace(manual.Recommendations))
                continue;
            count++;
            builder.AppendLine("<article class=\"action-card\">");
            builder.AppendLine($"<h3>{H(computer.DisplayName)}</h3>");
            if (!string.IsNullOrWhiteSpace(manual.PendingWork))
                builder.AppendLine($"<div><span class=\"action-kind\">Trabajo pendiente</span><p>{H(manual.PendingWork)}</p></div>");
            if (!string.IsNullOrWhiteSpace(manual.Recommendations))
                builder.AppendLine($"<div><span class=\"action-kind\">Recomendación</span><p>{H(manual.Recommendations)}</p></div>");
            builder.AppendLine("</article>");
        }
        if (count == 0)
            builder.AppendLine("<div class=\"empty-panel\">No se registraron trabajos pendientes ni recomendaciones en esta auditoría.</div>");
        builder.AppendLine("</section></div>");
    }

    private static void AppendFieldNotes(StringBuilder builder, AuditReportData data)
    {
        var notes = new (string Label, string? Value)[]
        {
            ("Conectividad y red", data.Manifest.ConnectivityNotes),
            ("Aplicaciones y módulos", data.Manifest.ModulesNotes),
            ("Estado general", data.Manifest.ConditionNotes),
            ("Limitaciones de la visita", data.Manifest.LimitationsNotes),
            ("Otras observaciones", data.Manifest.OtherNotes)
        }.Where(item => !string.IsNullOrWhiteSpace(item.Value)).ToList();

        builder.AppendLine("<div class=\"container\"><section class=\"field-notes section-space\" id=\"notas\">");
        builder.AppendLine("<div class=\"section-heading\"><div><p class=\"eyebrow\">02 / Registro del técnico</p><h2>Notas de terreno</h2>");
        builder.AppendLine("<p class=\"section-lead\">Información ingresada durante la visita, separada de los datos detectados automáticamente.</p></div></div>");
        if (notes.Count == 0)
        {
            builder.AppendLine("<div class=\"empty-panel\">No se ingresaron notas generales de terreno para esta auditoría.</div>");
        }
        else
        {
            builder.AppendLine("<div class=\"field-notes-grid\">");
            foreach (var note in notes)
            {
                builder.AppendLine("<article class=\"field-note\">");
                builder.AppendLine($"<h3>{H(note.Label)}</h3>");
                builder.AppendLine($"<p>{H(note.Value)}</p>");
                builder.AppendLine("</article>");
            }
            builder.AppendLine("</div>");
        }
        builder.AppendLine("</section></div>");
    }

    private static void AppendHowToRead(StringBuilder builder, AuditReportData data)
    {
        builder.AppendLine("<dialog class=\"report-guide\" id=\"report-guide\" aria-labelledby=\"report-guide-title\">");
        builder.AppendLine("<div class=\"guide-header\"><div><p class=\"eyebrow\">Documento de ayuda · No forma parte del informe de auditoría</p>");
        builder.AppendLine("<h2 id=\"report-guide-title\">Cómo leer este informe</h2>");
        builder.AppendLine("<p class=\"section-lead\">Guía de interpretación de estados y alcance técnico. No es un hallazgo ni una conclusión de la auditoría.</p></div>");
        builder.AppendLine("<button type=\"button\" class=\"guide-close\" id=\"close-report-guide\" aria-label=\"Cerrar ayuda\">Cerrar ×</button></div>");
        builder.AppendLine("<div class=\"legend-grid\">");
        builder.AppendLine("<div class=\"panel\">");
        builder.AppendLine("<h3>Estados</h3>");
        builder.AppendLine("<dl class=\"status-legend\">");
        AppendLegend(builder, CheckStatus.Verified, "El dato se obtuvo y se validó.");
        AppendLegend(builder, CheckStatus.Detected, "Se detectó presencia o configuración, sin validar el estado completo.");
        AppendLegend(builder, CheckStatus.NotDetected, "La consulta funcionó y el elemento no está presente.");
        AppendLegend(builder, CheckStatus.Problem, "Se confirmó una situación que requiere revisión.");
        AppendLegend(builder, CheckStatus.Partial, "La categoría se obtuvo parcialmente.");
        AppendLegend(builder, CheckStatus.NotVerifiable, "El proveedor o el equipo no entregó información suficiente; el detalle indica qué faltó.");
        AppendLegend(builder, CheckStatus.RequiresElevation, "La consulta necesita una sesión de Windows con permisos de administrador.");
        AppendLegend(builder, CheckStatus.PermissionDenied, "Windows o una política denegó el acceso incluso con la sesión elevada.");
        AppendLegend(builder, CheckStatus.NotTestedOffline, "No se intentó porque requiere Internet o porque la comprobación en línea está desactivada.");
        AppendLegend(builder, CheckStatus.KnownLimitation, "La información queda fuera del alcance documentado de la recopilación.");
        AppendLegend(builder, CheckStatus.NotApplicable, "La comprobación no corresponde a este equipo o edición.");
        AppendLegend(builder, CheckStatus.QueryError, "La consulta falló por un error técnico.");
        AppendLegend(builder, CheckStatus.Timeout, "La consulta excedió el tiempo máximo.");
        builder.AppendLine("</dl>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"panel\">");
        builder.AppendLine("<h3>Alcance y límites</h3>");
        builder.AppendLine("<ul class=\"tight\">");
        builder.AppendLine("<li>AuditaPC consulta el equipo local. Por defecto no utiliza Internet; solo comprueba endpoints de activación de Microsoft si el técnico habilita expresamente esa opción.</li>");
        var onlineEnabledCount = data.Computers.Count(computer =>
            computer.Inventory?.Environment.OnlineLicenseChecksEnabled == true);
        if (onlineEnabledCount > 0)
        {
            builder.AppendLine($"<li>La comprobación HTTPS de servicios de Microsoft estuvo habilitada en {onlineEnabledCount.ToString(Culture)} captura(s). Una respuesta HTTP solo confirma conectividad; no activa Windows/Office ni demuestra la titularidad o vigencia contractual de una licencia.</li>");
        }
        else
        {
            builder.AppendLine("<li>La comprobación HTTPS de licencias estuvo desactivada en las capturas de este informe; no se realizaron consultas en línea.</li>");
        }
        builder.AppendLine("<li>No instala, desinstala ni modifica software, servicios, políticas ni configuraciones.</li>");
        builder.AppendLine("<li>No ejecuta reparaciones, pruebas de estrés ni benchmarks.</li>");
        builder.AppendLine("<li>No recopila contraseñas, credenciales, claves de producto completas ni claves de recuperación.</li>");
        builder.AppendLine("<li>La activación local de Windows u Office no demuestra la vigencia contractual de una suscripción.</li>");
        builder.AppendLine("<li>La ausencia de un dato puede deberse a permisos, a la edición de Windows o a que el proveedor no expone esa información.</li>");
        builder.AppendLine("<li>La lista de software no incluye aplicaciones de Microsoft Store, programas portables ni otros perfiles de usuario.</li>");
        builder.AppendLine("<li>Las capturas anteriores se conservan; este informe usa la captura vigente de cada equipo.</li>");
        builder.AppendLine("</ul>");
        builder.AppendLine("</div>");
        builder.AppendLine("</div>");
        builder.AppendLine("</dialog>");
    }

    private static void AppendComputer(
        StringBuilder builder,
        AuditReportData data,
        ComputerReport computer,
        ReportOptions options)
    {
        var state = computer.Inventory is null ? "missing"
            : computer.Checks.Any(check => check.Status == CheckStatus.Problem) ? "problems"
            : computer.Checks.Any(check => check.Status.IsIncomplete()) ? "review" : "ok";
        var stateLabel = state switch
        {
            "missing" => "Sin captura automática",
            "problems" => "Problemas detectados",
            "review" => "Requiere revisión",
            _ => "Sin incidencias detectadas"
        };

        builder.AppendLine($"<details class=\"computer equipment-card\" id=\"{Anchor(computer)}\">");
        builder.AppendLine("<summary class=\"computer-summary\">");
        builder.AppendLine("<div class=\"computer-head\"><div>");
        builder.AppendLine("<p class=\"eyebrow\">Ficha técnica del equipo</p>");
        builder.AppendLine($"<h2>{H(computer.DisplayName)}</h2>");
        builder.AppendLine($"<p class=\"computer-sub\">{H(Join(computer.Manufacturer, computer.Model))} · {H(computer.OperatingSystem)}</p>");
        builder.AppendLine("</div><div class=\"computer-tags\">");
        builder.AppendLine($"<span class=\"tag\">Activo {H(computer.Record.Manual.AssetCode)}</span>");
        builder.AppendLine($"<span class=\"tag\">Serie {H(computer.Serial)}</span>");
        builder.AppendLine($"<span class=\"tag\">{computer.Record.Captures.Count.ToString(Culture)} captura(s)</span>");
        builder.AppendLine($"<span class=\"state-pill state-{state}\">{H(stateLabel)}</span>");
        builder.AppendLine("</div></div>");
        builder.AppendLine("<span class=\"open-hint\">Abrir ficha <span aria-hidden=\"true\">＋</span></span>");
        builder.AppendLine("</summary>");
        builder.AppendLine("<div class=\"computer-content\">");

        AppendIdentityCard(builder, computer);

        if (computer.Inventory is null)
        {
            builder.AppendLine("<div class=\"notice warn\"><strong>Sin captura automática.</strong> " +
                               "Este equipo solo tiene información manual o sus capturas no pudieron leerse.</div>");
            AppendManual(builder, computer);
        }
        else
        {
            AppendManual(builder, computer);
            AppendOperatingSystem(builder, computer);
            AppendHardware(builder, computer);
            AppendStorage(builder, computer);
            AppendNetwork(builder, computer);
            AppendSoftware(builder, computer, options);
            AppendOffice(builder, computer);
            AppendSecurity(builder, computer);
            if (options.IncludeCheckTables)
                AppendChecks(builder, computer);
        }

        AppendPhotos(builder, computer);
        AppendHistory(builder, computer);
        builder.AppendLine("</div></details>");
    }

    private static void AppendIdentityCard(StringBuilder builder, ComputerReport computer)
    {
        var identity = computer.Inventory?.Identity.Data;
        var manual = computer.Record.Manual;
        builder.AppendLine("<div class=\"identity-card\">");
        builder.AppendLine("<dl class=\"identity-grid\">");
        AppendMeta(builder, "Código de activo", Value(manual.AssetCode));
        AppendMeta(builder, "Ubicación", Value(manual.Location));
        AppendMeta(builder, "Responsable", Value(manual.Responsible));
        AppendMeta(builder, "Técnico de registro", Value(manual.Technician));
        AppendMeta(builder, "Fabricante", Value(computer.Manufacturer));
        AppendMeta(builder, "Modelo", Value(computer.Model));
        AppendMeta(builder, "Número de serie", Value(computer.Serial));
        AppendMeta(builder, "UUID SMBIOS", Value(identity?.SystemUuid));
        AppendMeta(builder, "Dominio o grupo", Value(identity?.Domain ?? identity?.Workgroup));
        AppendMeta(builder, "Última captura", Formatting.DateTimeLong(computer.Record.CurrentCapture?.CapturedAt));
        AppendMeta(builder, "Captura vigente", computer.CurrentCapture?.Id);
        builder.AppendLine("</dl>");
        builder.AppendLine("</div>");
    }

    private static void AppendManual(StringBuilder builder, ComputerReport computer)
    {
        var manual = computer.Record.Manual;
        builder.AppendLine("<section class=\"block\">");
        builder.AppendLine("<h3>Información registrada por el técnico</h3>");
        if (manual.IsEmpty)
        {
            builder.AppendLine("<p class=\"empty\">Sin información manual registrada.</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine("<div class=\"manual-grid\">");
        AppendManualField(builder, "Observaciones", manual.Notes);
        AppendManualField(builder, "Problemas encontrados", manual.IssuesFound);
        AppendManualField(builder, "Intervenciones realizadas", manual.Interventions);
        AppendManualField(builder, "Trabajo pendiente", manual.PendingWork);
        AppendManualField(builder, "Recomendaciones", manual.Recommendations);
        builder.AppendLine("</div>");
        if (manual.UpdatedAt is { } updated)
        {
            builder.AppendLine($"<p class=\"fine\">Actualizado por última vez el {Formatting.DateTimeLong(updated)}.</p>");
        }

        builder.AppendLine("</section>");
    }

    private static void AppendManualField(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.AppendLine("<div class=\"manual-field\">");
        builder.AppendLine($"<h4>{H(label)}</h4>");
        builder.AppendLine($"<p>{H(value)}</p>");
        builder.AppendLine("</div>");
    }

    private static void AppendOperatingSystem(StringBuilder builder, ComputerReport computer)
    {
        var category = computer.Inventory!.OperatingSystem;
        var os = category.Data;
        builder.AppendLine("<section class=\"block\">");
        builder.AppendLine("<h3>Sistema operativo</h3>");
        builder.AppendLine($"<p class=\"block-status\">{Chip(category.Status)}</p>");

        if (os is null)
        {
            builder.AppendLine("<p class=\"empty\">Sin datos del sistema operativo.</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine("<table class=\"data\"><tbody>");
        AppendRow(builder, "Producto", os.ProductName);
        AppendRow(builder, "Edición", os.Edition);
        AppendRow(builder, "Versión", os.DisplayVersion);
        AppendRow(builder, "Compilación", JoinBuild(os.Version, os.BuildNumber, os.UpdateBuildRevision));
        AppendRow(builder, "Arquitectura", os.Architecture);
        AppendRow(builder, "Fecha de instalación", Formatting.DateTimeLong(os.InstallDate));
        AppendRow(builder, "Último arranque", Formatting.DateTimeLong(os.LastBootTime));
        AppendRow(builder, "Tipo de instalación", os.InstallationType);
        AppendRow(builder, "Disco del sistema", os.SystemDrive);
        AppendRow(builder, "Idioma del sistema", os.SystemLocale);
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<h4>Licencia de Windows</h4>");
        builder.AppendLine("<table class=\"data\"><tbody>");
        AppendRow(builder, "Producto", os.License.ProductName);
        AppendRow(builder, "Estado", os.License.LicenseStatusText);
        AppendRow(builder, "Canal", os.License.Channel);
        AppendRow(builder, "Fuente", os.License.Source);
        if (!string.IsNullOrWhiteSpace(os.License.QueryNote))
        {
            AppendRow(builder, "Nota", os.License.QueryNote);
        }

        builder.AppendLine("</tbody></table>");

        if (os.RecentUpdates.Count > 0)
        {
            builder.AppendLine("<h4>Actualizaciones recientes</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>KB</th><th>Descripción</th><th>Instalada</th></tr></thead><tbody>");
            foreach (var update in os.RecentUpdates.OrderByDescending(update => update.InstalledOn))
            {
                builder.AppendLine($"<tr><td class=\"mono\">{H(update.HotFixId)}</td><td>{H(update.Description)}</td>" +
                                   $"<td>{H(Formatting.DateTimeLong(update.InstalledOn))}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        builder.AppendLine("</section>");
    }

    private static void AppendHardware(StringBuilder builder, ComputerReport computer)
    {
        var category = computer.Inventory!.Hardware;
        var hardware = category.Data;
        builder.AppendLine("<section class=\"block\">");
        builder.AppendLine("<h3>Procesador y memoria</h3>");
        builder.AppendLine($"<p class=\"block-status\">{Chip(category.Status)}</p>");

        if (hardware is null)
        {
            builder.AppendLine("<p class=\"empty\">Sin datos de hardware.</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine("<div class=\"stat-line\">");
        builder.AppendLine($"<span><strong>{Formatting.Bytes(hardware.TotalMemoryBytes)}</strong> de RAM</span>");
        if (hardware.MemorySlotCount is { } slots)
        {
            builder.AppendLine($"<span><strong>{slots.ToString(Culture)}</strong> ranura(s)</span>");
        }

        if (hardware.MemoryMaxCapacityBytes is { } max)
        {
            builder.AppendLine($"<span>máximo <strong>{Formatting.Bytes(max)}</strong></span>");
        }

        builder.AppendLine("</div>");

        if (hardware.Processors.Count > 0)
        {
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Procesador</th><th class=\"num\">Núcleos</th>" +
                               "<th class=\"num\">Hilos</th><th class=\"num\">Frecuencia</th><th>Zócalo</th></tr></thead><tbody>");
            foreach (var cpu in hardware.Processors)
            {
                builder.AppendLine($"<tr><td>{H(cpu.Name)}</td><td class=\"num\">{H(cpu.Cores?.ToString(Culture))}</td>" +
                                   $"<td class=\"num\">{H(cpu.LogicalProcessors?.ToString(Culture))}</td>" +
                                   $"<td class=\"num\">{H(cpu.MaxClockMhz is { } mhz ? mhz.ToString(Culture) + " MHz" : null)}</td>" +
                                   $"<td>{H(cpu.Socket)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (hardware.MemoryModules.Count > 0)
        {
            builder.AppendLine("<h4>Módulos de memoria</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Ranura</th><th class=\"num\">Capacidad</th>" +
                               "<th class=\"num\">Velocidad</th><th>Tipo</th><th>Fabricante</th><th>Parte</th></tr></thead><tbody>");
            foreach (var module in hardware.MemoryModules)
            {
                builder.AppendLine($"<tr><td>{H(module.Slot ?? module.BankLabel)}</td>" +
                                   $"<td class=\"num\">{H(Formatting.Bytes(module.CapacityBytes))}</td>" +
                                   $"<td class=\"num\">{H(module.SpeedMhz is { } speed ? speed.ToString(Culture) + " MHz" : null)}</td>" +
                                   $"<td>{H(module.MemoryType)}</td><td>{H(module.Manufacturer)}</td><td>{H(module.PartNumber)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (hardware.Graphics.Count > 0)
        {
            builder.AppendLine("<h4>Adaptadores de video</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Adaptador</th><th class=\"num\">Memoria</th>" +
                               "<th>Controlador</th><th>Resolución</th></tr></thead><tbody>");
            foreach (var gpu in hardware.Graphics)
            {
                builder.AppendLine($"<tr><td>{H(gpu.Name)}</td><td class=\"num\">{H(Formatting.Bytes(gpu.AdapterRamBytes))}</td>" +
                                   $"<td>{H(gpu.DriverVersion)}</td><td>{H(gpu.CurrentResolution)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (hardware.Batteries.Count > 0)
        {
            builder.AppendLine("<h4>Baterías</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Batería</th><th class=\"num\">Carga</th><th>Estado</th></tr></thead><tbody>");
            foreach (var battery in hardware.Batteries)
            {
                builder.AppendLine($"<tr><td>{H(battery.Name)}</td>" +
                                   $"<td class=\"num\">{H(battery.EstimatedChargePercent is { } charge ? charge.ToString(Culture) + " %" : null)}</td>" +
                                   $"<td>{H(battery.Status)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        builder.AppendLine("</section>");
    }

    private static void AppendStorage(StringBuilder builder, ComputerReport computer)
    {
        var category = computer.Inventory!.Storage;
        var storage = category.Data;
        builder.AppendLine("<section class=\"block\">");
        builder.AppendLine("<h3>Almacenamiento</h3>");
        builder.AppendLine($"<p class=\"block-status\">{Chip(category.Status)}</p>");

        if (storage is null)
        {
            builder.AppendLine("<p class=\"empty\">Sin datos de almacenamiento.</p>");
            builder.AppendLine("</section>");
            return;
        }

        if (storage.PhysicalDisks.Count > 0)
        {
            builder.AppendLine("<h4>Discos físicos</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Modelo</th><th class=\"num\">Capacidad</th>" +
                               "<th>Interfaz</th><th>Medio</th><th>Salud</th><th>Serie</th></tr></thead><tbody>");
            foreach (var disk in storage.PhysicalDisks)
            {
                builder.AppendLine($"<tr><td>{H(disk.Model)}</td><td class=\"num\">{H(Formatting.Bytes(disk.SizeBytes))}</td>" +
                                   $"<td>{H(disk.InterfaceType ?? disk.BusType)}</td><td>{H(disk.MediaType)}</td>" +
                                   $"<td>{H(disk.HealthStatus ?? disk.OperationalStatus)}</td><td class=\"mono\">{H(disk.SerialNumber)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (storage.Volumes.Count > 0)
        {
            builder.AppendLine("<h4>Unidades lógicas</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Unidad</th><th>Etiqueta</th><th>Sistema de archivos</th>" +
                               "<th class=\"num\">Capacidad</th><th class=\"num\">Libre</th><th class=\"num\">Uso</th></tr></thead><tbody>");
            foreach (var volume in storage.Volumes)
            {
                builder.AppendLine($"<tr><td class=\"mono\">{H(volume.DriveLetter)}</td><td>{H(volume.Label)}</td>" +
                                   $"<td>{H(volume.FileSystem)}</td><td class=\"num\">{H(Formatting.Bytes(volume.SizeBytes))}</td>" +
                                   $"<td class=\"num\">{H(Formatting.Bytes(volume.FreeBytes))}</td>" +
                                   $"<td class=\"num\">{H(Formatting.Percent(volume.UsedPercent))}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (!string.IsNullOrWhiteSpace(storage.HealthQueryNote))
        {
            builder.AppendLine($"<p class=\"fine\">{H(storage.HealthQueryNote)}</p>");
        }

        builder.AppendLine("</section>");
    }

    private static void AppendNetwork(StringBuilder builder, ComputerReport computer)
    {
        var category = computer.Inventory!.Network;
        var network = category.Data;
        builder.AppendLine("<section class=\"block\">");
        builder.AppendLine("<h3>Red local</h3>");
        builder.AppendLine($"<p class=\"block-status\">{Chip(category.Status)}</p>");

        if (network is null || network.Adapters.Count == 0)
        {
            builder.AppendLine("<p class=\"empty\">Sin interfaces de red detectadas.</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine("<div class=\"adapter-grid\">");
        foreach (var adapter in network.Adapters
                     .OrderByDescending(adapter => adapter.IPv4Addresses.Count > 0)
                     .ThenBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            builder.AppendLine("<article class=\"adapter\">");
            builder.AppendLine($"<h4>{H(adapter.Name ?? adapter.Description)}</h4>");
            builder.AppendLine($"<p class=\"adapter-sub\">{H(adapter.Description)}</p>");
            builder.AppendLine("<dl class=\"adapter-meta\">");
            AppendMeta(builder, "Estado", adapter.Status);
            AppendMeta(builder, "Tipo", adapter.AdapterType ?? (adapter.IsPhysical == true ? "Física" : adapter.IsVirtual == true ? "Virtual" : null));
            AppendMeta(builder, "Velocidad", Formatting.LinkSpeed(adapter.LinkSpeedBps));
            AppendMeta(builder, "MAC", adapter.MacAddress);
            AppendMeta(builder, "IPv4", adapter.IPv4Addresses.Count > 0 ? string.Join(", ", adapter.IPv4Addresses) : null);
            AppendMeta(builder, "IPv6", adapter.IPv6Addresses.Count > 0 ? string.Join(", ", adapter.IPv6Addresses) : null);
            AppendMeta(builder, "DHCP", Formatting.YesNo(adapter.DhcpEnabled));
            AppendMeta(builder, "Puerta de enlace", adapter.Gateways.Count > 0 ? string.Join(", ", adapter.Gateways) : null);
            AppendMeta(builder, "DNS", adapter.DnsServers.Count > 0 ? string.Join(", ", adapter.DnsServers) : null);
            builder.AppendLine("</dl>");
            builder.AppendLine("</article>");
        }

        builder.AppendLine("</div>");
        builder.AppendLine("</section>");
    }

    private static void AppendSoftware(StringBuilder builder, ComputerReport computer, ReportOptions options)
    {
        var category = computer.Inventory!.Software;
        var software = category.Data;
        builder.AppendLine("<section class=\"block\">");
        builder.AppendLine("<h3>Software instalado</h3>");
        builder.AppendLine($"<p class=\"block-status\">{Chip(category.Status)}</p>");

        if (software is null)
        {
            builder.AppendLine("<p class=\"empty\">Sin datos de software.</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine($"<p class=\"stat-line\"><span><strong>{software.TotalCount.ToString(Culture)}</strong> programas registrados</span></p>");

        if (software.Limitations.Count > 0)
        {
            builder.AppendLine("<ul class=\"tight fine\">");
            foreach (var limitation in software.Limitations)
            {
                builder.AppendLine($"<li>{H(limitation)}</li>");
            }

            builder.AppendLine("</ul>");
        }

        if (options.IncludeSoftwareDetail && software.Programs.Count > 0)
        {
            var open = software.Programs.Count <= 25 ? " open" : string.Empty;
            builder.AppendLine($"<details class=\"fold\"{open}>");
            builder.AppendLine($"<summary>Ver listado completo ({software.Programs.Count.ToString(Culture)})</summary>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Programa</th><th>Versión</th><th>Fabricante</th>" +
                               "<th>Instalación</th><th>Ámbito</th></tr></thead><tbody>");
            foreach (var program in software.Programs
                         .OrderBy(program => program.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                builder.AppendLine($"<tr><td>{H(program.Name)}</td><td>{H(program.Version)}</td>" +
                                   $"<td>{H(program.Publisher)}</td><td>{H(Formatting.Date(program.InstallDate))}</td>" +
                                   $"<td>{H(program.Scope)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
            builder.AppendLine("</details>");
        }

        builder.AppendLine("</section>");
    }

    private static void AppendOffice(StringBuilder builder, ComputerReport computer)
    {
        var category = computer.Inventory!.Office;
        var office = category.Data;
        builder.AppendLine("<section class=\"block\">");
        builder.AppendLine("<h3>Microsoft Office</h3>");
        builder.AppendLine($"<p class=\"block-status\">{Chip(category.Status)}</p>");

        if (office is null)
        {
            builder.AppendLine("<p class=\"empty\">Sin datos de Office.</p>");
            builder.AppendLine("</section>");
            return;
        }

        builder.AppendLine($"<p class=\"stat-line\"><span>Instalado: <strong>{Formatting.YesNo(office.Installed)}</strong></span>" +
                           $"<span>{office.Installations.Count.ToString(Culture)} instalación(es)</span></p>");

        if (office.Installations.Count > 0)
        {
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Producto</th><th>Versión</th><th>Arquitectura</th>" +
                               "<th>Tipo</th><th>Canal</th></tr></thead><tbody>");
            foreach (var installation in office.Installations)
            {
                builder.AppendLine($"<tr><td>{H(installation.ProductName)}</td><td class=\"mono\">{H(installation.Version)}</td>" +
                                   $"<td>{H(installation.Architecture)}</td><td>{H(installation.InstallType)}</td>" +
                                   $"<td>{H(installation.UpdateChannel)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (office.Licenses.Count > 0)
        {
            builder.AppendLine("<h4>Licencias locales</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Producto</th><th>Estado</th><th>Descripción</th></tr></thead><tbody>");
            foreach (var license in office.Licenses)
            {
                builder.AppendLine($"<tr><td>{H(license.ProductName)}</td><td>{H(license.LicenseStatusText)}</td>" +
                                   $"<td>{H(license.Description)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (!string.IsNullOrWhiteSpace(office.DetectionNote))
        {
            builder.AppendLine($"<p class=\"fine\">{H(office.DetectionNote)}</p>");
        }

        if (!string.IsNullOrWhiteSpace(office.SubscriptionNote))
        {
            builder.AppendLine($"<p class=\"fine\">{H(office.SubscriptionNote)}</p>");
        }

        builder.AppendLine("</section>");
    }

    private static void AppendSecurity(StringBuilder builder, ComputerReport computer)
    {
        var category = computer.Inventory!.Security;
        var security = category.Data;
        builder.AppendLine("<section class=\"block\">");
        builder.AppendLine("<h3>Seguridad de Windows</h3>");
        builder.AppendLine($"<p class=\"block-status\">{Chip(category.Status)}</p>");

        if (security is null)
        {
            builder.AppendLine("<p class=\"empty\">Sin datos de seguridad.</p>");
            builder.AppendLine("</section>");
            return;
        }

        if (security.Antivirus.Count > 0)
        {
            builder.AppendLine("<h4>Antivirus</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Producto</th><th>Activo</th><th>Firmas</th><th>Fuente</th></tr></thead><tbody>");
            foreach (var antivirus in security.Antivirus)
            {
                builder.AppendLine($"<tr><td>{H(antivirus.DisplayName)}</td><td>{H(Formatting.YesNo(antivirus.Enabled))}</td>" +
                                   $"<td>{H(Formatting.YesNo(antivirus.DefinitionsUpToDate, "Actualizadas", "Desactualizadas"))}</td>" +
                                   $"<td>{H(antivirus.Source)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (security.Defender is { } defender)
        {
            builder.AppendLine("<h4>Microsoft Defender</h4>");
            builder.AppendLine("<table class=\"data\"><tbody>");
            AppendRow(builder, "Antivirus", Formatting.YesNo(defender.AntivirusEnabled));
            AppendRow(builder, "Protección en tiempo real", Formatting.YesNo(defender.RealTimeProtectionEnabled));
            AppendRow(builder, "Versión de firmas", defender.SignatureVersion);
            AppendRow(builder, "Antigüedad de firmas", defender.SignatureAgeDays is { } age ? $"{age} día(s)" : null);
            AppendRow(builder, "Protección contra manipulación", Formatting.YesNo(defender.TamperProtectionEnabled));
            builder.AppendLine("</tbody></table>");
        }

        if (security.Firewall.Count > 0)
        {
            builder.AppendLine("<h4>Firewall</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Perfil</th><th>Estado</th><th>Fuente</th></tr></thead><tbody>");
            foreach (var profile in security.Firewall)
            {
                builder.AppendLine($"<tr><td>{H(profile.Profile)}</td>" +
                                   $"<td>{H(profile.Enabled == true ? "Activo" : profile.Enabled == false ? "Inactivo" : "Sin datos")}</td>" +
                                   $"<td>{H(profile.Source)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        if (security.BitLocker.Count > 0)
        {
            builder.AppendLine("<h4>BitLocker</h4>");
            builder.AppendLine("<table class=\"data\"><thead><tr><th>Unidad</th><th>Protección</th><th>Cifrado</th><th>Método</th></tr></thead><tbody>");
            foreach (var volume in security.BitLocker)
            {
                builder.AppendLine($"<tr><td class=\"mono\">{H(volume.DriveLetter)}</td><td>{H(volume.ProtectionText)}</td>" +
                                   $"<td>{H(volume.ConversionText)}</td><td>{H(volume.EncryptionMethod)}</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
            builder.AppendLine("<p class=\"fine\">AuditaPC no lee ni muestra claves de recuperación.</p>");
        }

        builder.AppendLine("<div class=\"security-summary\">");
        builder.AppendLine("<table class=\"data\"><tbody>");
        AppendRow(builder, "TPM", security.Tpm is null
            ? "No verificable"
            : $"Presente · valor inicial habilitado: {Formatting.YesNo(security.Tpm.Enabled)} · " +
              $"valor inicial activado: {Formatting.YesNo(security.Tpm.Activated)} · {security.Tpm.SpecVersion}");
        AppendRow(builder, "Secure Boot", security.SecureBoot is null
            ? "No verificable"
            : $"{(security.SecureBoot.Enabled == true ? "Activado" : "Desactivado")} · " +
              $"firmware {Formatting.OrDash(security.SecureBoot.FirmwareType)}");
        builder.AppendLine("</tbody></table>");
        builder.AppendLine("</div>");
        builder.AppendLine("</section>");
    }

    private static void AppendChecks(StringBuilder builder, ComputerReport computer)
    {
        builder.AppendLine("<section class=\"block checks-block\">");
        builder.AppendLine("<h3>Comprobaciones y fuentes</h3>");
        builder.AppendLine("<p class=\"fine\">Cada fila conserva el estado real de la consulta. Un error o una falta de permisos " +
                           "nunca se presenta como un resultado correcto.</p>");

        foreach (var category in computer.Inventory!.Categories())
        {
            var open = category.Checks.Any(check =>
                check.Status is CheckStatus.Problem or CheckStatus.QueryError or CheckStatus.Timeout
                    or CheckStatus.PermissionDenied or CheckStatus.RequiresElevation
                    or CheckStatus.NotVerifiable) ? " open" : string.Empty;
            builder.AppendLine($"<details class=\"fold checks\"{open}>");
            builder.AppendLine($"<summary>{H(category.Title)} {Chip(category.Status)}</summary>");
            builder.AppendLine("<table class=\"data checks\"><thead><tr><th>Comprobación</th><th>Estado</th>" +
                               "<th>Detalle</th><th>Fuente</th></tr></thead><tbody>");
            foreach (var check in category.Checks)
            {
                builder.AppendLine("<tr>");
                builder.AppendLine($"<td>{H(check.Label)}</td>");
                builder.AppendLine($"<td>{Chip(check.Status)}</td>");
                builder.AppendLine($"<td>{H(check.Detail)}</td>");
                builder.AppendLine($"<td class=\"fine\">{H(check.Source)}</td>");
                builder.AppendLine("</tr>");
            }

            builder.AppendLine("</tbody></table>");
            builder.AppendLine("</details>");
        }

        builder.AppendLine("</section>");
    }

    private static void AppendPhotos(StringBuilder builder, ComputerReport computer)
    {
        if (computer.Record.Photos.Count == 0)
        {
            return;
        }

        builder.AppendLine("<section class=\"block photos\">");
        builder.AppendLine("<h3>Fotografías</h3>");
        builder.AppendLine("<div class=\"photo-grid\">");
        foreach (var photo in computer.Photos)
        {
            builder.AppendLine("<figure>");
            if (photo.Embedded)
            {
                builder.AppendLine($"<img src=\"{photo.DataUri}\" alt=\"{H(photo.Attachment.Description ?? photo.Attachment.OriginalFileName)}\">");
            }
            else
            {
                builder.AppendLine($"<div class=\"photo-missing\">Vista no disponible<br><span class=\"mono\">" +
                                   $"{H(photo.Attachment.DisplayPath)}</span></div>");
            }

            builder.AppendLine("<figcaption>");
            if (!string.IsNullOrWhiteSpace(photo.Attachment.Description))
            {
                builder.AppendLine($"<strong>{H(photo.Attachment.Description)}</strong>");
            }

            builder.AppendLine($"<span>{H(photo.Attachment.OriginalFileName)} · " +
                               $"{Formatting.Date(photo.Attachment.AddedAt)} · " +
                               $"{Formatting.Bytes(photo.Attachment.SizeBytes)}</span>");
            builder.AppendLine("</figcaption>");
            builder.AppendLine("</figure>");
        }

        builder.AppendLine("</div>");
        builder.AppendLine("</section>");
    }

    private static void AppendHistory(StringBuilder builder, ComputerReport computer)
    {
        if (computer.Record.Captures.Count == 0)
        {
            return;
        }

        builder.AppendLine("<section class=\"block history\">");
        builder.AppendLine("<h3>Historial de capturas</h3>");
        builder.AppendLine("<table class=\"data\"><thead><tr><th>Fecha</th><th>Identificador</th>" +
                           "<th>Archivo</th><th>SHA-256</th><th>Versión</th></tr></thead><tbody>");
        foreach (var capture in computer.Record.Captures.OrderByDescending(item => item.CapturedAt))
        {
            var current = capture.Id == computer.Record.CurrentCapture?.Id ? " <span class=\"tag\">vigente</span>" : string.Empty;
            builder.AppendLine($"<tr><td>{H(Formatting.DateTimeLong(capture.CapturedAt))}</td>" +
                               $"<td class=\"mono\">{H(capture.Id)}{current}</td>" +
                               $"<td class=\"mono fine\">{H(capture.File)}</td>" +
                               $"<td class=\"mono fine\">{H(ShortHash(capture.Sha256))}</td>" +
                               $"<td>{H(capture.AppVersion)}</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
        builder.AppendLine("<p class=\"fine\">Las capturas son inmutables y se conservan todas. La captura vigente es la que se usa en este informe.</p>");
        builder.AppendLine("</section>");
    }

    private static void AppendFooter(StringBuilder builder, AuditReportData data)
    {
        builder.AppendLine("<footer class=\"doc-footer\">");
        builder.AppendLine($"<span>AuditaPC · {H(data.Manifest.CompanyName)} · {data.Manifest.StartDate:dd-MM-yyyy}</span>");
        builder.AppendLine($"<span>Generado localmente el {data.GeneratedAt.ToString("dd-MM-yyyy HH:mm", Culture)} · " +
                           $"AuditaPC {H(data.AppVersion)}</span>");
        builder.AppendLine("</footer>");
    }

    private static readonly CheckStatus[] StatusOrder =
    {
        CheckStatus.Problem,
        CheckStatus.QueryError,
        CheckStatus.Timeout,
        CheckStatus.PermissionDenied,
        CheckStatus.RequiresElevation,
        CheckStatus.NotVerifiable,
        CheckStatus.Partial,
        CheckStatus.NotTestedOffline,
        CheckStatus.KnownLimitation,
        CheckStatus.Detected,
        CheckStatus.Verified,
        CheckStatus.NotDetected,
        CheckStatus.NotApplicable
    };

    private static void AppendKpi(StringBuilder builder, string value, string label, string? tone = null)
    {
        var css = string.IsNullOrWhiteSpace(tone) ? string.Empty : " " + tone;
        builder.AppendLine($"<div class=\"kpi{css}\"><span class=\"kpi-value\">{H(value)}</span>" +
                           $"<span class=\"kpi-label\">{H(label)}</span></div>");
    }

    private static void AppendLegend(StringBuilder builder, CheckStatus status, string description)
    {
        builder.AppendLine($"<dt>{Chip(status)}</dt><dd>{H(description)}</dd>");
    }

    private static void AppendMeta(StringBuilder builder, string label, string? value)
    {
        builder.AppendLine($"<div><dt>{H(label)}</dt><dd>{H(Value(value))}</dd></div>");
    }

    private static void AppendRow(StringBuilder builder, string label, string? value)
    {
        builder.AppendLine($"<tr><th>{H(label)}</th><td>{H(Value(value))}</td></tr>");
    }

    private static string Chip(CheckStatus status)
    {
        return $"<span class=\"status {StatusClass(status)}\"><span class=\"dot\"></span>" +
               $"{H(status.ToDisplayText())}</span>";
    }

    private static string StatusClass(CheckStatus status) => status switch
    {
        CheckStatus.Verified => "s-verified",
        CheckStatus.Detected => "s-detected",
        CheckStatus.NotDetected => "s-notdetected",
        CheckStatus.Problem => "s-problem",
        CheckStatus.Partial => "s-partial",
        CheckStatus.NotVerifiable => "s-notverifiable",
        CheckStatus.RequiresElevation => "s-elevation",
        CheckStatus.PermissionDenied => "s-permission",
        CheckStatus.NotTestedOffline => "s-offline",
        CheckStatus.KnownLimitation => "s-limitation",
        CheckStatus.NotApplicable => "s-notapplicable",
        CheckStatus.QueryError => "s-queryerror",
        CheckStatus.Timeout => "s-timeout",
        _ => "s-notverifiable"
    };

    private static IEnumerable<ComputerReport> OrderedComputers(AuditReportData data) =>
        data.Computers
            .OrderBy(computer => string.IsNullOrWhiteSpace(computer.Record.Manual.AssetCode)
                ? computer.DisplayName
                : computer.Record.Manual.AssetCode!, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(computer => computer.DisplayName, StringComparer.CurrentCultureIgnoreCase);

    private static string Anchor(ComputerReport computer) =>
        "pc-" + (computer.Record.Id.Length >= 10 ? computer.Record.Id[..10] : computer.Record.Id);

    private static string Value(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string Join(params string?[] values)
    {
        var parts = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim());
        var joined = string.Join(" · ", parts);
        return joined.Length == 0 ? "—" : joined;
    }

    private static string? JoinBuild(string? version, string? build, int? revision)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(version))
        {
            parts.Add(version.Trim());
        }

        if (!string.IsNullOrWhiteSpace(build))
        {
            parts.Add(build.Trim());
        }

        if (revision is { } number)
        {
            parts.Add(number.ToString(Culture));
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string ShortHash(string? hash) =>
        string.IsNullOrWhiteSpace(hash) ? "—" : hash.Length <= 16 ? hash : hash[..16] + "…";

    private static string ShortId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Length <= 8 ? value : value[..8].ToUpperInvariant();

    private static readonly HtmlEncoder Encoder =
        HtmlEncoder.Create(UnicodeRanges.All);

    private static string H(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : Encoder.Encode(value);

    private const string Css = """
:root{
  --ink:#1c2430; --muted:#5c6674; --soft:#7a8492;
  --paper:#fbfaf7; --card:#ffffff; --line:#dcd8cf; --line-strong:#b9b3a7;
  --accent:#0f6b63; --accent-soft:#e6f1ef;
  --ok:#166534; --ok-soft:#e7f4ea;
  --info:#1e4e8c; --info-soft:#e9eff8;
  --warn:#9a5b06; --warn-soft:#fbf0d9;
  --danger:#a62a20; --danger-soft:#fbe9e6;
  --violet:#5b3a86; --violet-soft:#efe9f7;
  --gray:#55606d; --gray-soft:#eef0f2;
}
*{box-sizing:border-box}
html{font-size:15px}
body{
  margin:0; background:var(--paper); color:var(--ink);
  font-family:"Segoe UI","Inter",system-ui,-apple-system,sans-serif;
  line-height:1.55; -webkit-font-smoothing:antialiased;
}
h1,h2,h3,h4{line-height:1.2; margin:0 0 .45em; font-weight:650; letter-spacing:-.01em}
h1{font-size:2.6rem}
h2{font-size:1.7rem; margin-top:0}
h3{font-size:1.18rem; margin-top:1.4em}
h4{font-size:.98rem; margin-top:1.1em; color:var(--ink)}
p{margin:.4em 0}
a{color:var(--accent); text-decoration:none}
a:hover{text-decoration:underline}
ul.tight{margin:.4em 0; padding-left:1.15em}
ul.tight li{margin:.22em 0}
.mono{font-family:"Cascadia Mono","Consolas",monospace; font-size:.86em}
.fine{font-size:.84rem; color:var(--muted)}
.empty{color:var(--soft); font-style:italic}
.eyebrow{text-transform:uppercase; letter-spacing:.14em; font-size:.72rem; font-weight:700; color:var(--accent); margin:0 0 .5em}
.cover{
  background:#1d2c37;
  color:#f4f1e8; padding:52px 56px 44px; position:relative; overflow:hidden;
  border-bottom:5px solid var(--accent);
}
.cover-top{display:flex; align-items:baseline; gap:14px; border-bottom:1px solid rgba(255,255,255,.18); padding-bottom:14px}
.brand{font-weight:750; letter-spacing:.16em; text-transform:uppercase; font-size:.95rem}
.brand-sub{font-size:.78rem; color:rgba(244,241,232,.62); letter-spacing:.06em}
.cover-main{margin:54px 0 38px; max-width:760px}
.cover .eyebrow{color:#8fd3c9}
.cover h1{font-size:3.1rem; margin:0 0 .2em; color:#fff}
.cover-lead{font-size:1.05rem; color:rgba(244,241,232,.78); max-width:640px}
.cover-meta{display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:18px 28px;
  margin:0; padding:22px 0; border-top:1px solid rgba(255,255,255,.16); border-bottom:1px solid rgba(255,255,255,.16)}
.cover-meta div{min-width:0}
.cover-meta dt{font-size:.68rem; letter-spacing:.12em; text-transform:uppercase; color:rgba(244,241,232,.55)}
.cover-meta dd{margin:.25em 0 0; font-size:.95rem; overflow-wrap:anywhere}
.cover-note{font-size:.8rem; color:rgba(244,241,232,.6); max-width:820px; margin-top:20px}
.report-tools{max-width:1080px; margin:22px auto 0; padding:0 40px}
.report-tools-row{display:flex; align-items:end; gap:10px; flex-wrap:wrap; padding-bottom:14px; border-bottom:1px solid var(--line)}
.filter-control{display:grid; gap:5px; flex:1 1 320px}
.filter-control label{font-size:.72rem; font-weight:700; letter-spacing:.08em; text-transform:uppercase; color:var(--muted)}
.filter-control input{width:100%; min-height:40px; border:1px solid var(--line-strong); border-radius:4px; padding:8px 11px; color:var(--ink); background:#fff; font:inherit}
.filter-control input:focus{outline:2px solid var(--accent); outline-offset:1px}
.report-tools button{min-height:40px; border:1px solid var(--line-strong); border-radius:4px; padding:8px 13px; background:#fff; color:var(--ink); font:inherit; font-size:.86rem; cursor:pointer}
.report-tools button:hover{border-color:var(--accent); color:var(--accent)}
.result-count{margin-left:auto; padding-bottom:10px; color:var(--muted); font-size:.8rem; font-variant-numeric:tabular-nums}
[hidden],section.computer[hidden],[data-equipment-row][hidden]{display:none!important}
main{max-width:1080px; margin:0 auto; padding:0 40px}
section.summary,section.legend{padding:44px 0 8px}
.summary .lede{font-size:1.05rem; color:var(--muted); max-width:820px}
.field-notes{max-width:1080px; margin:26px auto 0; padding:28px 40px; border-top:1px solid var(--line-strong)}
.field-notes h2{font-size:1.45rem; margin:0 0 6px}
.field-notes-grid{display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:12px; margin-top:16px}
.field-note{padding:14px 16px; border:1px solid var(--line); border-radius:9px; background:var(--card); break-inside:avoid}
.field-note h3{margin:0 0 8px; font-size:.9rem}
.field-note p{margin:0; white-space:pre-wrap; overflow-wrap:anywhere; font-size:.9rem; line-height:1.55}
.kpis{display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:14px; margin:26px 0 30px}
.kpi{background:var(--card); border:1px solid var(--line); border-radius:10px; padding:18px 18px 15px;
  display:flex; flex-direction:column; gap:4px; box-shadow:0 1px 0 rgba(28,36,48,.03)}
.kpi-value{font-size:1.9rem; font-weight:700; letter-spacing:-.03em}
.kpi-label{font-size:.8rem; color:var(--muted)}
.kpi.problem .kpi-value{color:var(--danger)} .kpi.warn .kpi-value{color:var(--warn)} .kpi.ok .kpi-value{color:var(--ok)}
.summary-grid,.legend-grid{display:grid; grid-template-columns:1fr 1fr; gap:20px; align-items:start}
.panel{background:var(--card); border:1px solid var(--line); border-radius:10px; padding:20px 22px}
.panel h3{margin-top:0}
table{border-collapse:collapse; width:100%; margin:.5em 0 1em}
table.data,table.compact{font-size:.9rem}
th,td{text-align:left; vertical-align:top; padding:.5em .6em; border-bottom:1px solid var(--line)}
thead th{font-size:.7rem; letter-spacing:.08em; text-transform:uppercase; color:var(--muted);
  border-bottom:1px solid var(--line-strong); font-weight:700}
tbody tr:nth-child(even){background:rgba(28,36,48,.018)}
table.data th[scope],table.data tbody th{width:31%; color:var(--muted); font-weight:600; border-bottom:1px solid var(--line)}
td.num,th.num{text-align:right; font-variant-numeric:tabular-nums}
.status{display:inline-flex; align-items:center; gap:.42em; font-size:.76rem; font-weight:650;
  padding:.18em .55em .2em; border-radius:999px; white-space:nowrap}
.status .dot{width:.46em; height:.46em; border-radius:50%; background:currentColor; flex:none}
.s-verified{color:var(--ok); background:var(--ok-soft)}
.s-detected{color:var(--info); background:var(--info-soft)}
.s-notdetected{color:var(--gray); background:var(--gray-soft)}
.s-problem{color:var(--danger); background:var(--danger-soft)}
.s-partial{color:var(--warn); background:var(--warn-soft)}
.s-notverifiable{color:var(--violet); background:var(--violet-soft)}
.s-elevation{color:#8a5a00; background:#fff2d8}
.s-permission{color:#a62a20; background:var(--danger-soft)}
.s-offline{color:#496579; background:#eaf0f4}
.s-limitation{color:#65717d; background:var(--gray-soft)}
.s-notapplicable{color:var(--soft); background:var(--gray-soft)}
.s-queryerror{color:#8a1c14; background:#f7e2df}
.s-timeout{color:#9a4a06; background:#fae8d7}
.status-table td{vertical-align:middle}
.bar{display:block; height:7px; background:var(--gray-soft); border-radius:999px; overflow:hidden; min-width:80px}
.bar-fill{display:block; height:100%; border-radius:999px; background:var(--accent)}
.bar-fill.s-problem{background:var(--danger)} .bar-fill.s-partial{background:var(--warn)}
.bar-fill.s-notverifiable{background:var(--violet)} .bar-fill.s-verified{background:var(--ok)}
.bar-fill.s-detected{background:var(--info)} .bar-fill.s-permission{background:var(--danger)}
.bar-fill.s-elevation{background:#b07818} .bar-fill.s-offline{background:#496579}
.bar-fill.s-queryerror{background:#8a1c14} .bar-fill.s-timeout{background:#b5651d}
.notice{border-radius:10px; padding:16px 18px; margin:22px 0; border:1px solid var(--line); background:var(--card)}
.notice.warn{border-color:#e4c98f; background:var(--warn-soft)}
.notice strong{display:block; margin-bottom:.25em}
.notice ul{margin:.2em 0 0; padding-left:1.2em}
.status-legend{display:grid; grid-template-columns:auto 1fr; gap:.5em 1em; margin:0}
.status-legend dt{margin:0}
.status-legend dd{margin:0; color:var(--muted); font-size:.86rem}
section.computer{border-top:1px solid var(--line-strong); margin-top:54px; padding-top:38px}
.computer-head{display:flex; justify-content:space-between; align-items:flex-start; gap:24px; flex-wrap:wrap}
.computer-head h2{font-size:2rem; margin:0}
.computer-sub{color:var(--muted); margin:.2em 0 0}
.computer-tags{display:flex; gap:8px; flex-wrap:wrap}
.tag{display:inline-block; font-size:.72rem; letter-spacing:.03em; color:var(--muted);
  background:var(--card); border:1px solid var(--line); border-radius:999px; padding:.2em .65em; vertical-align:middle}
.identity-card{background:var(--card); border:1px solid var(--line); border-radius:10px; padding:6px 22px; margin:24px 0 8px}
.identity-grid{display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:0 28px; margin:0}
.identity-grid > div{display:grid; grid-template-columns:44% 56%; gap:8px; padding:9px 0; border-bottom:1px solid var(--line)}
.identity-grid dt{font-size:.72rem; text-transform:uppercase; letter-spacing:.07em; color:var(--muted); margin:0; padding-top:.18em}
.identity-grid dd{margin:0; overflow-wrap:anywhere; font-size:.9rem}
.block{margin-top:34px}
.block-status{margin:-.2em 0 .8em}
.stat-line{display:flex; gap:22px; flex-wrap:wrap; color:var(--muted); margin:.4em 0 1em}
.stat-line strong{color:var(--ink)}
.manual-grid{display:grid; grid-template-columns:1fr 1fr; gap:14px}
.manual-field{background:var(--card); border:1px solid var(--line); border-left:3px solid var(--accent); border-radius:8px; padding:14px 16px}
.manual-field h4{margin:0 0 .3em; font-size:.72rem; letter-spacing:.1em; text-transform:uppercase; color:var(--accent)}
.manual-field p{margin:0; white-space:pre-wrap}
.adapter-grid{display:grid; grid-template-columns:1fr 1fr; gap:14px}
.adapter{background:var(--card); border:1px solid var(--line); border-radius:9px; padding:16px 18px}
.adapter h4{margin:0 0 .15em}
.adapter-sub{font-size:.8rem; color:var(--muted); margin:0 0 .6em}
.adapter-meta{margin:0; display:grid; grid-template-columns:38% 62%; gap:.28em 10px; font-size:.84rem}
.adapter-meta dt{color:var(--muted); margin:0}
.adapter-meta dd{margin:0; overflow-wrap:anywhere}
.fold{border:1px solid var(--line); border-radius:9px; background:var(--card); padding:0 16px; margin:14px 0}
.fold > summary{cursor:pointer; padding:12px 0; font-weight:600; list-style:none; display:flex; gap:10px; align-items:center; flex-wrap:wrap}
.fold > summary::-webkit-details-marker{display:none}
.fold > summary:before{content:"+"; display:inline-block; width:1.1em; color:var(--accent); font-weight:700}
.fold[open] > summary:before{content:"–"}
.fold[open]{padding-bottom:8px}
.checks summary{font-size:.94rem}
table.checks td{font-size:.84rem}
.photo-grid{display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:16px}
.photo-grid figure{margin:0; background:var(--card); border:1px solid var(--line); border-radius:9px; overflow:hidden}
.photo-grid img{display:block; width:100%; height:auto; max-height:420px; object-fit:cover; background:#eee}
.photo-grid figcaption{padding:10px 12px; font-size:.8rem; color:var(--muted); display:flex; flex-direction:column; gap:.15em}
.photo-grid figcaption strong{color:var(--ink); font-weight:600}
.photo-missing{padding:38px 14px; text-align:center; color:var(--soft); background:var(--gray-soft); font-size:.85rem}
.doc-footer{max-width:1080px; margin:56px auto 0; padding:18px 40px 40px; border-top:1px solid var(--line-strong);
  display:flex; justify-content:space-between; gap:18px; flex-wrap:wrap; color:var(--muted); font-size:.78rem}
@page{size:A4; margin:15mm 13mm 17mm}
@media print{
  .report-tools{display:none}
  html{font-size:11.5px}
  body{background:#fff}
  main{max-width:none; padding:0}
  .cover{background:#16212b !important; -webkit-print-color-adjust:exact; print-color-adjust:exact; padding:34px 30px}
  .cover h1{font-size:2.5rem}
  section.summary,section.legend{padding-top:24px}
  section.computer{page-break-before:always; border-top:0; margin-top:0; padding-top:0}
  .kpi,.panel,.adapter,.manual-field,.identity-card,.notice,figure{break-inside:avoid}
  details{display:block}
  details > summary{list-style:none}
  .fold{border:0; padding:0}
  .fold > summary:before{content:""}
  .doc-footer{position:fixed; bottom:0; left:0; right:0; max-width:none; padding:0 13mm;
    background:#fff; border-top:1px solid #ccc; font-size:.7rem}
  a{color:inherit}
}
@media (max-width:820px){
  .report-tools{padding:0 22px}
  .result-count{margin-left:0}
  main{padding:0 22px}
  .cover{padding:34px 26px}
  .cover h1{font-size:2.2rem}
  .cover-meta,.kpis,.summary-grid,.legend-grid,.identity-grid,.manual-grid,.adapter-grid,.photo-grid{grid-template-columns:1fr}
}

/* Diseño editorial del informe consolidado */
:root{
  --paper:#f4f6f8; --card:#fff; --ink:#182431; --muted:#657382;
  --line:#dce6f1; --line-strong:#c7d5e4; --accent:#126ee1; --accent-soft:#eaf3ff;
}
body{background:var(--paper); color:var(--ink); line-height:1.58}
main#main-content{width:100%; max-width:none; margin:0; padding:0}
.container{width:min(1180px, calc(100% - 64px)); margin-left:auto; margin-right:auto}
.topbar{background:#082846; color:#f6f8fa; border-bottom:3px solid #126ee1}
.topinner{min-height:76px; display:flex; justify-content:space-between; align-items:center; gap:24px}
.brand-lockup{display:flex; align-items:center; gap:12px; min-width:0}
.brand-mark{display:grid; place-items:center; width:38px; height:38px; border-radius:10px; background:#126ee1; color:#fff; font-weight:800; font-size:1.2rem}
.brand-text,.client-name{display:flex; flex-direction:column; line-height:1.25}
.brand-text strong{font-size:1rem; letter-spacing:.06em}
.brand-text small,.client-name small{font-size:.7rem; color:#b8c8d2}
.brand-divider{height:34px; width:1px; background:#526675; margin:0 8px}
.client-name strong{max-width:360px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap}
.top-status{text-align:right; color:#bdcbd4; font-size:.76rem}
.top-status strong{color:#fff; font-size:.88rem}
.nav{background:#fff; border-bottom:1px solid var(--line); position:sticky; top:0; z-index:10}
.navinner{display:flex; gap:6px; align-items:center; overflow-x:auto; min-height:46px}
.nav a{display:block; padding:12px 10px; color:#405363; white-space:nowrap; font-size:.78rem; font-weight:650; border-bottom:2px solid transparent}
.nav a:hover{color:var(--accent); border-bottom-color:var(--accent); text-decoration:none}
.cover.container{width:min(1180px, calc(100% - 64px)); margin-top:28px; border:0; border-radius:16px; padding:42px 48px 24px; background:linear-gradient(115deg,#082846 0%,#123d66 65%,#126ee1 100%); box-shadow:0 14px 34px rgba(24,45,60,.12)}
.cover-main{margin:0; max-width:900px}
.cover .eyebrow{color:#8ed4ca}
.cover h1{max-width:760px; font-size:clamp(2.1rem,4vw,3.25rem); letter-spacing:-.04em}
.cover-lead{max-width:780px; font-size:1rem; color:#d7e2e8}
.cover-lead strong{color:#fff}
.cover-rule{display:flex; flex-wrap:wrap; gap:10px 24px; margin-top:28px; padding-top:18px; border-top:1px solid rgba(255,255,255,.18); color:#d8e5eb; font-size:.82rem}
.cover-bottom{display:flex; justify-content:space-between; align-items:center; gap:18px; margin-top:26px; padding-top:16px; border-top:1px solid rgba(255,255,255,.15); color:#c0d0d8; font-size:.75rem}
.action-btn{border:1px solid transparent; border-radius:8px; padding:10px 14px; font:inherit; font-size:.82rem; font-weight:700; cursor:pointer; transition:background .15s,border-color .15s,transform .15s}
.action-btn:hover{transform:translateY(-1px)}
.action-btn.primary{background:#eaf3ff; color:#0e559f}
.action-btn.primary:hover{background:#fff}
.action-btn.secondary{background:#fff; border-color:var(--line-strong); color:#334858}
.action-btn.secondary:hover{border-color:var(--accent); color:var(--accent)}
.section-space{padding-top:48px; scroll-margin-top:70px}
.section-heading{display:flex; justify-content:space-between; align-items:end; gap:20px; margin-bottom:20px}
.section-heading h2{font-size:1.75rem; letter-spacing:-.03em; margin:0 0 6px}
.section-heading .eyebrow{margin-bottom:7px}
.section-lead{margin:0; color:var(--muted); max-width:780px; font-size:.92rem}
.section-meta{color:var(--muted); font-size:.78rem; white-space:nowrap}
.summary .lede{padding:14px 16px; background:#fff; border-left:3px solid var(--accent); border-radius:0 8px 8px 0; font-size:.9rem}
.kpis{grid-template-columns:repeat(4,minmax(0,1fr)); gap:14px; margin:22px 0}
.kpi{border:1px solid var(--line); border-radius:12px; padding:18px 20px; box-shadow:0 3px 12px rgba(28,48,64,.035)}
.kpi-value{font-size:2rem; color:#1d3445}
.kpi-label{font-size:.76rem; font-weight:600}
.intro-grid{display:grid; grid-template-columns:1.2fr .8fr; gap:16px; margin:20px 0}
.intro-grid .panel{border-radius:12px; padding:20px 22px}
.intro-grid .panel h3{margin:0 0 10px}
.intro-grid .panel p{margin:0; white-space:pre-wrap}
.factlist{display:grid; grid-template-columns:1fr 1fr; gap:0 18px; margin:0}
.factlist > div{padding:8px 0; border-bottom:1px solid var(--line)}
.factlist dt{font-size:.68rem; color:var(--muted); text-transform:uppercase; letter-spacing:.06em}
.factlist dd{margin:3px 0 0; font-size:.84rem; font-weight:650; overflow-wrap:anywhere}
.overview-panel{padding:20px 22px; margin-top:12px}
.panel-heading{display:flex; justify-content:space-between; align-items:start; gap:20px}
.panel-heading h3{margin:0 0 4px}
.panel-heading strong{font-size:1.25rem; white-space:nowrap}
.panel-heading small{font-size:.7rem; font-weight:500; color:var(--muted)}
.status-overview{display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:10px 24px; margin-top:18px}
.status-overview-row{display:grid; grid-template-columns:minmax(150px,1fr) 32px minmax(90px,1.2fr); gap:10px; align-items:center}
.status-overview-row .bar{min-width:0}
.inventory-toolbar{display:grid; grid-template-columns:minmax(240px,1.5fr) minmax(170px,.7fr) auto auto; gap:10px; align-items:end; padding:16px; border:1px solid var(--line); border-radius:12px 12px 0 0; background:#fff}
.search-field,.filter-field{display:grid; gap:5px; min-width:0}
.search-field span,.filter-field span{font-size:.7rem; font-weight:750; letter-spacing:.06em; text-transform:uppercase; color:var(--muted)}
.search-field input,.filter-field select{height:40px; min-width:0; width:100%; padding:8px 11px; border:1px solid var(--line-strong); border-radius:7px; background:#fff; color:var(--ink); font:inherit; font-size:.84rem}
.search-field input:focus,.filter-field select:focus{outline:2px solid #8bc9c1; border-color:var(--accent)}
.inventory-count{margin:12px 0 8px; text-align:right; color:var(--muted); font-size:.76rem; font-variant-numeric:tabular-nums}
.table-wrap{width:100%; overflow-x:auto; border:1px solid var(--line); border-radius:12px; background:#fff}
.table-wrap table{margin:0; min-width:760px}
.table-wrap thead th{background:#f7f9fa; padding:12px 11px; white-space:nowrap}
.table-wrap tbody td{padding:11px; font-size:.82rem}
.table-wrap tbody tr:hover{background:#f4f9f8}
.inventory-link{font-weight:750}
.state-pill{display:inline-flex; align-items:center; border-radius:999px; padding:4px 9px; font-size:.68rem; font-weight:750; white-space:nowrap}
.state-ok{color:#166534; background:#e8f5eb}
.state-review{color:#87530a; background:#fff3d9}
.state-problems{color:#a12a22; background:#fce9e6}
.state-missing{color:#5c6674; background:#eef1f4}
.inventory-empty{padding:18px; text-align:center}
.field-notes{max-width:none; margin:0; padding:0; border:0}
.field-notes-grid{grid-template-columns:repeat(2,minmax(0,1fr)); gap:14px}
.field-note{border-radius:12px; padding:18px; box-shadow:0 3px 12px rgba(28,48,64,.03)}
.field-note h3{margin:0 0 8px; color:var(--accent); font-size:.82rem}
.field-note p{font-size:.88rem}
.equipment-list{padding-top:48px}
.equipment-list > .section-heading{margin-bottom:18px}
details.equipment-card{margin:14px 0; border:1px solid var(--line); border-radius:14px; background:#fff; box-shadow:0 4px 16px rgba(28,48,64,.035); overflow:hidden; scroll-margin-top:68px}
.equipment-card > summary{list-style:none; cursor:pointer}
.equipment-card > summary::-webkit-details-marker{display:none}
.computer-summary{padding:20px 22px; display:flex; align-items:center; justify-content:space-between; gap:18px; background:linear-gradient(90deg,#fff,#f9fbfc)}
.computer-summary:hover{background:#f4f9f8}
.computer-summary .computer-head{flex:1; align-items:center}
.computer-summary .computer-head h2{font-size:1.3rem}
.computer-summary .eyebrow{margin:0 0 5px}
.computer-tags{justify-content:flex-end; max-width:55%}
.open-hint{display:inline-flex; align-items:center; gap:8px; color:var(--accent); font-size:.75rem; font-weight:750; white-space:nowrap}
.equipment-card[open] > summary{border-bottom:1px solid var(--line)}
.equipment-card[open] .open-hint span{transform:rotate(45deg)}
.computer-content{padding:2px 24px 26px}
.computer-content .block{margin-top:26px}
.computer-content .block h3{margin-top:0; padding-bottom:8px; border-bottom:1px solid var(--line); font-size:1.05rem}
.computer-content .identity-card{margin:20px 0}
.computer-content .data{font-size:.82rem}
.computer-content .fold{margin:10px 0}
.action-card{display:grid; grid-template-columns:minmax(140px,.35fr) 1fr; gap:8px 18px; margin:12px 0; padding:18px 20px; border:1px solid var(--line); border-radius:12px; background:#fff}
.action-card h3{grid-column:1/-1; margin:0; font-size:1rem}
.action-card > div{padding:8px 0}
.action-card p{margin:5px 0 0; white-space:pre-wrap}
.action-kind{display:inline-block; color:var(--accent); font-size:.68rem; font-weight:800; text-transform:uppercase; letter-spacing:.08em}
.empty-panel{padding:22px; border:1px dashed var(--line-strong); border-radius:12px; background:#fff; color:var(--muted); text-align:center}
.legend{padding-bottom:30px}
.legend-grid{grid-template-columns:1fr 1fr}
.legend .panel{border-radius:12px}
.report-guide{width:min(1040px,calc(100% - 28px)); max-width:none; max-height:min(88vh,920px); overflow-y:auto; border:1px solid var(--line-strong); border-radius:14px; padding:28px; background:var(--paper); color:var(--ink); box-shadow:0 20px 64px rgba(10,28,46,.24)}
.report-guide::backdrop{background:rgba(8,27,42,.64)}
.guide-header{display:flex; align-items:flex-start; justify-content:space-between; gap:20px; margin-bottom:22px; padding-bottom:18px; border-bottom:1px solid var(--line-strong)}
.guide-header h2{margin:0 0 8px}
.guide-close{flex:none; border:1px solid var(--line-strong); border-radius:7px; background:white; color:var(--ink); font:inherit; font-size:.84rem; font-weight:700; padding:8px 12px; cursor:pointer}
.guide-close:hover{border-color:var(--accent); color:var(--accent)}
.report-guide .legend-grid{display:grid; grid-template-columns:1fr 1fr; gap:14px}
.report-guide .panel{min-width:0}
.report-guide .status-legend{grid-template-columns:minmax(140px,auto) minmax(0,1fr)}
.nav .guide-trigger{display:block; white-space:nowrap; margin-left:auto; padding:10px 12px; border:1px solid var(--line-strong); border-radius:7px; background:#eef5fb; color:#164c76; font:inherit; font-size:.78rem; font-weight:750; cursor:pointer}
.nav .guide-trigger:hover,.nav .guide-trigger:focus-visible{background:#dcecf9; border-color:#74a6cb; outline-offset:2px}
.doc-footer{max-width:1180px; margin:42px auto 0; padding:18px 0 36px}
@media(max-width:900px){
  .container,.cover.container{width:min(100% - 36px,1180px)}
  .inventory-toolbar{grid-template-columns:1fr 1fr}
  .inventory-toolbar .action-btn{width:100%}
  .computer-summary{align-items:flex-start; flex-direction:column}
  .computer-summary .computer-head{width:100%; align-items:flex-start}
  .computer-tags{max-width:100%; justify-content:flex-start}
}
@media(max-width:640px){
  .container,.cover.container{width:calc(100% - 28px)}
  .topinner{padding:10px 0; align-items:flex-start; flex-direction:column; gap:8px}
  .top-status{text-align:left}
  .brand-divider{display:none}
  .client-name{display:none}
  .cover.container{padding:28px 22px 20px; margin-top:16px}
  .cover h1{font-size:2rem}
  .cover-bottom{align-items:flex-start; flex-direction:column}
  .section-space{padding-top:34px}
  .section-heading{align-items:flex-start; flex-direction:column; gap:8px}
  .kpis{grid-template-columns:repeat(2,minmax(0,1fr)); gap:9px}
  .kpi{padding:14px}
  .kpi-value{font-size:1.6rem}
  .status-overview{grid-template-columns:1fr}
  .inventory-toolbar{grid-template-columns:1fr}
  .field-notes-grid,.legend-grid,.intro-grid,.report-guide .legend-grid{grid-template-columns:1fr}
  .report-guide{padding:18px; max-height:90vh}
  .guide-header{gap:10px; flex-direction:column}
  .report-guide .status-legend{grid-template-columns:1fr}
  .report-guide .status-legend dd{margin-bottom:7px}
  .factlist{grid-template-columns:1fr}
  .computer-content{padding:2px 14px 18px}
  .computer-summary{padding:16px}
  .computer-summary .computer-head h2{font-size:1.15rem}
  .action-card{grid-template-columns:1fr}
}
@media print{
  body{background:#fff}
  .container,.cover.container{width:100%}
  .nav,.report-guide,.inventory-toolbar,.inventory-count,.open-hint,.cover-bottom .action-btn{display:none!important}
  .cover.container{margin:0 0 18px; border-radius:0; box-shadow:none; -webkit-print-color-adjust:exact; print-color-adjust:exact}
  .section-space{padding-top:24px}
  .kpi,.panel,.field-note,.action-card,.identity-card,.notice,figure{break-inside:avoid}
  details.equipment-card{break-before:page; box-shadow:none; border:0; border-radius:0; overflow:visible}
  details.equipment-card > summary{display:block; background:#fff; border-bottom:1px solid #bbb; padding:0 0 10px}
  details.equipment-card:not([open]) > .computer-content{display:block}
  .computer-content{padding:0}
  .table-wrap{overflow:visible; border:0}
  .table-wrap table{min-width:0}
  .table-wrap thead{display:table-header-group}
  tr{break-inside:avoid}
  .doc-footer{position:static; max-width:none; margin-top:24px; padding:12px 0; border-top:1px solid #bbb}
}
""";
}
