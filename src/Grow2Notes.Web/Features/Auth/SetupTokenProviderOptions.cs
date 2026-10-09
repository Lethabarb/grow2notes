using Microsoft.AspNetCore.Identity;

namespace Grow2Notes.Web.Features.Auth;

/// <summary>
/// <see cref="SetupTokenProvider"/>'s own options, apart from those of Identity's default Data Protection token
/// provider, whose tokens last a day: a setup link works for 7 days (A23).
/// </summary>
internal sealed class SetupTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public SetupTokenProviderOptions()
    {
        // The name is also the purpose of the provider's Data Protection protector, which differs from the default
        // provider's, so neither provider can read the other's tokens.
        Name = SetupTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromDays(7);
    }
}
