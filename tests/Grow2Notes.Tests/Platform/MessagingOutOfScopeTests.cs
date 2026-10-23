using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Grow2Notes.Tests.Fixtures;

namespace Grow2Notes.Tests.Platform;

/// <summary>
/// The build check for design.md's scope line on messaging (D14): the only message the app sends is the setup link's
/// email, to the address of a user a manager invites, so no SMS, push or other messaging reaches the build. The test
/// reads the repository's files as text and needs neither the app nor a database, so it fails on a pull request that
/// adds a messaging package or sends an email from somewhere new. Matching names by substring is a coarse check by
/// nature: it stops the usual ways in, and does not prove the code sends nothing else.
/// </summary>
public sealed partial class MessagingOutOfScopeTests
{
    private const string EmailPackage = "Azure.Communication.Email";

    // Words that mark a messaging package by its ID, in any case. Azure.Communication.Email is the one package that
    // matches, and the one that is allowed.
    private static readonly string[] Denylist =
    [
        "sms", "push", "mail", "smtp", "mime", "sendgrid", "twilio", "vonage", "nexmo", "plivo", "messagebird", "sinch",
        "firebase", "onesignal", "notificationhub", "pinpoint", "postmark", "apns",
    ];

    // The names of the mail libraries: the Azure package's namespace and .NET's own.
    private static readonly string[] MailLibraries = [EmailPackage, "System.Net.Mail", "SmtpClient"];

    private static readonly string[] SenderNames = ["IEmailSender", .. MailLibraries];

    // Relative to src/Grow2Notes.Web: every source file that names the email sender or a mail library. The send of the
    // setup link is the sender's one use, in the three handlers that issue a link, through SetupLinkEmail.
    private static readonly string[] SenderFiles =
    [
        "Features/Users/SetupLinkEmail.cs",
        "Features/Users/UserEdit.cs",
        "Features/Users/UserInvite.cs",
        "Features/Users/UserSetupLink.cs",
        "Platform/Email/CommunicationServicesEmailSender.cs",
        "Platform/Email/ConsoleEmailSender.cs",
        "Platform/Email/EmailNotSentException.cs",
        "Platform/Email/EmailSending.cs",
        "Platform/Email/IEmailSender.cs",
    ];

    private static readonly string[] IgnoredFolders = ["bin", "obj", "node_modules", ".git"];

    [Fact]
    public void No_package_is_a_messaging_service_and_Azure_Communication_Email_is_the_only_email_package()
    {
        var (packages, sources) = PackageIds();

        // The check finds the package that is allowed, so it reads the files that name packages.
        Assert.True(packages.Contains(EmailPackage), $"{EmailPackage} was not found. Read: {sources}");
        Assert.DoesNotContain(
            packages,
            id => id != EmailPackage && Denylist.Any(word => id.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Only_the_email_files_name_the_email_sender_or_a_mail_library()
    {
        string[] named =
        [
            .. WebSources()
                .Where(source => SenderNames.Any(name => source.Text.Contains(name, StringComparison.Ordinal)))
                .Select(source => source.Path)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(SenderFiles, named);
    }

    [Fact]
    public void The_mail_libraries_are_named_in_Platform_Email_alone()
    {
        var named = WebSources()
            .Where(source => MailLibraries.Any(name => source.Text.Contains(name, StringComparison.Ordinal)))
            .Select(source => source.Path)
            .ToList();

        Assert.NotEmpty(named);
        Assert.All(named, path => Assert.StartsWith("Platform/Email/", path, StringComparison.Ordinal));
    }

    // The three handlers take the sender and hand it to SetupLinkEmail.SendAsync, as its first argument, and do nothing
    // else with it, so each message they send is the setup link's.
    [Theory]
    [InlineData("Features/Users/UserInvite.cs")]
    [InlineData("Features/Users/UserEdit.cs")]
    [InlineData("Features/Users/UserSetupLink.cs")]
    public void A_handler_uses_the_email_sender_only_as_the_first_argument_of_SetupLinkEmail_SendAsync(string file)
    {
        var text = File.ReadAllText(Path.Combine(WebRoot(), file));

        var parameter = Assert.Single(SenderParameter().Matches(text)).Groups[1].Value;

        var sends = Regex.Count(text, $@"SetupLinkEmail\.SendAsync\(\s*{parameter}\s*,");
        Assert.True(sends > 0, $"{file} never sends the setup link.");

        // The parameter's own declaration is its one other mention.
        Assert.Equal(sends + 1, Regex.Count(text, $@"\b{parameter}\b"));
    }

    [GeneratedRegex(@"\bIEmailSender\s+(\w+)")]
    private static partial Regex SenderParameter();

    private static string WebRoot() => Path.Combine(Repository.Root(), "src", "Grow2Notes.Web");

    // The package IDs of the .NET packages, central and referenced, and of the SPA's and the end-to-end tests' npm
    // dependencies, with a line on what was read for the failure message of a check that finds nothing.
    private static (List<string> Ids, string Sources) PackageIds()
    {
        var root = Repository.Root();
        List<string> ids = [];
        List<string> read = [$"root {root}"];
        foreach (var file in new[] { Path.Combine(root, "Directory.Packages.props") }
            .Concat(Files(root, ".csproj")))
        {
            var found = PackageElements(file);
            ids.AddRange(found);
            read.Add($"{System.IO.Path.GetRelativePath(root, file)} {found.Count} ids");
        }

        foreach (var file in new[] { "src/grow2notes-spa/package.json", "tests/e2e/package.json" })
        {
            using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, file)));
            foreach (var section in new[] { "dependencies", "devDependencies" })
            {
                if (package.RootElement.TryGetProperty(section, out var dependencies))
                {
                    ids.AddRange(dependencies.EnumerateObject().Select(dependency => dependency.Name));
                }
            }
        }

        return (ids, string.Join("; ", read));
    }

    // The package IDs a props or project file names, read as XML. A pattern over the text found none on the CI runner,
    // so the files are parsed rather than matched.
    private static List<string> PackageElements(string file) =>
    [
        .. XDocument.Load(file).Descendants()
            .Where(element => element.Name.LocalName is "PackageVersion" or "PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>(),
    ];

    // Each .cs file of the web project as its path relative to the project, with / between folders, and its text.
    private static IEnumerable<(string Path, string Text)> WebSources() =>
        Files(WebRoot(), ".cs").Select(file => (
            System.IO.Path.GetRelativePath(WebRoot(), file).Replace(System.IO.Path.DirectorySeparatorChar, '/'),
            File.ReadAllText(file)));

    // The files under the folder with the extension, without those of the build output, packages and Git.
    private static IEnumerable<string> Files(string folder, string extension)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*" + extension))
        {
            if (file.EndsWith(extension, StringComparison.Ordinal))
            {
                yield return file;
            }
        }

        foreach (var child in Directory.EnumerateDirectories(folder))
        {
            if (IgnoredFolders.Contains(System.IO.Path.GetFileName(child)))
            {
                continue;
            }

            foreach (var file in Files(child, extension))
            {
                yield return file;
            }
        }
    }
}
