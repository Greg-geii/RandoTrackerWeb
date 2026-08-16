using Microsoft.Data.Sqlite;
using RandoTracker.Core.Geographie;

namespace RandoTracker.Core.Donnees;

public static class ParcsRepository
{
    /// <summary>
    /// Pour chaque parc contenant au moins une trace : combien de traces,
    /// et quelle distance cumulée — la position de chaque trace est estimée
    /// par le centroïde de son profil, pas son tracé exact.
    /// </summary>
    public static List<ParcAvecStats> ObtenirStatistiques(SqliteConnection connection, List<Parc> parcs)
    {
        var parStat = new Dictionary<string, (string Type, int Nombre, double Distance)>();

        foreach (TraceGeoloc loc in ObtenirLocalisationMoyenne(connection))
        {
            Parc? parc = ParcsGeographiques.Trouver(parcs, loc.LatMoyenne, loc.LonMoyenne);
            if (parc is null) continue;

            parStat[parc.Nom] = parStat.TryGetValue(parc.Nom, out var actuel)
                ? (parc.Type, actuel.Nombre + 1, actuel.Distance + loc.DistanceKm)
                : (parc.Type, 1, loc.DistanceKm);
        }

        return parStat
            .Select(kv => new ParcAvecStats(kv.Key, kv.Value.Type, kv.Value.Nombre, kv.Value.Distance))
            .OrderByDescending(p => p.DistanceKm)
            .ToList();
    }

    /// <summary>
    /// Les sorties qui ont au moins une trace dans ce parc, avec le total de
    /// distance/dénivelés pour les seules traces concernées — pas la sortie
    /// entière, si elle a aussi des traces ailleurs.
    /// </summary>
    public static List<SortieDansParc> ObtenirSortiesDuParc(SqliteConnection connection, List<Parc> parcs, string nomParc)
    {
        return ObtenirTracesDuParc(connection, parcs, nomParc)
            .GroupBy(t => (t.SortieId, t.SortieNom))
            .Select(g => new SortieDansParc(
                g.Key.SortieId, g.Key.SortieNom, g.Count(),
                g.Sum(t => t.DistanceKm), g.Sum(t => t.DenivelePositif), g.Sum(t => t.DeniveleNegatif)))
            .OrderByDescending(s => s.DistanceKm)
            .ToList();
    }

    /// <summary>Les traces dont le centroïde tombe dans ce parc, avec leur sortie.</summary>
    private static List<TraceAvecSortie> ObtenirTracesDuParc(SqliteConnection connection, List<Parc> parcs, string nomParc)
    {
        var idsDuParc = ObtenirLocalisationMoyenne(connection)
            .Where(loc => ParcsGeographiques.Trouver(parcs, loc.LatMoyenne, loc.LonMoyenne)?.Nom == nomParc)
            .Select(loc => loc.TraceId)
            .ToHashSet();

        return TraceRepository.ObtenirToutes(connection)
            .Where(t => idsDuParc.Contains(t.Id))
            .ToList();
    }

    private static List<TraceGeoloc> ObtenirLocalisationMoyenne(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT t.Id, t.DistanceKm, AVG(p.Lat), AVG(p.Lon)
            FROM Traces t
            JOIN Profils p ON p.TraceId = t.Id
            WHERE p.Lat IS NOT NULL AND p.Lon IS NOT NULL
            GROUP BY t.Id";

        using var reader = cmd.ExecuteReader();

        var resultat = new List<TraceGeoloc>();

        while (reader.Read())
        {
            resultat.Add(new TraceGeoloc(
                reader.GetInt64(0), reader.GetDouble(1), reader.GetDouble(2), reader.GetDouble(3)));
        }

        return resultat;
    }
}
