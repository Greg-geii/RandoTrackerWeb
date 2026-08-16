using RandoTracker.Core.Donnees;
using RandoTracker.Core.Gpx;
using RandoTracker.Core.Modele;

// ═════════════════════════════════════════════════════════════
//  RandoTracker — analyse de traces GPX (interface console)
//
//  Les calculs, le modèle et l'accès SQLite vivent dans
//  RandoTracker.Core ; ce projet ne fait que lire les arguments,
//  piloter l'enchaînement et afficher les résultats.
// ═════════════════════════════════════════════════════════════

// ── Analyse de la ligne de commande ──────────────────────────

var options = new Options();
var cibles = new List<string>();

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--help" or "-h":
            AfficherAide();
            return 0;

        case "--sorties":
            options.Commande = Commande.ListerSorties;
            break;

        case "--historique":
            options.Commande = Commande.ListerTraces;
            break;

        case "--sortie" when i + 1 < args.Length:
            options.SortieId = LireEntier(args[++i]);
            break;

        case "--nouvelle-sortie" when i + 1 < args.Length:
            options.NouvelleSortie = args[++i];
            break;

        case "--sans-graphes":
            options.Graphes = false;
            break;

        case "--seuil-denivele" when i + 1 < args.Length:
            options.Parametres.SeuilDenivele = LireDouble(args[++i], options.Parametres.SeuilDenivele);
            break;

        case "--seuil-vitesse" when i + 1 < args.Length:
            options.Parametres.SeuilVitesse = LireDouble(args[++i], options.Parametres.SeuilVitesse);
            break;

        case "--fenetre-pente" when i + 1 < args.Length:
            options.Parametres.FenetrePente = LireDouble(args[++i], options.Parametres.FenetrePente);
            break;

        case "--pas-profil" when i + 1 < args.Length:
            options.Parametres.PasProfil = LireDouble(args[++i], options.Parametres.PasProfil);
            break;

        default:
            if (args[i].StartsWith("--"))
            {
                Console.Error.WriteLine($"Option inconnue : {args[i]}");
                return 1;
            }
            cibles.Add(args[i]);
            break;
    }
}

// ── Base de données ──────────────────────────────────────────

using var db = new RandoDb("randos.db");

if (options.Commande == Commande.ListerSorties) { AffichageSorties.ListerSorties(db.Connexion); return 0; }
if (options.Commande == Commande.ListerTraces) { AffichageSorties.ListerTraces(db.Connexion); return 0; }

// ── Résolution des fichiers ──────────────────────────────────

if (cibles.Count == 0) cibles.Add("test.gpx");

List<string> fichiers = ResoudreFichiers(cibles);

if (fichiers.Count == 0)
{
    Console.Error.WriteLine("Aucun fichier GPX à traiter.");
    return 1;
}

// ── Analyse ──────────────────────────────────────────────────

var analyses = new List<Analyse>();
int echecs = 0;

foreach (string fichier in fichiers)
{
    var (analyse, erreur) = AnalyseurTrace.AnalyserFichier(fichier, options.Parametres);

    if (analyse is null)
    {
        Console.Error.WriteLine($"{Path.GetFileName(fichier)} : {erreur}");
        echecs++;
        continue;
    }

    analyses.Add(analyse);
}

if (analyses.Count == 0)
{
    Console.Error.WriteLine("Aucune trace exploitable.");
    return 1;
}

// ── Rattachement à une sortie ────────────────────────────────

long sortieId = ResolutionSortie.Resoudre(db.Connexion, analyses, options);

// ── Archivage ────────────────────────────────────────────────

int archivees = 0;

foreach (Analyse analyse in analyses)
{
    ResultatArchivage resultat = TraceRepository.Archiver(db.Connexion, analyse, sortieId, options.Parametres.PasProfil);

    if (resultat.DejaExistante)
    {
        Console.WriteLine($"Déjà archivée : {analyse.Nom}");
        continue;
    }

    Console.WriteLine($"Trace n°{resultat.TraceId} archivée : {analyse.Nom} "
                    + $"({resultat.PointsStockes} points de profil sur {resultat.PointsTotal})");
    archivees++;
}

SortieRepository.RecalculerDates(db.Connexion, sortieId);

// ── Restitution ──────────────────────────────────────────────

Console.WriteLine();

if (analyses.Count == 1)
{
    AffichageAnalyse.Afficher(analyses[0]);

    if (options.Graphes)
    {
        AffichageAnalyse.AfficherProfilAltimetrique(analyses[0].Profil);
        AffichageAnalyse.AfficherProfilPente(analyses[0].Profil);
    }

    AffichageAnalyse.AfficherStatsPente(analyses[0]);
}
else
{
    AffichageAnalyse.AfficherResumeLot(analyses, echecs);
}

Console.WriteLine($"{archivees} trace(s) archivée(s) dans la sortie n°{sortieId}.");
Console.WriteLine();

AffichageSorties.AfficherSortie(db.Connexion, sortieId);

return 0;


// ═════════════════════════════════════════════════════════════
//  Aide et lecture des arguments
// ═════════════════════════════════════════════════════════════

static void AfficherAide()
{
    Console.WriteLine("""
        RandoTracker — analyse de traces GPX

        Usage :
          randotracker [options] <fichier.gpx | dossier> [...]
          randotracker --sorties
          randotracker --historique

        Rattachement à une sortie :
          --sortie <id>            Rattache les traces à une sortie existante.
          --nouvelle-sortie <nom>  Crée une sortie et y rattache les traces.
          (sans option)            Propose les sorties dont les dates
                                   correspondent, ou en crée une.

        Réglages de calcul :
          --seuil-denivele <m>     Hystérésis altimétrique (défaut : 0,6 m).
                                   0,5-1 m pour du baro, 3-5 m pour du GPS seul.
          --seuil-vitesse <m/s>    En dessous, le temps compte comme pause
                                   (défaut : 0,1 m/s).
          --fenetre-pente <m>      Demi-fenêtre de mesure de la pente
                                   (défaut : 50 m, soit 100 m centrés).
          --pas-profil <m>         Échantillonnage du profil stocké
                                   (défaut : 25 m ; 0 = tous les points).

        Affichage :
          --sans-graphes           Pas de profils ASCII.
          --help                   Cette aide.

        Exemples :
          randotracker ventoux.gpx
          randotracker --nouvelle-sortie "Raid Champsaur" traces/champsaur/
          randotracker --sortie 3 jour4.gpx
          randotracker --seuil-denivele 3 trace-telephone.gpx
        """);
}

static double LireDouble(string texte, double defaut)
{
    if (double.TryParse(texte.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out double valeur))
    {
        return valeur;
    }

    Console.Error.WriteLine($"Valeur illisible : {texte} — on garde {defaut}.");
    return defaut;
}

static long? LireEntier(string texte) =>
    long.TryParse(texte, out long v) ? v : null;

/// <summary>Développe les dossiers en liste de fichiers .gpx, triés par nom.</summary>
static List<string> ResoudreFichiers(List<string> cibles)
{
    var fichiers = new List<string>();

    foreach (string cible in cibles)
    {
        if (Directory.Exists(cible))
        {
            fichiers.AddRange(Directory.GetFiles(cible, "*.gpx", SearchOption.AllDirectories));
        }
        else if (File.Exists(cible))
        {
            fichiers.Add(cible);
        }
        else
        {
            Console.Error.WriteLine($"Introuvable, ignoré : {cible}");
        }
    }

    fichiers.Sort();
    return fichiers;
}
