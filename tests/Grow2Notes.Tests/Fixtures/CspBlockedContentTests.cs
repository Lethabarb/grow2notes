namespace Grow2Notes.Tests.Fixtures;

/// <summary>
/// <see cref="CspBlockedContent"/> on short samples, so <c>SpaBuildTests</c>, which runs it on the build, is shown to
/// catch each thing it looks for: each sample that breaks a check is found, and what the build has in its place, such
/// as a script with a <c>src</c>, or a <c>data:</c> object key in the React bundle, passes.
/// </summary>
public sealed class CspBlockedContentTests
{
    [Theory]
    [InlineData("<script>start()</script>", "<script> with no src")]
    [InlineData("""<script type="module">import "/assets/a.js";</script>""", "<script> with no src")]
    [InlineData("""<script type="application/json">{"a":1}</script>""", "<script> with no src")]
    [InlineData("""<SCRIPT data-src="/assets/a.js"></SCRIPT>""", "<script> with no src")]
    [InlineData("<style>p { color: red }</style>", "<style> element")]
    [InlineData("""<p STYLE="color: red">Hi</p>""", "style attribute on <p>")]
    [InlineData("""<div id="root" onclick="start()"></div>""", "onclick attribute on <div>")]
    [InlineData("<img src=/assets/a.png OnError=retry()>", "onerror attribute on <img>")]
    [InlineData("<svg/onload=start()>", "onload attribute on <svg>")]
    [InlineData("""<img alt="a > b"onerror="retry()">""", "onerror attribute on <img>")]
    [InlineData("""<img src="data:image/png;base64,iVBORw0KGgo=">""", "data:image/png")]
    [InlineData("""<a href="data:,Hello">Hi</a>""", "data:,")]
    [InlineData("<a href='DATA:;base64,SGk='>Hi</a>", "DATA:;")]
    public void The_page_check_finds_a_sample_that_breaks_the_policy(string sample, string found)
    {
        Assert.Equal([found], CspBlockedContent.InPage(ViteLikePage(sample)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("""<script defer src="/assets/b.js"></script>""")]
    [InlineData("""<div data-state="open" title="style=x, onclick=y > z" class='on'></div>""")]
    public void The_page_check_passes_a_page_with_none(string sample)
    {
        Assert.Empty(CspBlockedContent.InPage(ViteLikePage(sample)));
    }

    [Theory]
    [InlineData(".icon{background:url(data:image/svg+xml;utf8,<svg/>)}", "data:image/svg+xml")]
    [InlineData("""const empty="data:,";""", "data:,")]
    [InlineData("img.src=`data:;base64,${png}`", "data:;")]
    public void The_asset_check_finds_a_data_uri(string sample, string found)
    {
        Assert.Equal([found], CspBlockedContent.DataUris(sample));
    }

    [Theory]
    [InlineData("var Oe={pending:!1,data:null,method:null,action:null};")]
    [InlineData("return{data:e,error:t}")]
    [InlineData("""el.dataset.state="open";""")]
    public void The_asset_check_passes_data_that_is_no_uri(string sample)
    {
        Assert.Empty(CspBlockedContent.DataUris(sample));
    }

    // A page shaped as Vite builds it, with its one hashed module script, and the sample at the end of its body.
    private static string ViteLikePage(string sample) => $"""
        <!doctype html>
        <html lang="en-AU">
          <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>Grow2Notes</title>
            <script type="module" crossorigin src="/assets/index-Oz0nqNiM.js"></script>
          </head>
          <body>
            <div id="root"></div>
            {sample}
          </body>
        </html>
        """;
}
