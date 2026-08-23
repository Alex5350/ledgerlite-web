using System.Net.Http.Headers;

namespace LedgerLite.Web.Client.Services.Auth;

/// <summary>
/// Delegating handler that attaches the stored JWT as a "Authorization: Bearer" header
/// on every outgoing request. Registered per-scope so it shares the owning circuit's
/// <see cref="ITokenStore"/> (and therefore its JS runtime) in Interactive Server,
/// and the app-wide store in WebAssembly. No header is attached when the token is
/// missing or expired.
/// </summary>
public sealed class BearerTokenHandler(ITokenStore tokenStore) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetAsync(cancellationToken);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
