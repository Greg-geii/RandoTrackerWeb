using RandoTracker.Core.Modele;

namespace RandoTracker.Core.Calculs;

/// <summary>Distances géographiques.</summary>
public static class Geo
{
    /// <summary>Distance orthodromique entre deux points, en mètres.</summary>
    public static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000.0; // rayon terrestre en mètres

        double φ1 = lat1 * Math.PI / 180.0;
        double φ2 = lat2 * Math.PI / 180.0;
        double Δφ = (lat2 - lat1) * Math.PI / 180.0;
        double Δλ = (lon2 - lon1) * Math.PI / 180.0;

        double a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2)
                 + Math.Cos(φ1) * Math.Cos(φ2)
                 * Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c;
    }

    public static double CalculerDistanceTotale(List<Point> points)
    {
        double total = 0;

        for (int i = 1; i < points.Count; i++)
        {
            total += Haversine(
                points[i - 1].Lat, points[i - 1].Lon,
                points[i].Lat, points[i].Lon);
        }

        return total;
    }
}
