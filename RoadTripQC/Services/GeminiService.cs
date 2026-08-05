using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using RoadTripQC.Models;

namespace RoadTripQC.Services;

public record ChatMessage(string Text, bool IsUser, bool IsAction = false);

/// <summary>Action structurée émise par l'assistant (bloc JSON dans sa réponse).</summary>
public class AssistantAction
{
    [JsonPropertyName("action")]     public string  Action     { get; set; } = "";
    [JsonPropertyName("title")]      public string? Title      { get; set; }
    [JsonPropertyName("category")]   public string? Category   { get; set; }
    [JsonPropertyName("status")]     public string? Status     { get; set; }
    [JsonPropertyName("date")]       public string? Date       { get; set; }
    [JsonPropertyName("partId")]     public int?    PartId     { get; set; }
    [JsonPropertyName("time")]       public string? Time       { get; set; }
    [JsonPropertyName("type")]       public string? Type       { get; set; }  // add_activity
    [JsonPropertyName("location")]   public string? Location   { get; set; }
    [JsonPropertyName("notes")]      public string? Notes      { get; set; }
    [JsonPropertyName("link")]       public string? Link       { get; set; }
    [JsonPropertyName("price")]      public string? Price      { get; set; }
    [JsonPropertyName("bookingRef")] public string? BookingRef { get; set; }
}

