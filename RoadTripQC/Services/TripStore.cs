using System.Net.Http.Json;
using RoadTripQC.Models;

namespace RoadTripQC.Services;

/// <summary>
/// Source unique des données du voyage pour l'UI.
/// Sprint 2 : charge les données de démo locales (wwwroot/sample-data).
/// Sprint 3/4 : basculera sur le Gist (via GistService) avec cache localStorage.
/// </summary>
public class TripStore(HttpClient http)
{
    public TripData? Data { get; private set; }
    public string? LoadError { get; private set; }

    public async Task EnsureLoadedAsync()
    {
        if (Data is null)
            await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        try
        {
            LoadError = null;
            Data = await http.GetFromJsonAsync<TripData>("sample-data/trip-demo.json", GistService.JsonOpts);
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
    }

    /// <summary>Jours triés par date (le numéro J1, J2... est l'index + 1).</summary>
    public IReadOnlyList<TripDay> OrderedDays =>
        Data?.Days.OrderBy(d => d.Date).ToList() ?? [];

    public int DayNumber(TripDay day) =>
        OrderedDays.ToList().FindIndex(d => d.Date == day.Date) + 1;
}
