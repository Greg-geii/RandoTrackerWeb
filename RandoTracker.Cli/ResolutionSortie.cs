using Microsoft.Data.Sqlite;
using RandoTracker.Core.Donnees;
using RandoTracker.Core.Modele;

/// <summary>Détermine la sortie à laquelle rattacher un lot de traces analysées.</summary>
static class ResolutionSortie
{
    /// <summary>
    /// Option explicite, sinon proposition des sorties dont les dates encadrent
    /// le lot, sinon création.
    /// </summary>
    public static long Resoudre(SqliteConnection connection, List<Analyse> analyses, Options options)
    {
        if (options.SortieId is long id)
        {
            if (SortieRepository.Existe(connection, id)) return id;

            Console.Error.WriteLine($"Sortie n°{id} inconnue — création d'une nouvelle sortie.");
        }

        if (options.NouvelleSortie is string nomDemande)
            return CreerEtAnnoncer(connection, nomDemande);

        // Nom par défaut : celui de la première trace.
        string nomDefaut = analyses[0].Nom;

        DateTime? debut = analyses.Where(a => a.Depart is not null).Min(a => a.Depart);
        DateTime? fin = analyses.Where(a => a.Depart is not null).Max(a => a.Depart);

        List<(long Id, string Nom, string Periode)> candidates = SortieRepository.Proches(connection, debut, fin);

        // Pas de terminal interactif (script, redirection) : on crée sans demander.
        if (Console.IsInputRedirected)
        {
            if (candidates.Count == 1) return candidates[0].Id;
            return CreerEtAnnoncer(connection, nomDefaut, debut, fin);
        }

        Console.WriteLine();
        Console.WriteLine($"{analyses.Count} trace(s) à rattacher"
                        + (debut is not null ? $" — {debut:dd/MM/yyyy}" : ""));

        if (candidates.Count > 0)
        {
            Console.WriteLine("Sorties correspondantes :");

            foreach (var c in candidates)
                Console.WriteLine($"  [{c.Id}] {c.Nom}  ({c.Periode})");
        }

        Console.Write($"Numéro de sortie, ou Entrée pour créer « {nomDefaut} » : ");
        string? reponse = Console.ReadLine()?.Trim();

        if (!string.IsNullOrEmpty(reponse) && long.TryParse(reponse, out long choix))
        {
            if (SortieRepository.Existe(connection, choix)) return choix;

            Console.Error.WriteLine($"Sortie n°{choix} inconnue — création à la place.");
        }

        return CreerEtAnnoncer(connection, nomDefaut, debut, fin);
    }

    static long CreerEtAnnoncer(SqliteConnection connection, string nom,
                                DateTime? debut = null, DateTime? fin = null)
    {
        long id = SortieRepository.Creer(connection, nom, debut, fin);
        Console.WriteLine($"Sortie n°{id} créée : {nom}");
        return id;
    }
}
