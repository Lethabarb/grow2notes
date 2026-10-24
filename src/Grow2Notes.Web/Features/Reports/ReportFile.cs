using System.Diagnostics.CodeAnalysis;

namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// The two file formats that a report is sent in, shared by the daily report and the participant record: the names
/// that the <c>format</c> query takes, and the render of a <see cref="ReportDocument"/> in memory.
/// </summary>
internal static class ReportFile
{
    /// <summary>The <c>format</c> of a Word file.</summary>
    public const string Docx = "docx";

    /// <summary>The <c>format</c> of a PDF file.</summary>
    public const string Pdf = "pdf";

    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const string PdfContentType = "application/pdf";

    /// <summary>Whether <paramref name="format"/> is exactly <c>docx</c> or <c>pdf</c>, so <c>PDF</c> is not.</summary>
    public static bool IsFormat([NotNullWhen(true)] string? format) => format is Docx or Pdf;

    /// <summary>
    /// The bytes of <paramref name="document"/> in <paramref name="format"/>, which <see cref="IsFormat"/> has
    /// accepted, and the file's content type.
    /// </summary>
    public static (byte[] Bytes, string ContentType) Render(ReportDocument document, string format) =>
        format == Docx
            ? (WordReport.Render(document), DocxContentType)
            : (PdfReport.Render(document), PdfContentType);
}
