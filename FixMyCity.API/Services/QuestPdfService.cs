// FixMyCity.API/Services/QuestPdfService.cs
// Requires NuGet: QuestPDF (>=2024.3)
// Add to .csproj:  <PackageReference Include="QuestPDF" Version="2024.3.*" />
// License: QuestPDF Community (free for open-source / non-commercial).
//          Set QuestPDF.Settings.License = LicenseType.Community in Program.cs.

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FixMyCity.API.Services;

// ── Data contracts passed from controllers ─────────────────────────────────

public record ComplaintPdfData(
    int ComplaintId,
    string Title,
    string Description,
    string Status,
    string Criticality,
    string CategoryName,
    string LocalityName,
    string Address,
    string CitizenName,
    string CitizenEmail,
    string? DepartmentName,
    DateTime SubmittedAt,
    DateTime? ResolvedAt,
    float? PriorityScore,
    float? ResolutionProbability,
    string? PredictedResolutionDate,
    IList<TimelineEntry> Timeline
);

public record TimelineEntry(string Status, string ChangedBy, DateTime ChangedAt, string? Notes);

public record CertificatePdfData(
    string CitizenName,
    string MilestoneName,
    string Description,
    int Points,
    DateTime AwardedAt,
    int CertificateId
);

public record PwgReportPdfData(
    int ReportId,
    string OrgName,
    string ComplaintTitle,
    string WorkDescription,
    DateTime StartDate,
    DateTime? EndDate,
    decimal? FundUtilized,
    string Status
);

// ── Service ───────────────────────────────────────────────────────────────────

public interface IQuestPdfService
{
    byte[] GenerateComplaintReport(ComplaintPdfData data);
    byte[] GenerateCertificate(CertificatePdfData data);
    byte[] GeneratePwgReport(PwgReportPdfData data);
}

public class QuestPdfService : IQuestPdfService
{
    // Palette — matches FixMyCity brand colours
    private static readonly string PrimaryHex = "#1e3a5f";
    private static readonly string AccentHex = "#2d9c73";
    private static readonly string DangerHex = "#c0392b";
    private static readonly string WarningHex = "#e67e22";
    private static readonly string LightGray = "#f5f6fa";
    private static readonly string BorderGray = "#dee2e6";
    private static readonly string TextMuted = "#6c757d";

    // ── Complaint Report ──────────────────────────────────────────────────────

