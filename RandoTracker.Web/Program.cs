using Microsoft.Data.Sqlite;
using RandoTracker.Core.Donnees;
using RandoTracker.Core.Geographie;
using RandoTracker.Core.Vitesse;
using RandoTracker.Web;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Chargé une seule fois au démarrage : le contour des parcs ne change pas
// en cours de route, inutile de reparser le GeoJSON à chaque requête.
List<Parc> parcsGeographiques = ParcsGeographiques.Charger(
    Path.Combine(app.Environment.WebRootPath, "data", "parcs.json"));

app.MapGet("/api/sorties", () =>
{
    using var db = new RandoDb("randos.db");
    return SortieRepository.ObtenirToutes(db.Connexion);
});

// Version enrichie de la liste, pour le fil façon Strava (croquis de tracé,
// aperçu photo) — séparée de GET /api/sorties, qui reste légère pour les
// menus déroulants (choix de sortie à l'ajout/déplacement d'une trace).
app.MapGet("/api/sorties/fil", () =>
{
    using var db = new RandoDb("randos.db");

    List<SortieAvecTotaux> sorties = SortieRepository.ObtenirToutes(db.Connexion);
    var photos = PhotoRepository.ObtenirGroupeesParSortie(db.Connexion, sorties.Select(s => s.Id));

    // .ToList() indispensable ici : sans lui, le Select() ne s'exécute que
    // plus tard, pendant la sérialisation JSON — après que `db` (using) ait
    // déjà été fermée, ce qui plante ExecuteReader avec une connexion fermée.
    var resume = sorties.Select(s => new SortieResume(
        s,
        TraceRepository.ObtenirApercuSortie(db.Connexion, s.Id),
        photos[s.Id].Take(3).Select(p => new PhotoDto(p.Id, p.NomFichier, "/" + p.CheminRelatif)).ToList())).ToList();

    return Results.Ok(resume);
});

app.MapGet("/api/sorties/{id:long}", (long id) =>
{
    using var db = new RandoDb("randos.db");

    SortieAvecTotaux? sortie = SortieRepository.ObtenirParId(db.Connexion, id);
    if (sortie is null) return Results.NotFound();

    List<PhotoDto> photos = PhotoRepository.ObtenirPourSortie(db.Connexion, id)
        .Select(p => new PhotoDto(p.Id, p.NomFichier, "/" + p.CheminRelatif)).ToList();

    return Results.Ok(new DetailSortie(sortie, SortieRepository.ObtenirTraces(db.Connexion, id), photos));
});

app.MapPut("/api/sorties/{id:long}", (long id, RenommerRequete requete) =>
{
    using var db = new RandoDb("randos.db");

    if (!SortieRepository.Existe(db.Connexion, id)) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(requete.Nom)) return Results.BadRequest("Le nom ne peut pas être vide.");

    SortieRepository.Renommer(db.Connexion, id, requete.Nom.Trim());
    return Results.NoContent();
});

app.MapPut("/api/sorties/{id:long}/tags", (long id, TagsRequete requete) =>
{
    using var db = new RandoDb("randos.db");

    if (!SortieRepository.Existe(db.Connexion, id)) return Results.NotFound();

    SortieRepository.ModifierTags(db.Connexion, id, requete.Tags);
    return Results.NoContent();
});

// La suppression d'une sortie cascade sur ses traces et leurs profils
// (FOREIGN KEY ... ON DELETE CASCADE, actif grâce au PRAGMA de RandoDb).
app.MapDelete("/api/sorties/{id:long}", (long id) =>
{
    using var db = new RandoDb("randos.db");

    if (!SortieRepository.Existe(db.Connexion, id)) return Results.NotFound();

    SortieRepository.Supprimer(db.Connexion, id);
    return Results.NoContent();
});

app.MapGet("/api/traces", () =>
{
    using var db = new RandoDb("randos.db");
    return TraceRepository.ObtenirToutes(db.Connexion);
});

app.MapGet("/api/traces/{id:long}", (long id) =>
{
    using var db = new RandoDb("randos.db");

    TraceDetail? trace = TraceRepository.ObtenirDetail(db.Connexion, id);
    if (trace is null) return Results.NotFound();

    return Results.Ok(trace);
});

app.MapGet("/api/traces/{id:long}/profil", (long id) =>
{
    using var db = new RandoDb("randos.db");

    if (!TraceRepository.Existe(db.Connexion, id)) return Results.NotFound();

    return Results.Ok(TraceRepository.ObtenirProfil(db.Connexion, id));
});

app.MapPut("/api/traces/{id:long}", (long id, RenommerRequete requete) =>
{
    using var db = new RandoDb("randos.db");

    if (!TraceRepository.Existe(db.Connexion, id)) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(requete.Nom)) return Results.BadRequest("Le nom ne peut pas être vide.");

    try
    {
        TraceRepository.Renommer(db.Connexion, id, requete.Nom.Trim());
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19) // contrainte d'unicité (Nom, Date)
    {
        return Results.Conflict("Une trace du même nom existe déjà à cette date.");
    }

    return Results.NoContent();
});

