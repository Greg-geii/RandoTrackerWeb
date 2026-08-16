using RandoTracker.Core.Modele;

namespace RandoTracker.Core.Calculs;

public static class DureeCalculateur
{
    public static TimeSpan? CalculerDureeEnMouvement(List<Point> points, double seuilVitesse)
    {
        if (!points.Any(p => p.Time is not null)) return null;

        double secondes = 0;

        for (int i = 1; i < points.Count; i++)
        {
            if (points[i - 1].Time is not DateTime t0 || points[i].Time is not DateTime t1) continue;

            double dt = (t1 - t0).TotalSeconds;
            if (dt <= 0) continue;

            double distance = Geo.Haversine(
                points[i - 1].Lat, points[i - 1].Lon,
                points[i].Lat, points[i].Lon);

            if (distance / dt >= seuilVitesse) secondes += dt;
        }

        return TimeSpan.FromSeconds(secondes);
    }

    /// <summary>Temps passé à monter, au sens de l'hystérésis.</summary>
    public static TimeSpan? CalculerTempsEnMontee(List<PointProfil> profil, double seuil)
    {
        if (!profil.Any(p => p.Time is not null)) return null;

        double secondes = 0;
        double? altRef = null;
        DateTime? debutSegment = null;

        foreach (PointProfil p in profil)
        {
            if (p.Time is not DateTime t) continue;

            if (altRef is double r)
            {
                double ecart = p.Altitude - r;

                if (ecart > seuil)
                {
                    if (debutSegment is DateTime d) secondes += (t - d).TotalSeconds;
                    altRef = p.Altitude;
                    debutSegment = t;
                }
                else if (ecart < -seuil)
                {
                    altRef = p.Altitude;
                    debutSegment = t;   // la montée est interrompue
                }
            }
            else
            {
                altRef = p.Altitude;
                debutSegment = t;
            }
        }

        return TimeSpan.FromSeconds(secondes);
    }
}
