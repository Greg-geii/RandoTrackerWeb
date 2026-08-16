using RandoTracker.Core.Donnees;
using RandoTracker.Core.Materiel;

namespace RandoTracker.Web;

/// <summary>
/// Domaine matériel outdoor : comparaison de candidats avant achat, puis suivi
/// de ce qui est possédé. Indépendant du domaine GPX (RandoTracker.Core.Donnees) —
/// seule RandoDb, la connexion SQLite, est commune aux deux.
/// </summary>
public static class MaterielEndpoint
{
    private static readonly HashSet<string> StatutsValides =
        new(StringComparer.Ordinal) { "a_etudier", "retenu", "ecarte", "achete" };

    public static IResult ObtenirCategories()
    {
        using var db = new RandoDb("randos.db");
        return Results.Ok(CategorieRepository.ObtenirToutes(db.Connexion).Select(CategorieDto.DepuisCore));
    }

    public static IResult CreerCategorie(CategorieRequete requete)
    {
        if (string.IsNullOrWhiteSpace(requete.Nom))
            return Results.BadRequest("Le nom ne peut pas être vide.");

        using var db = new RandoDb("randos.db");
        long id = CategorieRepository.Creer(db.Connexion, requete.Nom.Trim(), requete.Priorite, requete.Notes);
        return Results.Ok(new { id });
    }

    /// <summary>Tous les candidats toutes catégories confondues — pour les filtres par tag/discipline transverses.</summary>
    public static IResult ObtenirTousLesCandidats()
    {
        using var db = new RandoDb("randos.db");

        List<Candidat> candidats = CandidatRepository.ObtenirTous(db.Connexion);
        var photos = MaterielPhotoRepository.ObtenirGroupeesParCandidat(db.Connexion, candidats.Select(c => c.Id));

        return Results.Ok(candidats.Select(c => CandidatDto.DepuisCore(c, photos[c.Id])));
    }

    public static IResult ObtenirCandidatsDeCategorie(long categorieId)
    {
        using var db = new RandoDb("randos.db");

        if (!CategorieRepository.Existe(db.Connexion, categorieId)) return Results.NotFound();

        List<Candidat> candidats = CandidatRepository.ObtenirParCategorie(db.Connexion, categorieId);
        var photos = MaterielPhotoRepository.ObtenirGroupeesParCandidat(db.Connexion, candidats.Select(c => c.Id));

        return Results.Ok(candidats.Select(c => CandidatDto.DepuisCore(c, photos[c.Id])));
    }

    public static IResult ObtenirCandidat(long id)
    {
        using var db = new RandoDb("randos.db");

        Candidat? candidat = CandidatRepository.ObtenirParId(db.Connexion, id);
        if (candidat is null) return Results.NotFound();

        List<MaterielPhoto> photos = MaterielPhotoRepository.ObtenirPourCandidat(db.Connexion, id);
        return Results.Ok(CandidatDto.DepuisCore(candidat, photos));
    }

    public static IResult CreerCandidat(CandidatRequete requete)
    {
        (bool valide, string? erreur) = ValiderCandidat(requete);
        if (!valide) return Results.BadRequest(erreur);

        using var db = new RandoDb("randos.db");

        if (!CategorieRepository.Existe(db.Connexion, requete.CategorieId))
            return Results.NotFound($"Catégorie n°{requete.CategorieId} inconnue.");

        long id = CandidatRepository.Creer(db.Connexion, requete.CategorieId, requete.Marque, requete.Modele.Trim(),
            requete.PrixIndicatif, requete.Url, requete.Statut, requete.Motif, requete.Specs, requete.EssayageRequis,
            requete.Tags ?? [], requete.Disciplines ?? []);

        return Results.Ok(new { id });
    }

    public static IResult ModifierCandidat(long id, CandidatRequete requete)
    {
        (bool valide, string? erreur) = ValiderCandidat(requete);
        if (!valide) return Results.BadRequest(erreur);

        using var db = new RandoDb("randos.db");

        if (!CandidatRepository.Existe(db.Connexion, id)) return Results.NotFound();
        if (!CategorieRepository.Existe(db.Connexion, requete.CategorieId))
            return Results.NotFound($"Catégorie n°{requete.CategorieId} inconnue.");

        CandidatRepository.Modifier(db.Connexion, id, requete.CategorieId, requete.Marque, requete.Modele.Trim(),
            requete.PrixIndicatif, requete.Url, requete.Statut, requete.Motif, requete.Specs, requete.EssayageRequis,
            requete.Tags ?? [], requete.Disciplines ?? []);

        return Results.NoContent();
    }

    /// <summary>Le candidat devient une possession : ligne Possessions créée, statut passé à "achete".</summary>
    public static IResult Acheter(long id, PossessionRequete requete)
    {
        using var db = new RandoDb("randos.db");

        if (!CandidatRepository.Existe(db.Connexion, id)) return Results.NotFound();

        long possessionId = PossessionRepository.Creer(db.Connexion, id, requete.DateAchat, requete.PrixPaye,
            requete.Taille, requete.Etat, requete.DateLimiteUsage, requete.NotesUsage);
        CandidatRepository.ChangerStatut(db.Connexion, id, "achete");

        return Results.Ok(new { possessionId });
    }

    public static IResult ObtenirPossessions()
    {
        using var db = new RandoDb("randos.db");

        List<PossessionAvecCandidat> possessions = PossessionRepository.ObtenirToutes(db.Connexion);
        var photos = MaterielPhotoRepository.ObtenirGroupeesParCandidat(db.Connexion, possessions.Select(p => p.CandidatId));

        return Results.Ok(possessions.Select(p => PossessionDto.DepuisCore(p, photos[p.CandidatId])));
    }

    public static IResult ModifierPossession(long id, PossessionRequete requete)
    {
        using var db = new RandoDb("randos.db");

        if (!PossessionRepository.Existe(db.Connexion, id)) return Results.NotFound();

        PossessionRepository.Modifier(db.Connexion, id, requete.DateAchat, requete.PrixPaye,
            requete.Taille, requete.Etat, requete.DateLimiteUsage, requete.NotesUsage);

        return Results.NoContent();
    }

    public static IResult ObtenirAlertes()
    {
        using var db = new RandoDb("randos.db");
        return Results.Ok(PossessionRepository.ObtenirAlertes(db.Connexion));
    }

    private static (bool Valide, string? Erreur) ValiderCandidat(CandidatRequete requete)
    {
        if (string.IsNullOrWhiteSpace(requete.Modele))
            return (false, "Le modèle ne peut pas être vide.");

        if (!StatutsValides.Contains(requete.Statut))
            return (false, "Statut invalide (attendu : a_etudier, retenu, ecarte ou achete).");

        return (true, null);
    }
}
