namespace RoadTripQC.Models;

/// <summary>Racine du fichier roadtrip.json stocké dans le Gist.</summary>
public class TripData
{
    public TripInfo Trip { get; set; } = new();
    public List<ChecklistItem> Checklist { get; set; } = [];
    public List<TripPart> Parts { get; set; } = [];
    public List<TripDay> Days { get; set; } = [];
}

public class TripInfo
{
    public string Name { get; set; } = "";
    public List<string> Travelers { get; set; } = [];
    public string Timezone { get; set; } = "America/Toronto";
    public string Currency { get; set; } = "CAD";
    public DateTime LastUpdated { get; set; }
}

public class ChecklistItem
{
    public string Id { get; set; } = "";
    public string Task { get; set; } = "";
    public bool Done { get; set; }

    /// <summary>haute | moyenne | basse</summary>
    public string Priority { get; set; } = "moyenne";
    public string Url { get; set; } = "";
}

/// <summary>Grande section du voyage (Montréal / Fjord / Mauricie) pour l'affichage.</summary>
public class TripPart
{
    public int Id { get; set; }
    public string Emoji { get; set; } = "";
    public string Title { get; set; } = "";
    public DateOnly DateStart { get; set; }
    public DateOnly DateEnd { get; set; }
}

public class TripDay
{
    /// <summary>Clé d'identité du jour (unique). Le numéro J1, J2... est calculé, jamais stocké.</summary>
    public DateOnly Date { get; set; }
    public int PartId { get; set; }
    public string Title { get; set; } = "";

    /// <summary>confirmed | free</summary>
    public string Status { get; set; } = "confirmed";

    /// <summary>true = zone blanche probable (badge 📵 dans l'UI).</summary>
    public bool Offline { get; set; }
    public List<string> Notes { get; set; } = [];
    public Accommodation? Accommodation { get; set; }
    public List<Activity> Activities { get; set; } = [];
}

public class Accommodation
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string MapUrl { get; set; } = "";

    /// <summary>Heure "HH:mm" ou null.</summary>
    public string? Checkin { get; set; }
    public string? Checkout { get; set; }

    /// <summary>Ex : "2/4" (2e nuit sur 4).</summary>
    public string Night { get; set; } = "";
    public string Host { get; set; } = "";
    public string Phone { get; set; } = "";
    public string BookingRef { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class Activity
{
    public string Id { get; set; } = "";

    /// <summary>Heure "HH:mm", ou null = toute la journée (affiché en tête de jour).</summary>
    public string? Time { get; set; }

    /// <summary>transport | activite | resto | hebergement | alerte | note</summary>
    public string Type { get; set; } = "note";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public string MapUrl { get; set; } = "";
    public string WebsiteUrl { get; set; } = "";
    public string Phone { get; set; } = "";
    public string BookingRef { get; set; } = "";
    public string Price { get; set; } = "";

    /// <summary>true = contrainte horaire critique (carte rouge, toujours visible).</summary>
    public bool Alert { get; set; }

    /// <summary>confirme | suggestion | a-reserver</summary>
    public string Status { get; set; } = "confirme";
}
