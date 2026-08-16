using RandoTracker.Core.Modele;

namespace RandoTracker.Core.Calculs;

/// <summary>Construction du profil altitude/pente à partir des points bruts.</summary>
public static class ProfilCalculateur
{
    public static List<PointProfil> CalculerProfil(List<Point> points, double fenetrePente)
    {
        var profil = new List<PointProfil>();
        double distanceCumulee = 0;
        double? derniereAltitude = null;

        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0)
            {
                distanceCumulee += Geo.Haversine(
                    points[i - 1].Lat, points[i - 1].Lon,
                    points[i].Lat, points[i].Lon);
            }

            // Trou dans les données : on prolonge la dernière altitude connue.
            double? altitude = points[i].Alt ?? derniereAltitude;
            if (altitude is null) continue;
            derniereAltitude = altitude;

            double pente = PenteEnPoint(points, i, fenetrePente) ?? 0;

            profil.Add(new PointProfil(distanceCumulee, altitude.Value, pente, points[i].Time,
                points[i].Lat, points[i].Lon));
        }

        return profil;
    }

    /// <summary>Pente locale au point i, sur une fenêtre centrée.</summary>
    public static double? PenteEnPoint(List<Point> points, int i, double fenetre)
    {
        if (i <= 0 || i >= points.Count - 1) return null;

        double distanceAvant = 0, distanceApres = 0;
        double? altAvant = null, altApres = null;

        for (int j = i - 1; j >= 0; j--)
        {
            distanceAvant += Geo.Haversine(
                points[j + 1].Lat, points[j + 1].Lon,
                points[j].Lat, points[j].Lon);

            if (distanceAvant >= fenetre) { altAvant = points[j].Alt; break; }
        }

        for (int j = i + 1; j < points.Count; j++)
        {
            distanceApres += Geo.Haversine(
                points[j - 1].Lat, points[j - 1].Lon,
                points[j].Lat, points[j].Lon);

            if (distanceApres >= fenetre) { altApres = points[j].Alt; break; }
        }

        if (altAvant is double av && altApres is double ap)
        {
            double deltaDist = distanceAvant + distanceApres;
            return deltaDist > 0 ? (ap - av) / deltaDist : null;
        }

        return null;
    }
}
