using RandoTracker.Core.Donnees;
using RandoTracker.Core.Materiel;

namespace RandoTracker.Web;

/// <summary>
/// Photos de candidats matériel — dossier séparé de RandoTracker.Web/PhotosEndpoint
/// (photos de sortie) pour garder les deux domaines indépendants jusque sur disque.
/// </summary>
public static class MaterielPhotosEndpoint
{
    private static readonly HashSet<string> ExtensionsAutorisees =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    public static async Task<IResult> Ajouter(long candidatId, IFormFileCollection fichiers, IWebHostEnvironment environnement)
    {
        if (fichiers.Count == 0)
            return Results.BadRequest("Aucun fichier envoyé.");

        using var db = new RandoDb("randos.db");

        if (!CandidatRepository.Existe(db.Connexion, candidatId))
            return Results.NotFound($"Candidat n°{candidatId} inconnu.");

        string dossierCandidat = Path.Combine(environnement.WebRootPath, "materiel-photos", candidatId.ToString());
        Directory.CreateDirectory(dossierCandidat);

        var resultats = new List<PhotoDto>();

        foreach (IFormFile fichier in fichiers)
        {
            string extension = Path.GetExtension(fichier.FileName);
            if (!ExtensionsAutorisees.Contains(extension)) continue;

            string nomStocke = $"{Guid.NewGuid()}{extension.ToLowerInvariant()}";
            string cheminRelatif = $"materiel-photos/{candidatId}/{nomStocke}";

            await using (var flux = File.Create(Path.Combine(dossierCandidat, nomStocke)))
            {
                await fichier.CopyToAsync(flux);
            }

            long id = MaterielPhotoRepository.Ajouter(db.Connexion, candidatId, fichier.FileName, cheminRelatif);
            resultats.Add(new PhotoDto(id, fichier.FileName, "/" + cheminRelatif));
        }

        return Results.Ok(resultats);
    }

    public static IResult Supprimer(long id, IWebHostEnvironment environnement)
    {
        using var db = new RandoDb("randos.db");

        string? cheminRelatif = MaterielPhotoRepository.ObtenirChemin(db.Connexion, id);
        if (cheminRelatif is null) return Results.NotFound();

        MaterielPhotoRepository.Supprimer(db.Connexion, id);

        string cheminDisque = Path.Combine(environnement.WebRootPath, cheminRelatif);
        if (File.Exists(cheminDisque)) File.Delete(cheminDisque);

        return Results.NoContent();
    }
}
