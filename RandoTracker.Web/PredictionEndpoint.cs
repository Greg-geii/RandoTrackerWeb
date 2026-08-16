using RandoTracker.Core.Donnees;
using RandoTracker.Core.Modele;
using RandoTracker.Core.Vitesse;

namespace RandoTracker.Web;

/// <summary>
/// Estime la durée d'une trace pas encore parcourue, à partir du modèle de
/// vitesse personnel appris sur l'historique archivé — sans rien archiver.
/// </summary>
public static class PredictionEndpoint
{
    public static async Task<IResult> Predire(IFormFile? fichier)
    {
        if (fichier is null)
            return Results.BadRequest("Aucun fichier envoyé.");

        var (analyse, erreur) = await TracesEndpoint.AnalyserFichierUploade(fichier, new ParametresAnalyse());
        if (analyse is null) return Results.BadRequest(erreur);

        using var db = new RandoDb("randos.db");

        ModeleVitessePersonnel modele = CalculateurVitesse.Calculer(VitesseRepository.ObtenirSegments(db.Connexion));
        (TimeSpan? duree, double couverture) = CalculateurVitesse.Predire(analyse.Profil, modele);

        return Results.Ok(new ReponsePrediction(
            analyse.Nom,
            analyse.DistanceTotale / 1000,
            analyse.DenivelePositif,
            analyse.DeniveleNegatif,
            duree?.TotalMinutes,
            couverture,
            modele.NombreSegments));
    }
}
