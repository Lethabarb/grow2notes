namespace Grow2Notes.Web.Features.Reports;

/// <summary>
/// A report file before it is drawn: the blocks of its body and the words around them. Both renderers read the same
/// document, so the Word and PDF files cannot say different things. <paramref name="Title"/> is the running header and
/// the file's title; <paramref name="OrganisationName"/> goes in the file's properties; <paramref name="Footer"/> is
/// the sentence before "Page n of N". None of the three holds a participant's name.
/// </summary>
internal sealed record ReportDocument(
    string Title,
    string OrganisationName,
    string Footer,
    IReadOnlyList<ReportBlock> Blocks);