    public byte[] GenerateComplaintReport(ComplaintPdfData d)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeComplaintContent(c, d));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer c) =>
        c.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("FixMyCity")
                    .FontSize(20).Bold().FontColor(PrimaryHex);
                col.Item().Text("Civic Complaint Management Platform")
                    .FontSize(9).FontColor(TextMuted);
            });
            row.ConstantItem(120).AlignRight().AlignMiddle()
               .Text($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
               .FontSize(8).FontColor(TextMuted);
        });

    private void ComposeComplaintContent(IContainer c, ComplaintPdfData d)
    {
        c.Column(col =>
        {
            col.Spacing(12);

            // ── Title bar ──
            col.Item().Background(PrimaryHex).Padding(12).Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text($"Complaint #{d.ComplaintId}")
                         .FontSize(16).Bold().FontColor(Colors.White);
                    inner.Item().Text(d.Title)
                         .FontSize(11).FontColor("#cce0ff");
                });
                row.ConstantItem(110).AlignRight().AlignMiddle()
                   .Background(StatusColor(d.Status)).Padding(6).AlignCenter()
                   .Text(d.Status).Bold().FontColor(Colors.White)
                   .FontSize(10);
            });

            // ── Info grid ──
            col.Item().Border(1).BorderColor(BorderGray).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(120);
                    cols.RelativeColumn();
                    cols.ConstantColumn(120);
                    cols.RelativeColumn();
                });

                void Cell(string label, string value, bool shade = false)
                {
                    var bg = shade ? LightGray : "#ffffff";
                    table.Cell().Background(bg).PaddingVertical(6).PaddingHorizontal(8)
                         .Text(label).FontColor(TextMuted).FontSize(9).Bold();
                    table.Cell().Background(bg).PaddingVertical(6).PaddingHorizontal(8)
                         .Text(value).FontSize(10);
                }

                Cell("Category", d.CategoryName);
                Cell("Criticality", d.Criticality, shade: true);
                Cell("Locality", d.LocalityName);
                Cell("Department", d.DepartmentName ?? "Unassigned", shade: true);
                Cell("Citizen", d.CitizenName);
                Cell("Email", d.CitizenEmail, shade: true);
                Cell("Submitted", d.SubmittedAt.ToString("dd MMM yyyy HH:mm"));
                Cell("Resolved", d.ResolvedAt?.ToString("dd MMM yyyy HH:mm") ?? "—", shade: true);
            });

            // ── Address ──
            col.Item().LabelledBox("Address", d.Address, LightGray);

            // ── Description ──
            col.Item().LabelledBox("Description", d.Description, LightGray);

            // ── AI scores (if available) ──
            if (d.PriorityScore.HasValue)
            {
                col.Item().Element(c2 =>
                {
                    c2.Background("#eaf3ff").Border(1).BorderColor("#b3d1f7")
                      .Padding(10).Column(inner =>
                      {
                          inner.Item().Text("AI Analysis").Bold().FontColor(PrimaryHex).FontSize(10);
                          inner.Item().Row(row =>
                          {
                              row.RelativeItem().Text($"Priority Score: {d.PriorityScore:F1} / 100")
                                 .FontSize(10);
                              row.RelativeItem().Text($"Resolution Probability: {d.ResolutionProbability * 100:F0}%")
                                 .FontSize(10);
                              row.RelativeItem()
                                 .Text($"Predicted Resolution: {d.PredictedResolutionDate ?? "N/A"}")
                                 .FontSize(10);
                          });
                      });
                });
            }

            // ── Timeline ──
            if (d.Timeline.Any())
            {
                col.Item().Text("Status Timeline").Bold().FontColor(PrimaryHex).FontSize(11);
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(130);
                        cols.RelativeColumn();
                        cols.ConstantColumn(80);
                        cols.RelativeColumn();
                    });

                    // Header
                    foreach (var h in new[] { "Date/Time", "Status", "Changed By", "Notes" })
                        table.Cell().Background(PrimaryHex).Padding(6)
                             .Text(h).FontColor(Colors.White).Bold().FontSize(9);

                    bool shade = false;
                    foreach (var t in d.Timeline)
                    {
                        var bg = shade ? LightGray : "#ffffff";
                        table.Cell().Background(bg).Border(1).BorderColor(BorderGray)
                             .Padding(5).Text(t.ChangedAt.ToString("dd MMM yyyy HH:mm"))
                             .FontSize(9);
                        table.Cell().Background(bg).Border(1).BorderColor(BorderGray)
                             .Padding(5).Text(t.Status).FontSize(9).Bold();
                        table.Cell().Background(bg).Border(1).BorderColor(BorderGray)
                             .Padding(5).Text(t.ChangedBy).FontSize(9);
                        table.Cell().Background(bg).Border(1).BorderColor(BorderGray)
                             .Padding(5).Text(t.Notes ?? "—").FontSize(9);
                        shade = !shade;
                    }
                });
            }
        });
    }

    // ── Certificate ───────────────────────────────────────────────────────────

    public byte[] GenerateCertificate(CertificatePdfData d)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial));

                page.Content().Column(col =>
                {
                    col.Spacing(16);

                    // Outer border
                    col.Item().Border(6).BorderColor(PrimaryHex).Padding(30).Column(inner =>
                    {
                        inner.Spacing(12);

                        inner.Item().AlignCenter()
                             .Text("FixMyCity").FontSize(28).Bold().FontColor(PrimaryHex);
                        inner.Item().AlignCenter()
                             .Text("Certificate of Achievement").FontSize(16).FontColor(TextMuted);
                        inner.Item().AlignCenter()
                             .Text("This is to certify that").FontSize(12).Italic();
                        inner.Item().AlignCenter()
                             .Text(d.CitizenName).FontSize(24).Bold().FontColor(AccentHex);
                        inner.Item().AlignCenter()
                             .Text("has been awarded").FontSize(12).Italic();
                        inner.Item().AlignCenter()
                             .Text(d.MilestoneName).FontSize(18).Bold().FontColor(PrimaryHex);
                        inner.Item().AlignCenter()
                             .Text(d.Description).FontSize(11).FontColor(TextMuted);
                        inner.Item().AlignCenter()
                             .Text($"Points Awarded: {d.Points}").FontSize(13).Bold();
                        inner.Item().AlignCenter()
                             .Text($"Date: {d.AwardedAt:dd MMMM yyyy}")
                             .FontSize(11).FontColor(TextMuted);
                        inner.Item().AlignCenter()
                             .Text($"Certificate No: FMC-CERT-{d.CertificateId:D6}")
                             .FontSize(9).FontColor(TextMuted);
                    });
                });
            });
        }).GeneratePdf();
    }

    // ── PWG Report ────────────────────────────────────────────────────────────

    public byte[] GeneratePwgReport(PwgReportPdfData d)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Element(ComposeHeader);
                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Background(AccentHex).Padding(12)
                       .Text($"PWG Progress Report — #{d.ReportId}")
                       .FontSize(15).Bold().FontColor(Colors.White);

                    col.Item().Border(1).BorderColor(BorderGray).Table(table =>
                    {
                        table.ColumnsDefinition(c2 =>
                        {
                            c2.ConstantColumn(140); c2.RelativeColumn();
                        });

                        void R(string l, string v, bool s = false)
                        {
                            var bg = s ? LightGray : "#ffffff";
                            table.Cell().Background(bg).PaddingVertical(6).PaddingHorizontal(8)
                                 .Text(l).FontColor(TextMuted).FontSize(9).Bold();
                            table.Cell().Background(bg).PaddingVertical(6).PaddingHorizontal(8)
                                 .Text(v).FontSize(10);
                        }

                        R("Organisation", d.OrgName);
                        R("Complaint", d.ComplaintTitle, true);
                        R("Status", d.Status);
                        R("Start Date", d.StartDate.ToString("dd MMM yyyy"), true);
                        R("Completion Date", d.EndDate?.ToString("dd MMM yyyy") ?? "In Progress");
                        R("Fund Utilized", d.FundUtilized.HasValue
                                                  ? $"₹ {d.FundUtilized:N2}" : "Not reported", true);
                    });

                    col.Item().LabelledBox("Work Description", d.WorkDescription, LightGray);
                });
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static void ComposeFooter(IContainer c) =>
        c.BorderTop(1).BorderColor(BorderGray).PaddingTop(8).Row(row =>
        {
            row.RelativeItem()
               .Text("This is a system-generated document from FixMyCity. No signature required.")
               .FontSize(8).FontColor(TextMuted);
            row.ConstantItem(80).AlignRight()
               .Text(c2 =>
               {
                   c2.DefaultTextStyle(x => x.FontSize(8).FontColor(TextMuted));
                   c2.CurrentPageNumber();
                   c2.Span(" / ");
                   c2.TotalPages();
               });
        });

    private static string StatusColor(string status) => status switch
    {
        "Resolved" => "#27ae60",
        "In Progress" => "#2980b9",
        "Submitted" => "#8e44ad",
        "Escalated" => "#c0392b",
        "Rejected" => "#7f8c8d",
        _ => "#7f8c8d"
    };
}

// ── QuestPDF extension helper ──────────────────────────────────────────────

internal static class QuestPdfExtensions
{
    public static void LabelledBox(this IContainer c,
                                    string label, string content, string bg)
    {
        c.Column(col =>
        {
            col.Item().Text(label).Bold().FontSize(10).FontColor("#1e3a5f");
            col.Item().Background(bg).Border(1).BorderColor("#dee2e6")
               .Padding(8).Text(content).FontSize(10);
        });
    }
}
