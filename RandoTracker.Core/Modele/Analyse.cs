namespace RandoTracker.Core.Modele;

/// <summary>Résultat complet de l'analyse d'une trace GPX.</summary>
public class Analyse
{
    public required string Fichier { get; init; }
    public required string Nom { get; init; }
    public required string Source { get; init; }
    public required List<Point> Points { get; init; }
    public required List<PointProfil> Profil { get; init; }
    public required List<Segment> Montees { get; init; }
    public required List<Segment> Descentes { get; init; }

    public DateTime? Depart { get; init; }
    public TimeSpan? DureeTotale { get; init; }
    public TimeSpan? DureeMouvement { get; init; }
    public TimeSpan? TempsEnMontee { get; init; }

    public double DistanceTotale { get; init; }
    public double DenivelePositif { get; init; }
    public double DeniveleNegatif { get; init; }
    public double SeuilDenivele { get; init; }
    public double SeuilVitesse { get; init; }

    // ── Valeurs dérivées ──

    public double AltitudeMin => Profil.Count > 0 ? Profil.Min(p => p.Altitude) : 0;
    public double AltitudeMax => Profil.Count > 0 ? Profil.Max(p => p.Altitude) : 0;

    public double DistanceAuSommet =>
        Profil.Count == 0 ? 0
        : Profil.OrderByDescending(p => p.Altitude).First().DistanceCumulee;

    public double PenteMaxMontee => Profil.Count > 0 ? Math.Max(0, Profil.Max(p => p.Pente)) : 0;
    public double PenteMaxDescente => Profil.Count > 0 ? Math.Min(0, Profil.Min(p => p.Pente)) : 0;

    public double? PausesMinutes =>
        DureeTotale is TimeSpan t && DureeMouvement is TimeSpan m ? (t - m).TotalMinutes : null;

    public double? VitesseMoyenne =>
        DureeMouvement is TimeSpan m && m.TotalHours > 0
        ? DistanceTotale / 1000 / m.TotalHours : null;

    public double? VitesseAscensionnelle =>
        TempsEnMontee is TimeSpan t && t.TotalHours > 0.01 && DenivelePositif > 0
        ? DenivelePositif / t.TotalHours : null;
}