public class GeminiService(SettingsService settings)
{
    private static readonly HttpClient _http = new();
    public const string DefaultModel = "gemini-3.1-flash-lite";
    private string ApiUrl => $"https://generativelanguage.googleapis.com/v1beta/models/{settings.GeminiModel}:generateContent";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.GeminiApiKey);

    public async Task<string> ChatAsync(string systemPrompt, IReadOnlyList<ChatMessage> history, string userMessage)
    {
        var key = settings.GeminiApiKey
            ?? throw new InvalidOperationException("Clé Gemini non configurée dans les Paramètres.");

        var contents = history
            .Select(m => (object)new { role = m.IsUser ? "user" : "model", parts = new[] { new { text = m.Text } } })
            .Append(new { role = "user", parts = new[] { new { text = userMessage } } })
            .ToArray();

        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents,
            generationConfig = new { temperature = 0.4, maxOutputTokens = 1024 }
        };

        var resp = await _http.PostAsJsonAsync($"{ApiUrl}?key={key}", body);

        if (!resp.IsSuccessStatusCode)
        {
            if ((int)resp.StatusCode == 503)
                throw new Exception("Le modèle Gemini est momentanément surchargé. Réessaie dans quelques secondes, ou change de modèle dans Paramètres.");

            var err = await resp.Content.ReadAsStringAsync();
            string? message = null;
            try { message = JsonDocument.Parse(err).RootElement.GetProperty("error").GetProperty("message").GetString(); } catch { }
            throw new Exception($"Erreur Gemini {(int)resp.StatusCode} — {message ?? err[..Math.Min(200, err.Length)]}");
        }

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("candidates")[0]
                   .GetProperty("content")
                   .GetProperty("parts")[0]
                   .GetProperty("text")
                   .GetString() ?? "";
    }

    /// <summary>Extrait l'action JSON de la réponse et retourne le texte visible (sans le bloc JSON).</summary>
    public static (string DisplayText, AssistantAction? Action) ParseResponse(string raw)
    {
        AssistantAction? action = null;

        var m = Regex.Match(raw, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        if (m.Success)
        {
            action = TryDeserialize(m.Groups[1].Value);
            raw = raw.Remove(m.Index, m.Length).Trim();
        }
        else
        {
            var m2 = Regex.Match(raw, @"\{[^{}]*""action""[^{}]*\}");
            if (m2.Success)
            {
                action = TryDeserialize(m2.Value);
                raw = raw.Remove(m2.Index, m2.Length).Trim();
            }
        }

        return (raw.Trim(), action);
    }

    private static AssistantAction? TryDeserialize(string json)
    {
        try { return JsonSerializer.Deserialize<AssistantAction>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch { return null; }
    }

    /// <summary>Construit le prompt système avec le contexte complet du voyage.</summary>
    public static string BuildSystemPrompt(TripData data, string today)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Tu es l'assistant d'un road trip. Tu réponds TOUJOURS en français, de façon concise et amicale.");
        sb.AppendLine();
        sb.AppendLine($"VOYAGE : {data.Trip.Name}");
        if (data.Trip.Travelers.Count > 0)
            sb.AppendLine($"Voyageurs : {string.Join(", ", data.Trip.Travelers)}");
        sb.AppendLine($"Devise : {data.Trip.Currency}");
        sb.AppendLine($"Aujourd'hui : {today}");
        sb.AppendLine();

        sb.AppendLine("ÉTAPES / RÉGIONS (utilise l'id dans le champ partId) :");
        foreach (var p in data.Parts.OrderBy(p => p.DateStart))
            sb.AppendLine($"  - id {p.Id} → {p.Emoji} {p.Title} ({p.DateStart:yyyy-MM-dd} → {p.DateEnd:yyyy-MM-dd})");
        if (data.Parts.Count == 0) sb.AppendLine("  (aucune étape définie)");
        sb.AppendLine();

        sb.AppendLine("PLANNING PAR JOUR (la date doit correspondre à un jour existant pour add_activity) :");
        foreach (var day in data.Days.OrderBy(d => d.Date))
        {
            sb.AppendLine($"## {day.Date:yyyy-MM-dd} — {day.Title} (partId {day.PartId})");
            if (day.Accommodation is { } acc && !string.IsNullOrWhiteSpace(acc.Name))
                sb.AppendLine($"   nuit : {acc.Name}");
            foreach (var a in day.Activities.OrderBy(a => a.Time ?? "99:99"))
            {
                var time = a.Time is not null ? $"{a.Time} " : "";
                sb.AppendLine($"   • {time}{a.Title}");
            }
        }
        sb.AppendLine();

        if (data.Reservations.Count > 0)
        {
            sb.AppendLine("RÉSERVATIONS EXISTANTES :");
            foreach (var r in data.Reservations)
                sb.AppendLine($"  - {r.Name} [{r.Category}/{r.Status}]");
            sb.AppendLine();
        }

        sb.AppendLine("INSTRUCTIONS — il existe TROIS types de création :");
        sb.AppendLine();
        sb.AppendLine("1) RÉSERVATION (hébergement, resto, activité ou transport à réserver) → action add_reservation :");
        sb.AppendLine("  ```json");
        sb.AppendLine("  {\"action\":\"add_reservation\",\"title\":\"...\",\"category\":\"hebergement|resto|activite|transport|autre\",\"status\":\"tobook|booked|paid\",\"date\":\"YYYY-MM-DD ou null\",\"partId\":null,\"location\":null,\"price\":null,\"link\":null,\"bookingRef\":null,\"notes\":null}");
        sb.AppendLine("  ```");
        sb.AppendLine("  - status : \"tobook\" (à réserver), \"booked\" (réservé), \"paid\" (payé/confirmé). Défaut : tobook.");
        sb.AppendLine("  - price : nombre en dollars canadiens (ex: 129.00) ou null.");
        sb.AppendLine();
        sb.AppendLine("2) IDÉE (un truc à faire, sans horaire précis, rattaché à une région) → action add_idea :");
        sb.AppendLine("  ```json");
        sb.AppendLine("  {\"action\":\"add_idea\",\"title\":\"...\",\"category\":\"rando|visite|resto|detente|shopping|autre\",\"partId\":null,\"location\":null,\"link\":null,\"notes\":null}");
        sb.AppendLine("  ```");
        sb.AppendLine("  - partId = l'étape/région où l'idée est pertinente (voir la liste ci-dessus), ou null.");
        sb.AppendLine();
        sb.AppendLine("3) ACTIVITÉ de planning (sur un jour précis, avec ou sans heure) → action add_activity :");
        sb.AppendLine("  ```json");
        sb.AppendLine("  {\"action\":\"add_activity\",\"date\":\"YYYY-MM-DD\",\"title\":\"...\",\"time\":\"HH:MM ou null\",\"type\":\"transport|activite|resto|hebergement|alerte|note\",\"location\":null,\"notes\":null}");
        sb.AppendLine("  ```");
        sb.AppendLine("  - La date DOIT correspondre à un jour existant listé ci-dessus.");
        sb.AppendLine();
        sb.AppendLine("RÈGLES :");
        sb.AppendLine("- Réservation à faire/suivre (où dormir, table de resto, billet…) → add_reservation.");
        sb.AppendLine("- Suggestion « à faire si on a le temps » sans jour précis → add_idea.");
        sb.AppendLine("- Événement calé sur un jour précis du planning → add_activity.");
        sb.AppendLine("- Si le titre manque : POSE UNE QUESTION avant d'émettre le JSON.");
        sb.AppendLine("- Réponds d'abord en texte naturel, PUIS ajoute le bloc JSON.");
        sb.AppendLine("- Pour LIRE le planning : réponds en texte, sans JSON.");
        sb.AppendLine("- Tu ne peux pas modifier ni supprimer des éléments existants.");

        return sb.ToString();
    }
}
