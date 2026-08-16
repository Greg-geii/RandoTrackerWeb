using Microsoft.AspNetCore.Mvc;
using RandoTracker.Core.Calculs;
using RandoTracker.Core.Donnees;
using RandoTracker.Core.Gpx;
using RandoTracker.Core.Modele;

namespace RandoTracker.Web;

/// <summary>Résultat de l'analyse d'un fichier envoyé, pour un fichier donné.</summary>
public record ResultatFichier(
    string NomFichier,
    string Statut,   // "archivee" | "deja_archivee" | "erreur"
    string? Erreur,
    long? TraceId,
    int? PointsStockes,
    int? PointsTotal);

/// <summary>Réponse de POST /api/traces.</summary>
public record ReponseUpload(long SortieId, bool SortieCreee, List<ResultatFichier> Resultats);

/// <summary>Aperçu d'un fichier envoyé, avant tout archivage — de quoi reconnaître une trace mal nommée.</summary>
public record ApercuTrace(
    string NomFichier,
    string? Erreur,
    string? Nom,
    string? Date,
    double? DistanceKm,
    double? DenivelePositif,
    double? DeniveleNegatif,
    List<PointCarte>? Points);

/// <summary>Analyse et archive un ou plusieurs GPX envoyés depuis le navigateur.</summary>
public static class TracesEndpoint
{
    /// <summary>Analyse chaque fichier sans rien archiver — pour un aperçu avant de choisir la sortie.</summary>
    public static async Task<IResult> Previsualiser(
        IFormFileCollection fichiers,
        [FromForm] double? seuilDenivele,
        [FromForm] double? seuilVitesse,
        [FromForm] double? fenetrePente,
        [FromForm] double? pasProfil)
    {
        if (fichiers.Count == 0)
            return Results.BadRequest("Aucun fichier envoyé.");

        var (parametres, erreurParametres) = ConstruireParametres(seuilDenivele, seuilVitesse, fenetrePente, pasProfil);
        if (parametres is null) return Results.BadRequest(erreurParametres);

        var apercus = new List<ApercuTrace>();

        foreach (IFormFile fichier in fichiers)
        {
            var (analyse, erreur) = await AnalyserFichierUploade(fichier, parametres);

            apercus.Add(analyse is null
                ? new ApercuTrace(fichier.FileName, erreur, null, null, null, null, null, null)
                : new ApercuTrace(
                    fichier.FileName, null, analyse.Nom,
                    analyse.Depart?.ToString("yyyy-MM-dd HH:mm:ss"),
                    analyse.DistanceTotale / 1000, analyse.DenivelePositif, analyse.DeniveleNegatif,
                    Echantillonnage.EchantillonnerProfil(analyse.Profil, parametres.PasProfil)
                        .Select(p => new PointCarte(p.Lat, p.Lon)).ToList()));
        }

        return Results.Ok(apercus);
    }

    public static async Task<IResult> Traiter(
        IFormFileCollection fichiers,
        [FromForm] long? sortieId,
        [FromForm] string? nouvelleSortie,
        [FromForm] double? seuilDenivele,
        [FromForm] double? seuilVitesse,
        [FromForm] double? fenetrePente,
        [FromForm] double? pasProfil)
    {
        if (fichiers.Count == 0)
            return Results.BadRequest("Aucun fichier envoyé.");

        // XOR : il faut choisir l'un des deux, pas les deux, pas aucun — pas de
        // terminal interactif ici pour proposer des sorties candidates comme le
        // fait la console.
        if (sortieId is null == (nouvelleSortie is null))
            return Results.BadRequest("Précisez soit sortieId, soit nouvelleSortie (un seul des deux).");

        var (parametres, erreurParametres) = ConstruireParametres(seuilDenivele, seuilVitesse, fenetrePente, pasProfil);
        if (parametres is null) return Results.BadRequest(erreurParametres);

        using var db = new RandoDb("randos.db");

        bool sortieCreee = false;
        long id;

        if (nouvelleSortie is string nom)
        {
            id = SortieRepository.Creer(db.Connexion, nom);
            sortieCreee = true;
        }
        else
        {
            id = sortieId!.Value;
            if (!SortieRepository.Existe(db.Connexion, id))
                return Results.NotFound($"Sortie n°{id} inconnue.");
        }

        var resultats = new List<ResultatFichier>();

        foreach (IFormFile fichier in fichiers)
        {
            var (analyse, erreur) = await AnalyserFichierUploade(fichier, parametres);

            if (analyse is null)
            {
                resultats.Add(new ResultatFichier(fichier.FileName, "erreur", erreur, null, null, null));
                continue;
            }

            ResultatArchivage archivage = TraceRepository.Archiver(db.Connexion, analyse, id, parametres.PasProfil);

            resultats.Add(archivage.DejaExistante
                ? new ResultatFichier(fichier.FileName, "deja_archivee", null, null, null, null)
                : new ResultatFichier(fichier.FileName, "archivee", null,
                    archivage.TraceId, archivage.PointsStockes, archivage.PointsTotal));
        }

        SortieRepository.RecalculerDates(db.Connexion, id);

        return Results.Ok(new ReponseUpload(id, sortieCreee, resultats));
    }

    /// <summary>
    /// Construit les paramètres d'analyse à partir des réglages envoyés depuis le
    /// formulaire, en gardant les valeurs par défaut pour ceux non précisés.
    /// </summary>
    private static (ParametresAnalyse? Parametres, string? Erreur) ConstruireParametres(
        double? seuilDenivele, double? seuilVitesse, double? fenetrePente, double? pasProfil)
    {
        var parametres = new ParametresAnalyse();

        if (seuilDenivele is double sd)
        {
            if (sd < 0) return (null, "Le seuil de dénivelé ne peut pas être négatif.");
            parametres.SeuilDenivele = sd;
        }

        if (seuilVitesse is double sv)
        {
            if (sv < 0) return (null, "Le seuil de vitesse ne peut pas être négatif.");
            parametres.SeuilVitesse = sv;
        }

        if (fenetrePente is double fp)
        {
            if (fp < 0) return (null, "La fenêtre de pente ne peut pas être négative.");
            parametres.FenetrePente = fp;
        }

        if (pasProfil is double pp)
        {
            if (pp < 0) return (null, "Le pas du profil ne peut pas être négatif.");
            parametres.PasProfil = pp;
        }

        return (parametres, null);
    }

    /// <summary>
    /// Enregistre un fichier envoyé dans un fichier temporaire, l'analyse, puis le
    /// supprime. Interne (pas privée) : PredictionEndpoint la réutilise aussi.
    /// </summary>
    internal static async Task<(Analyse? Analyse, string? Erreur)> AnalyserFichierUploade(
        IFormFile fichier, ParametresAnalyse parametres)
    {
        string cheminTemp = Path.GetTempFileName();

        try
        {
            await using (var flux = File.Create(cheminTemp))
            {
                await fichier.CopyToAsync(flux);
            }

            return AnalyseurTrace.AnalyserFichier(cheminTemp, parametres, fichier.FileName);
        }
        finally
        {
            File.Delete(cheminTemp);
        }
    }
}
