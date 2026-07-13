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
            return;

        // Pas de cache non plus : dernier recours, la démo (avec l'erreur affichée).
        await LoadDemoAsync();
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
        }
        catch (Exception ex)
        {
            LoadError ??= ex.Message;
        }
    }

    private async Task<bool> IsOnlineAsync()
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
