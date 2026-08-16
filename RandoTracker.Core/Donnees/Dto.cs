namespace RandoTracker.Core.Donnees;

/// <summary>Une sortie avec ses totaux, agrégés par jointure sur ses traces.</summary>
public record SortieAvecTotaux(
    long Id,
    string Nom,
    string? DateDebut,
    string? DateFin,
    int NombreTraces,
    double DistanceKm,
    double DenivelePositif,
    double DeniveleNegatif,
    double? DureeMouvementMin,
    double? VitesseAscensionnelle,
    List<string> Tags);

/// <summary>Une trace, avec l'identité de la sortie à laquelle elle est rattachée.</summary>
public record TraceAvecSortie(
    long Id,
    string Nom,
    string? Date,
    double DistanceKm,
    double DenivelePositif,
    double DeniveleNegatif,
    double? DureeMouvementMin,
    double? VitesseAscensionnelle,
    long SortieId,
    string SortieNom);

/// <summary>Une trace au sein du détail d'une sortie (colonnes réduites).</summary>
public record TraceDeSortie(
    long Id,
    string Nom,
    string? Date,
    double DistanceKm,
    double DenivelePositif,
    double DeniveleNegatif);

/// <summary>Résultat de l'archivage d'une trace analysée.</summary>
public record ResultatArchivage(bool DejaExistante, long TraceId, int PointsStockes, int PointsTotal);

/// <summary>Un point de coordonnées, pour tracer une trace sur une carte.</summary>
public record PointCarte(double Lat, double Lon);

/// <summary>
/// Un point du profil d'une trace, pour les graphiques altitude/pente. Lat/Lon/Temps
/// sont optionnels (absents sur les traces archivées avant leur ajout) : quand ils
/// sont présents, ils le sont pour tous les points de la trace, jamais partiellement.
/// </summary>
public record PointProfilDto(double DistanceCumulee, double Altitude, double Pente, double? Lat, double? Lon, string? Temps);

/// <summary>Position moyenne d'une trace (centroïde de son profil), pour la classer dans un parc.</summary>
public record TraceGeoloc(long TraceId, double DistanceKm, double LatMoyenne, double LonMoyenne);

/// <summary>Un parc et les traces qui s'y trouvent, d'après leur position moyenne.</summary>
public record ParcAvecStats(string Nom, string Type, int NombreTraces, double DistanceKm);

/// <summary>Une sortie et ses traces qui tombent dans un parc donné, agrégées pour comparer les sorties entre elles.</summary>
public record SortieDansParc(
    long SortieId,
    string SortieNom,
    int NombreTraces,
    double DistanceKm,
    double DenivelePositif,
    double DeniveleNegatif);

/// <summary>Détail complet d'une trace — tout ce qui a été calculé et archivé pour elle.</summary>
public record TraceDetail(
    long Id,
    string Nom,
    string? Date,
    long SortieId,
    string SortieNom,
    string Source,
    double DistanceKm,
    double AltitudeMin,
    double AltitudeMax,
    double DenivelePositif,
    double DeniveleNegatif,
    double PenteMaxMontee,
    double PenteMaxDescente,
    double? DureeTotaleMin,
    double? DureeMouvementMin,
    double? TempsEnMonteeMin,
    double? VitesseAscensionnelle,
    double SeuilDenivele,
    double SeuilVitesse,
    string Fichier);
