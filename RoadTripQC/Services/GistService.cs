using System.Net.Http.Headers;
using System.Text.Json;
using RoadTripQC.Models;

namespace RoadTripQC.Services;

/// <summary>
/// Lecture (et plus tard écriture) du roadtrip.json stocké dans un Gist GitHub.
/// Lecture via l'API (et non l'URL Raw) pour éviter le cache CDN qui peut
/// resservir une version périmée juste après une sauvegarde.
/// </summary>
public class GistService(HttpClient http)
{
    private const string ApiBase = "https://api.github.com/gists/";
    private const string PreferredFileName = "roadtrip.json";

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Charge le TripData depuis le Gist. Le token est optionnel en lecture
    /// (un gist secret est lisible par ID), mais recommandé pour le quota API.
    /// </summary>
    public async Task<TripData?> LoadAsync(string gistId, string? token = null)
    {
        var content = await LoadContentAsync(gistId, token);
        return content is null
            ? null
            : JsonSerializer.Deserialize<TripData>(content, JsonOpts);
    }

    /// <summary>Contenu JSON brut du Gist (utile pour le cache localStorage).</summary>
    public async Task<string?> LoadContentAsync(string gistId, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiBase + gistId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        return ExtractFileContent(doc.RootElement);
    }

    /// <summary>
    /// Prend le contenu de roadtrip.json s'il existe, sinon du premier fichier
    /// du Gist (le fichier a été créé sous le nom gistfile1.txt).
    /// </summary>
    private static string? ExtractFileContent(JsonElement root)
    {
        if (!root.TryGetProperty("files", out var files))
            return null;

        string? fallback = null;
        foreach (var file in files.EnumerateObject())
        {
            var content = file.Value.TryGetProperty("content", out var c) ? c.GetString() : null;
            if (file.Name.Equals(PreferredFileName, StringComparison.OrdinalIgnoreCase))
                return content;
            fallback ??= content;
        }
        return fallback;
    }
}
