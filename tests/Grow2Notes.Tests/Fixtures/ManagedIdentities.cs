using System.Reflection;
using Azure.Identity;

namespace Grow2Notes.Tests.Fixtures;

/// <summary>Which identity a <see cref="ManagedIdentityCredential"/> signs in as.</summary>
internal static class ManagedIdentities
{
    /// <summary>
    /// <c>ClientId &lt;client ID&gt;</c> for a user-assigned identity given by its client ID. The credential does not
    /// say publicly which identity it signs in as. Its internal ManagedIdentityId does, in ToString, so this reads
    /// that; if an Azure.Core or Azure.Identity update moves it, the test fails rather than passing unchecked.
    /// </summary>
    public static string? IdentityOf(ManagedIdentityCredential credential)
    {
        const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;
        var client = typeof(ManagedIdentityCredential).GetProperty("Client", Internal)?.GetValue(credential);
        return client?.GetType().GetProperty("ManagedIdentityId", Internal)?.GetValue(client)?.ToString();
    }
}