// Réaffecter change les dates des DEUX sorties concernées (l'ancienne peut
// perdre sa borne la plus ancienne/récente, la nouvelle peut en gagner une).
app.MapPut("/api/traces/{id:long}/sortie", (long id, ReassignerRequete requete) =>
{
    using var db = new RandoDb("randos.db");

    long? ancienneSortieId = TraceRepository.ObtenirSortieId(db.Connexion, id);
    if (ancienneSortieId is null) return Results.NotFound("Trace introuvable.");
    if (!SortieRepository.Existe(db.Connexion, requete.SortieId)) return Results.NotFound("Sortie introuvable.");

    TraceRepository.Reassigner(db.Connexion, id, requete.SortieId);

    SortieRepository.RecalculerDates(db.Connexion, ancienneSortieId.Value);
    SortieRepository.RecalculerDates(db.Connexion, requete.SortieId);

    return Results.NoContent();
});

// Supprimer une trace peut changer les dates de sa sortie (si elle était la
// plus ancienne ou la plus récente) : on les recalcule après coup.
app.MapDelete("/api/traces/{id:long}", (long id) =>
{
    using var db = new RandoDb("randos.db");

    long? sortieId = TraceRepository.ObtenirSortieId(db.Connexion, id);
    if (sortieId is null) return Results.NotFound();

    TraceRepository.Supprimer(db.Connexion, id);
    SortieRepository.RecalculerDates(db.Connexion, sortieId.Value);

    return Results.NoContent();
});

app.MapGet("/api/parcs", () =>
{
    using var db = new RandoDb("randos.db");
    return ParcsRepository.ObtenirStatistiques(db.Connexion, parcsGeographiques);
});

app.MapGet("/api/parcs/{nom}", (string nom) =>
{
    if (!parcsGeographiques.Any(p => p.Nom == nom)) return Results.NotFound();

    using var db = new RandoDb("randos.db");
    return Results.Ok(ParcsRepository.ObtenirSortiesDuParc(db.Connexion, parcsGeographiques, nom));
});

app.MapGet("/api/modele-vitesse", () =>
{
    using var db = new RandoDb("randos.db");
    return CalculateurVitesse.Calculer(VitesseRepository.ObtenirSegments(db.Connexion));
});

app.MapPost("/api/prediction", PredictionEndpoint.Predire).DisableAntiforgery();

// L'upload de fichier déclenche par défaut la protection anti-CSRF de .NET 8,
// qui suppose une session authentifiée (jeton, cookie...). Pas encore de
// notion d'utilisateur ici : à reprendre au moment de l'authentification.
app.MapPost("/api/traces/apercu", TracesEndpoint.Previsualiser).DisableAntiforgery();
app.MapPost("/api/traces", TracesEndpoint.Traiter).DisableAntiforgery();

app.MapPost("/api/sorties/{sortieId:long}/photos", PhotosEndpoint.Ajouter).DisableAntiforgery();
app.MapDelete("/api/photos/{id:long}", PhotosEndpoint.Supprimer);

// ── Domaine matériel : indépendant du domaine GPX ci-dessus ───────
app.MapGet("/api/materiel/categories", MaterielEndpoint.ObtenirCategories);
app.MapPost("/api/materiel/categories", MaterielEndpoint.CreerCategorie);
app.MapGet("/api/materiel/categories/{categorieId:long}/candidats", MaterielEndpoint.ObtenirCandidatsDeCategorie);
app.MapGet("/api/materiel/candidats", MaterielEndpoint.ObtenirTousLesCandidats);
app.MapGet("/api/materiel/candidats/{id:long}", MaterielEndpoint.ObtenirCandidat);
app.MapPost("/api/materiel/candidats", MaterielEndpoint.CreerCandidat);
app.MapPut("/api/materiel/candidats/{id:long}", MaterielEndpoint.ModifierCandidat);
app.MapPost("/api/materiel/candidats/{id:long}/achat", MaterielEndpoint.Acheter);
app.MapGet("/api/materiel/possessions", MaterielEndpoint.ObtenirPossessions);
app.MapPut("/api/materiel/possessions/{id:long}", MaterielEndpoint.ModifierPossession);
app.MapGet("/api/materiel/alertes", MaterielEndpoint.ObtenirAlertes);

app.MapPost("/api/materiel/candidats/{candidatId:long}/photos", MaterielPhotosEndpoint.Ajouter).DisableAntiforgery();
app.MapDelete("/api/materiel/photos/{id:long}", MaterielPhotosEndpoint.Supprimer);

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
