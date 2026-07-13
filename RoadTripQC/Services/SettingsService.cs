namespace RoadTripQC.Services;

/// <summary>
/// Configuration locale de l'app (par téléphone) : ID du Gist et token GitHub.
/// Stockée uniquement dans le localStorage — ne transite jamais ailleurs.
/// </summary>
public class SettingsService(LocalStorageService storage)
{
    private const string GistIdKey = "rtqc.gistId";
    private const string TokenKey = "rtqc.token";

    private bool _loaded;

    public string? GistId { get; private set; }
    public string? Token { get; private set; }

    /// <summary>Le token n'est pas requis pour lire, seulement pour sauvegarder.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(GistId);
    public bool CanSave => IsConfigured && !string.IsNullOrWhiteSpace(Token);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        GistId = await storage.GetAsync(GistIdKey);
        Token = await storage.GetAsync(TokenKey);
        _loaded = true;
    }

    public async Task SaveAsync(string? gistId, string? token)
    {
        GistId = string.IsNullOrWhiteSpace(gistId) ? null : gistId.Trim();
        Token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();

        if (GistId is null)
            await storage.RemoveAsync(GistIdKey);
        else
            await storage.SetAsync(GistIdKey, GistId);

        if (Token is null)
            await storage.RemoveAsync(TokenKey);
        else
            await storage.SetAsync(TokenKey, Token);

        _loaded = true;
    }
}
