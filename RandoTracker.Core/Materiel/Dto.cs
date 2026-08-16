using System.Text.Json;

namespace RandoTracker.Core.Materiel;

public record Categorie(long Id, string Nom, int? Priorite, string? Notes);

/// <summary>
/// Une catégorie avec ses indicateurs pour la page liste. StatutAvancement
/// vaut "rien" (aucun candidat), "en_cours" (candidats mais rien retenu),
/// "tranche" (un candidat retenu, pas encore acheté) ou "achete".
/// PhotoCheminRelatif : photo d'un de ses candidats (le plus avancé), pour la
/// vignette de la tuile — absente si aucun candidat n'a de photo.
/// </summary>
public record CategorieAvecCompteurs(long Id, string Nom, int? Priorite, string? Notes,
    int NombreCandidats, string StatutAvancement, string? PhotoCheminRelatif);

/// <summary>
/// Specs : objet JSON libre, propre à la catégorie (un baudrier et un casque
/// n'ont aucun attribut commun) — voir la discussion sur ce choix face à une
/// table clé-valeur dans le commit qui l'introduit. Tags et Disciplines sont
/// deux classifications libres et indépendantes, chacune à valeurs multiples
/// (ex. Disciplines: ["escalade","alpinisme"], Tags: ["EPI","hiver"]).
/// </summary>
public record Candidat(long Id, long CategorieId, string? Marque, string Modele,
    double? PrixIndicatif, string? Url, string Statut, string? Motif,
    JsonElement? Specs, bool EssayageRequis, List<string> Tags, List<string> Disciplines);

public record Possession(long Id, long CandidatId, string? DateAchat, double? PrixPaye,
    string? Taille, string? Etat, string? DateLimiteUsage, string? NotesUsage);

/// <summary>Une possession avec l'identité de son candidat/catégorie, pour l'inventaire.</summary>
public record PossessionAvecCandidat(long Id, string? DateAchat, double? PrixPaye,
    string? Taille, string? Etat, string? DateLimiteUsage, string? NotesUsage,
    long CandidatId, string? Marque, string Modele, long CategorieId, string CategorieNom,
    List<string> Tags, List<string> Disciplines);

/// <summary>Une possession dont la durée de vie (DateLimiteUsage) est dépassée ou proche.</summary>
public record Alerte(long PossessionId, long CandidatId, string? Marque, string Modele,
    long CategorieId, string CategorieNom, string DateLimiteUsage);

/// <summary>Une photo rattachée à un candidat. CheminRelatif est relatif à wwwroot.</summary>
public record MaterielPhoto(long Id, long CandidatId, string NomFichier, string CheminRelatif);
