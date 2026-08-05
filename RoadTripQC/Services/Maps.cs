namespace RoadTripQC.Services;

/// <summary>
/// Construction de liens Google Maps (universels api=1).
/// - <see cref="Search"/> : afficher un lieu.
/// - <see cref="Directions"/> / <see cref="Route"/> : navigation GPS (l'origine
///   est laissée vide → Google Maps utilise la position actuelle du téléphone).
/// </summary>
public static class Maps
{
    /// <summary>Afficher un lieu sur la carte.</summary>
    public static string Search(string query) =>
        $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(query)}";

    /// <summary>Navigation GPS en voiture vers une destination.</summary>
    public static string Directions(string destination) =>
        $"https://www.google.com/maps/dir/?api=1&destination={Uri.EscapeDataString(destination)}&travelmode=driving";

    /// <summary>
    /// Itinéraire multi-étapes : dernière étape = destination, les précédentes
    /// deviennent des waypoints (max 9, limite de l'API de liens Google Maps).
    /// </summary>
    public static string Route(IEnumerable<string> stops)
    {
        var list = stops
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct()
            .ToList();

        if (list.Count == 0) return "";

        var destination = list[^1];
        var waypoints = list.Take(list.Count - 1).Take(9).ToList();

        var url = $"https://www.google.com/maps/dir/?api=1&destination={Uri.EscapeDataString(destination)}&travelmode=driving";
        if (waypoints.Count > 0)
            url += "&waypoints=" + string.Join("|", waypoints.Select(Uri.EscapeDataString));

        return url;
    }
}
