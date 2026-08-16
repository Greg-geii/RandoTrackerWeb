using System.Globalization;
using Microsoft.Data.Sqlite;

namespace RandoTracker.Core.Donnees;

public static class VitesseRepository
{
    /// <summary>
    /// Les segments de marche de toutes les traces archivées qui ont un
    /// horodatage — matière première du modèle de vitesse personnel. Deux
    /// points consécutifs de traces différentes ne forment jamais un segment.
    /// </summary>
    public static List<SegmentVitesse> ObtenirSegments(SqliteConnection connection)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT TraceId, DistanceCumulee, Pente, Temps
            FROM Profils
            WHERE Temps IS NOT NULL
            ORDER BY TraceId, DistanceCumulee";

        using var reader = cmd.ExecuteReader();

        var segments = new List<SegmentVitesse>();

        long? traceIdPrecedente = null;
        double distancePrecedente = 0;
        DateTime? tempsPrecedent = null;

        while (reader.Read())
        {
            long traceId = reader.GetInt64(0);
            double distance = reader.GetDouble(1);
            double pente = reader.GetDouble(2);
            DateTime temps = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture);

            if (traceId == traceIdPrecedente && tempsPrecedent is DateTime precedent)
            {
                double distanceM = distance - distancePrecedente;
                double dureeSecondes = (temps - precedent).TotalSeconds;

                if (distanceM > 0 && dureeSecondes > 0)
                {
                    segments.Add(new SegmentVitesse(distanceM, pente, dureeSecondes));
                }
            }

            traceIdPrecedente = traceId;
            distancePrecedente = distance;
            tempsPrecedent = temps;
        }

        return segments;
    }
}
