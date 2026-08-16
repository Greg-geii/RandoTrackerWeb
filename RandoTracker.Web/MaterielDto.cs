using System.Text.Json;
using RandoTracker.Core.Materiel;

namespace RandoTracker.Web;

/// <summary>Corps de POST /api/materiel/categories.</summary>
public record CategorieRequete(string Nom, int? Priorite, string? Notes);

/// <summary>Réponse pour la page liste : une catégorie, avec une photo représentative si elle en a une.</summary>
public record CategorieDto(long Id, string Nom, int? Priorite, string? Notes,
    int NombreCandidats, string StatutAvancement, string? PhotoUrl)
{
    public static CategorieDto DepuisCore(CategorieAvecCompteurs c) => new(
        c.Id, c.Nom, c.Priorite, c.Notes, c.NombreCandidats, c.StatutAvancement,
        c.PhotoCheminRelatif is null ? null : "/" + c.PhotoCheminRelatif);
}

/// <summary>Corps de POST et PUT /api/materiel/candidats — remplacement complet (sémantique PUT).</summary>
public record CandidatRequete(long CategorieId, string? Marque, string Modele, double? PrixIndicatif,
    string? Url, string Statut, string? Motif, JsonElement? Specs, bool EssayageRequis,
    List<string>? Tags, List<string>? Disciplines);

/// <summary>Un candidat prêt à afficher, avec ses photos déjà résolues en URL.</summary>
public record CandidatDto(long Id, long CategorieId, string? Marque, string Modele, double? PrixIndicatif,
    string? Url, string Statut, string? Motif, JsonElement? Specs, bool EssayageRequis,
    List<string> Tags, List<string> Disciplines, List<PhotoDto> Photos)
{
    public static CandidatDto DepuisCore(Candidat c, List<MaterielPhoto> photos) => new(
        c.Id, c.CategorieId, c.Marque, c.Modele, c.PrixIndicatif, c.Url, c.Statut, c.Motif,
        c.Specs, c.EssayageRequis, c.Tags, c.Disciplines,
        photos.Select(p => new PhotoDto(p.Id, p.NomFichier, "/" + p.CheminRelatif)).ToList());
}

/// <summary>Corps de POST .../achat et PUT /api/materiel/possessions/{id}.</summary>
public record PossessionRequete(string? DateAchat, double? PrixPaye, string? Taille,
    string? Etat, string? DateLimiteUsage, string? NotesUsage);

/// <summary>
/// Une possession pour l'inventaire, avec les tags/disciplines et les photos
/// de son candidat (une possession n'a pas ses propres photos, elle hérite
/// de celles du candidat dont elle est issue).
/// </summary>
public record PossessionDto(long Id, string? DateAchat, double? PrixPaye, string? Taille, string? Etat,
    string? DateLimiteUsage, string? NotesUsage, long CandidatId, string? Marque, string Modele,
    long CategorieId, string CategorieNom, List<string> Tags, List<string> Disciplines, List<PhotoDto> Photos)
{
    public static PossessionDto DepuisCore(PossessionAvecCandidat p, List<MaterielPhoto> photos) => new(
        p.Id, p.DateAchat, p.PrixPaye, p.Taille, p.Etat, p.DateLimiteUsage, p.NotesUsage,
        p.CandidatId, p.Marque, p.Modele, p.CategorieId, p.CategorieNom, p.Tags, p.Disciplines,
        photos.Select(ph => new PhotoDto(ph.Id, ph.NomFichier, "/" + ph.CheminRelatif)).ToList());
}
