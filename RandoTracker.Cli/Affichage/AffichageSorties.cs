using Microsoft.Data.Sqlite;
using RandoTracker.Core.Donnees;

static class AffichageSorties
{
    /// <summary>Détail d'une sortie et de ses traces.</summary>
    public static void AfficherSortie(SqliteConnection connection, long sortieId)
    {
        SortieAvecTotaux? sortie = SortieRepository.ObtenirParId(connection, sortieId);
        if (sortie is null) return;

        string periode = sortie.DateDebut is null ? "dates inconnues"
            : sortie.DateFin is null || sortie.DateDebut[..10] == sortie.DateFin[..10]
                ? sortie.DateDebut[..10]
                : $"{sortie.DateDebut[..10]} → {sortie.DateFin[..10]}";

        Console.WriteLine($"=== Sortie n°{sortieId} — {sortie.Nom} ({periode}) ===");
        Console.WriteLine($"{sortie.NombreTraces} trace(s) — "
                        + $"{sortie.DistanceKm:F2} km, "
                        + $"+{sortie.DenivelePositif:F0} m, "
                        + $"-{sortie.DeniveleNegatif:F0} m"
                        + (sortie.DureeMouvementMin is double dm ? $", {FormaterMinutes(dm)} en mouvement" : "")
                        + (sortie.VitesseAscensionnelle is double va ? $", {va:F0} m/h en montée" : ""));

        foreach (TraceDeSortie t in SortieRepository.ObtenirTraces(connection, sortieId))
        {
            string nom = t.Nom.Length > 34 ? t.Nom[..31] + "..." : t.Nom;

            Console.WriteLine($"   [{t.Id,3}] {nom,-35} "
                            + $"{(t.Date is null ? "—" : t.Date[..10]),-11} "
                            + $"{t.DistanceKm,6:F2} km  "
                            + $"+{t.DenivelePositif,5:F0} m  -{t.DeniveleNegatif,5:F0} m");
        }
    }

    /// <summary>Liste des sorties avec leurs totaux.</summary>
    public static void ListerSorties(SqliteConnection connection)
    {
        List<SortieAvecTotaux> sorties = SortieRepository.ObtenirToutes(connection);

        Console.WriteLine($"{"#",-4} {"Sortie",-30} {"Période",-24} {"Tr.",3} "
                        + $"{"km",6} {"D+",6} {"D-",6} {"Mouv.",7} {"m/h",5}");
        Console.WriteLine(new string('-', 108));

        double totalKm = 0, totalDPlus = 0, totalDMoins = 0, totalMouv = 0;

        foreach (SortieAvecTotaux s in sorties)
        {
            string nom = s.Nom.Length > 29 ? s.Nom[..26] + "..." : s.Nom;

            string debut = s.DateDebut is null ? "?" : s.DateDebut[..10];
            string fin = s.DateFin is null ? "?" : s.DateFin[..10];
            string periode = debut == fin ? debut : $"{debut} → {fin}";

            string mouv = s.DureeMouvementMin is double dm ? FormaterMinutes(dm) : "—";
            string vitAsc = s.VitesseAscensionnelle is double va ? $"{va:F0}" : "—";

            Console.WriteLine($"{s.Id,-4} {nom,-30} {periode,-24} "
                            + $"{s.NombreTraces,3} {s.DistanceKm,6:F2} {s.DenivelePositif,5:F0}m {s.DeniveleNegatif,5:F0}m "
                            + $"{mouv,7} {vitAsc,5}");

            totalKm += s.DistanceKm;
            totalDPlus += s.DenivelePositif;
            totalDMoins += s.DeniveleNegatif;
            if (s.DureeMouvementMin is double t) totalMouv += t;
        }

        if (sorties.Count > 0)
        {
            Console.WriteLine(new string('-', 108));
            Console.WriteLine($"{sorties.Count} sortie(s) — {totalKm:F1} km, "
                            + $"+{totalDPlus:F0} m, -{totalDMoins:F0} m, "
                            + $"{FormaterMinutes(totalMouv)} en mouvement");
        }
        else
        {
            Console.WriteLine("Aucune sortie enregistrée.");
        }
    }

    /// <summary>Liste des traces, rattachées à leur sortie.</summary>
    public static void ListerTraces(SqliteConnection connection)
    {
        List<TraceAvecSortie> traces = TraceRepository.ObtenirToutes(connection);

        Console.WriteLine($"{"#",-4} {"Trace",-30} {"Sortie",-22} {"Date",-11} "
                        + $"{"km",6} {"D+",6} {"D-",6} {"Mouv.",7} {"m/h",5}");
        Console.WriteLine(new string('-', 108));

        double totalKm = 0, totalDPlus = 0, totalDMoins = 0, totalMouv = 0;

        foreach (TraceAvecSortie t in traces)
        {
            string nom = t.Nom.Length > 29 ? t.Nom[..26] + "..." : t.Nom;
            string sortie = t.SortieNom.Length > 21 ? t.SortieNom[..18] + "..." : t.SortieNom;
            string date = t.Date is null ? "—" : t.Date[..10];
            string mouv = t.DureeMouvementMin is double dm ? FormaterMinutes(dm) : "—";
            string vitAsc = t.VitesseAscensionnelle is double va ? $"{va:F0}" : "—";

            Console.WriteLine($"{t.Id,-4} {nom,-30} {sortie,-22} {date,-11} "
                            + $"{t.DistanceKm,6:F2} {t.DenivelePositif,5:F0}m {t.DeniveleNegatif,5:F0}m "
                            + $"{mouv,7} {vitAsc,5}");

            totalKm += t.DistanceKm;
            totalDPlus += t.DenivelePositif;
            totalDMoins += t.DeniveleNegatif;
            if (t.DureeMouvementMin is double d) totalMouv += d;
        }

        if (traces.Count > 0)
        {
            Console.WriteLine(new string('-', 108));
            Console.WriteLine($"{traces.Count} trace(s) — {totalKm:F1} km, "
                            + $"+{totalDPlus:F0} m, -{totalDMoins:F0} m, "
                            + $"{FormaterMinutes(totalMouv)} en mouvement");
        }
        else
        {
            Console.WriteLine("Aucune trace enregistrée.");
        }
    }

    static string FormaterMinutes(double minutes)
    {
        var t = TimeSpan.FromMinutes(minutes);
        return $"{(int)t.TotalHours}h{t.Minutes:00}";
    }
}
