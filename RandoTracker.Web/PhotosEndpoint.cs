using RandoTracker.Core.Donnees;

namespace RandoTracker.Web;

/// <summary>Photos souvenirs rattachées à une sortie — aucun lien avec l'analyse GPX.</summary>
public static class PhotosEndpoint
{
    private static readonly HashSet<string> ExtensionsAutorisees =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    public static async Task<IResult> Ajouter(long sortieId, IFormFileCollection fichiers, IWebHostEnvironment environnement)
    {
        if (fichiers.Count == 0)
            return Results.BadRequest("Aucun fichier envoyé.");

        using var db = new RandoDb("randos.db");

        if (!SortieRepository.Existe(db.Connexion, sortieId))
            return Results.NotFound($"Sortie n°{sortieId} inconnue.");

        string dossierSortie = Path.Combine(environnement.WebRootPath, "photos", sortieId.ToString());
        Directory.CreateDirectory(dossierSortie);

        var resultats = new List<PhotoDto>();

        foreach (IFormFile fichier in fichiers)
        {
            string extension = Path.GetExtension(fichier.FileName);
            if (!ExtensionsAutorisees.Contains(extension)) continue;

            // Nom généré : le nom d'origine est gardé en base pour l'affichage,
            // mais ne sert jamais de nom de fichier sur disque (espaces, accents,
            // collisions entre deux sorties différentes).
            string nomStocke = $"{Guid.NewGuid()}{extension.ToLowerInvariant()}";
            string cheminRelatif = $"photos/{sortieId}/{nomStocke}";

            await using (var flux = File.Create(Path.Combine(dossierSortie, nomStocke)))
            {
                await fichier.CopyToAsync(flux);
            }

            long id = PhotoRepository.Ajouter(db.Connexion, sortieId, fichier.FileName, cheminRelatif);
            resultats.Add(new PhotoDto(id, fichier.FileName, "/" + cheminRelatif));
        }

        return Results.Ok(resultats);
    }

    public static IResult Supprimer(long id, IWebHostEnvironment environnement)
    {
        using var db = new RandoDb("randos.db");

        string? cheminRelatif = PhotoRepository.ObtenirChemin(db.Connexion, id);
        if (cheminRelatif is null) return Results.NotFound();

        PhotoRepository.Supprimer(db.Connexion, id);

        string cheminDisque = Path.Combine(environnement.WebRootPath, cheminRelatif);
        if (File.Exists(cheminDisque)) File.Delete(cheminDisque);

        return Results.NoContent();
    }
}
