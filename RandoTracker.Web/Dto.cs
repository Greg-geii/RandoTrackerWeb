using RandoTracker.Core.Donnees;

namespace RandoTracker.Web;

/// <summary>Réponse de GET /api/sorties/{id} : la sortie, ses traces et ses photos.</summary>
public record DetailSortie(SortieAvecTotaux Sortie, List<TraceDeSortie> Traces, List<PhotoDto> Photos);

/// <summary>
/// Une sortie pour le fil (GET /api/sorties) : ses totaux, plus de quoi
/// dessiner une carte sans requête supplémentaire — croquis de tracé
/// sous-échantillonné et un aperçu des photos (pas forcément toutes).
/// </summary>
public record SortieResume(SortieAvecTotaux Sortie, List<PointCarte> Apercu, List<PhotoDto> Photos);

/// <summary>Une photo rattachée à une sortie, prête à être affichée (Url relative à la racine du site).</summary>
public record PhotoDto(long Id, string NomFichier, string Url);

/// <summary>Corps de PUT /api/sorties/{id} et PUT /api/traces/{id}.</summary>
public record RenommerRequete(string Nom);

/// <summary>Corps de PUT /api/sorties/{id}/tags.</summary>
public record TagsRequete(List<string> Tags);

/// <summary>Corps de PUT /api/traces/{id}/sortie.</summary>
public record ReassignerRequete(long SortieId);

/// <summary>Réponse de POST /api/prediction : la trace planifiée, avec sa durée estimée.</summary>
public record ReponsePrediction(
    string Nom,
    double DistanceKm,
    double DenivelePositif,
    double DeniveleNegatif,
    double? DureeEstimeeMin,
    double CouverturePourcent,
    int NombreSegmentsModele);
