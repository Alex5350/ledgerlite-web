using System.Text.Json;
using Microsoft.JSInterop;

namespace LedgerLite.Web.Client.Services.Auth;

/// <summary>
/// <see cref="ITokenStore"/> backed by the browser's local storage (key "ledgerlite.auth"),
/// with an in-memory cache filled after the first successful read so that subsequent
/// accesses never round-trip through JS interop. JS interop is unavailable during static
/// prerendering and after a server circuit disconnects; those cases are detected and
/// tolerated (the cache is used, or a fresh read is attempted later).
/// </summary>
public sealed class LocalStorageTokenStore(IJSRuntime jsRuntime) : ITokenStore
{
    private const string StorageKey = "ledgerlite.auth";
    private static readonly JsonSerializerOptions StorageJsonOptions = new(JsonSerializerDefaults.Web);

    private StoredToken? _cachedToken;
    private bool _loadedFromStorage;

    public async Task<StoredToken?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!_loadedFromStorage)
        {
            var json = await TryReadFromStorageAsync(cancellationToken);
            if (json is null)
            {
                // JS interop unavailable (prerender) or nothing stored yet: retry on the
                // next call, using whatever is cached in memory meanwhile.
                return _cachedToken;
            }

            _loadedFromStorage = true;
            try
            {
                _cachedToken = JsonSerializer.Deserialize<StoredToken>(json, StorageJsonOptions);
            }
            catch (JsonException)
            {
                _cachedToken = null; // Corrupted payload: behave as signed out.
            }
        }

        if (_cachedToken is { } token && token.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _cachedToken = null;
            _loadedFromStorage = false;
            await TryRemoveFromStorageAsync(cancellationToken);
            return null;
        }

        return _cachedToken;
    }

    public async Task SetAsync(string accessToken, DateTime expiresAtUtc, string email, CancellationToken cancellationToken = default)
    {
        _cachedToken = new StoredToken(accessToken, expiresAtUtc, email);
        _loadedFromStorage = true;

        var json = JsonSerializer.Serialize(_cachedToken, StorageJsonOptions);
        await TryWriteToStorageAsync(json, cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _cachedToken = null;
        _loadedFromStorage = false;
        await TryRemoveFromStorageAsync(cancellationToken);
    }

    private async Task<string?> TryReadFromStorageAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await jsRuntime.InvokeAsync<string>("localStorage.getItem", cancellationToken, StorageKey);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            // Thrown while prerendering (no JS runtime yet) or after a circuit disconnect.
            return null;
        }
    }

    private async Task TryWriteToStorageAsync(string json, CancellationToken cancellationToken)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey, json);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            // Memory cache still holds the token; local storage is best-effort.
        }
    }

    private async Task TryRemoveFromStorageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, StorageKey);
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSDisconnectedException)
        {
            // Nothing meaningful to do without a JS runtime.
        }
    }
}
