using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using RoadTripQC.Models;

namespace RoadTripQC.Services;

/// <summary>Provenance des données actuellement affichées.</summary>
public enum DataSource
{
    None,
    Demo,
    Gist,
    Cache
}

/// <summary>
/// Source unique des données du voyage pour l'UI.
/// Stratégie : Gist configuré + réseau → API GitHub (puis copie en cache localStorage) ;
/// hors-ligne ou erreur réseau → lecture silencieuse du cache ;
/// rien de configuré → données de démo locales.
/// </summary>
public class TripStore(
    HttpClient http,
    GistService gist,
    SettingsService settings,
    LocalStorageService storage,
    IJSRuntime js)
{
    private const string CacheKey = "rtqc.cache";
    private const string CacheDateKey = "rtqc.cacheDate";

    public TripData? Data { get; private set; }
    public string? LoadError { get; private set; }
    public DataSource Source { get; private set; } = DataSource.None;

    /// <summary>Date de la dernière synchro réussie avec le Gist.</summary>
    public DateTimeOffset? LastSync { get; private set; }

    /// <summary>Déclenché quand Data change (reload ou sauvegarde) — pour rafraîchir l'UI.</summary>
    public event Action? Changed;

    /// <summary>La sauvegarde nécessite : Gist + token configurés (l'état réseau est vérifié au moment de sauver).</summary>
    public bool CanEdit => settings.CanSave && Source != DataSource.Demo;

    public async Task EnsureLoadedAsync()
    {
        if (Data is null)
            await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        LoadError = null;
        await settings.EnsureLoadedAsync();

        if (!settings.IsConfigured)
        {
            await LoadDemoAsync();
            return;
        }

        if (await IsOnlineAsync())
        {
            try
            {
                var content = await gist.LoadContentAsync(settings.GistId!, settings.Token);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    Data = JsonSerializer.Deserialize<TripData>(content, GistService.JsonOpts);
                    Source = DataSource.Gist;
                    LastSync = DateTimeOffset.Now;
                    await storage.SetAsync(CacheKey, content);
                    await storage.SetAsync(CacheDateKey, LastSync.Value.ToString("O"));
                    Changed?.Invoke();
                    return;
                }
                LoadError = "Le Gist est vide — colle le roadtrip.json dedans.";
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
            }
        }

        // Hors-ligne ou échec réseau : on lit silencieusement le cache.
        if (await LoadFromCacheAsync())
        {
            Changed?.Invoke();
            return;
        }

        // Pas de cache non plus : dernier recours, la démo (avec l'erreur affichée).
        await LoadDemoAsync();
    }

    /// <summary>
    /// Sauvegarde sûre (fetch-avant-PATCH) : re-télécharge la dernière version du Gist,
    /// applique la modification dessus, puis pousse le tout. Évite d'écraser une
    /// modification faite par l'autre téléphone avec une copie locale périmée.
    /// </summary>
    /// <param name="applyChange">Modification à appliquer sur les données fraîches.
    /// Retourne false pour annuler (ex : cible introuvable).</param>
    public async Task<(bool Ok, string? Error)> SaveAsync(Func<TripData, bool> applyChange)
    {
        await settings.EnsureLoadedAsync();

        if (!settings.IsConfigured || settings.Token is null)
            return (false, "Token GitHub manquant — configure-le dans Paramètres.");

        if (!await IsOnlineAsync())
            return (false, "Hors ligne — modification impossible sans réseau.");

        try
        {
            var content = await gist.LoadContentAsync(settings.GistId!, settings.Token);
            var fresh = string.IsNullOrWhiteSpace(content)
                ? null
                : JsonSerializer.Deserialize<TripData>(content, GistService.JsonOpts);

            if (fresh is null)
                return (false, "Impossible de relire le Gist avant la sauvegarde.");

            if (!applyChange(fresh))
                return (false, "Modification impossible sur la dernière version des données.");

            fresh.Trip.LastUpdated = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(fresh, GistService.JsonOpts);

            await gist.SaveContentAsync(settings.GistId!, settings.Token, json);

            Data = fresh;
            Source = DataSource.Gist;
            LastSync = DateTimeOffset.Now;
            await storage.SetAsync(CacheKey, json);
            await storage.SetAsync(CacheDateKey, LastSync.Value.ToString("O"));
            Changed?.Invoke();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<bool> LoadFromCacheAsync()
    {
        try
        {
            var cached = await storage.GetAsync(CacheKey);
            if (string.IsNullOrWhiteSpace(cached))
                return false;

            Data = JsonSerializer.Deserialize<TripData>(cached, GistService.JsonOpts);
            Source = DataSource.Cache;

            var cachedDate = await storage.GetAsync(CacheDateKey);
            LastSync = DateTimeOffset.TryParse(cachedDate, out var d) ? d : null;
            return Data is not null;
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadDemoAsync()
    {
        try
        {
            Data = await http.GetFromJsonAsync<TripData>("sample-data/trip-demo.json", GistService.JsonOpts);
            Source = DataSource.Demo;
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            LoadError ??= ex.Message;
        }
    }

    public async Task<bool> IsOnlineAsync()
    {
        try
        {
            return await js.InvokeAsync<bool>("roadtrip.isOnline");
        }
        catch
        {
            return true; // au bénéfice du doute, on tentera le fetch
        }
    }

    /// <summary>Jours triés par date (le numéro J1, J2... est l'index + 1).</summary>
    public IReadOnlyList<TripDay> OrderedDays =>
        Data?.Days.OrderBy(d => d.Date).ToList() ?? [];

    public int DayNumber(TripDay day) =>
        OrderedDays.ToList().FindIndex(d => d.Date == day.Date) + 1;
}
