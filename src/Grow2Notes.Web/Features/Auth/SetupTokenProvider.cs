using Grow2Notes.Web.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// Makes and checks the setup token in an invite's link (design.md §8.1, §8.4): Identity's Data Protection token,
/// which holds the user's ID, the purpose, when it was made and the user's security stamp, protected by the app's key
/// ring. It works for 7 days (<see cref="SetupTokenProviderOptions"/>), and only while the user's stamp is the one it
/// holds: setup completion, a resend and a sign-in reset each change the stamp, which is what makes a link single use.
/// </summary>
internal sealed class SetupTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<SetupTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, options, logger)
{
    /// <summary>
    /// The name the provider is registered under (§8.4), which a setup token is made and checked with.
    /// </summary>
    public const string ProviderName = "Setup";

    /// <summary>
    /// The purpose of the one token the provider makes, which a setup token is made and checked with.
    /// </summary>
    public const string Purpose = "Setup";
}
