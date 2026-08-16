using System.Xml.Linq;
using RandoTracker.Core.Modele;

namespace RandoTracker.Core.Gpx;

/// <summary>Lecture des fichiers GPX : extraction des points et détection de la source d'altitude.</summary>
public static class LecteurGpx
{
    public static readonly XNamespace Namespace = "http://www.topografix.com/GPX/1/1";

    public static List<Point> LirePoints(XDocument doc)
    {
        var points = new List<Point>();

        foreach (XElement pt in doc.Descendants(Namespace + "trkpt"))
        {
            double? lat = (double?)pt.Attribute("lat");
            double? lon = (double?)pt.Attribute("lon");
            double? alt = (double?)pt.Element(Namespace + "ele");

            DateTime? temps = null;
            if (DateTime.TryParse(pt.Element(Namespace + "time")?.Value, out DateTime parsedTime))
            {
                temps = parsedTime;
            }

            if (lat is not double la || lon is not double lo) continue;

            points.Add(new Point(la, lo, alt, temps));
        }

        return points;
    }

    /// <summary>
    /// Devine la provenance des altitudes, ce qui détermine le seuil d'hystérésis
    /// pertinent : 0,5-1 m pour du baro, 3-5 m pour du GPS seul, rien à filtrer
    /// sur une trace redessinée.
    /// </summary>
    public static string DetecterSource(XDocument doc, List<Point> points)
    {
        string createur = doc.Root?.Attribute("creator")?.Value ?? "inconnu";

        var altitudes = points.Where(p => p.Alt is not null)
                              .Select(p => p.Alt!.Value)
                              .ToList();

        if (altitudes.Count == 0) return $"{createur} — sans altitude";

        // Des altitudes toutes entières trahissent un modèle numérique de terrain
        // (trace dessinée sur carte) plutôt qu'un capteur embarqué.
        if (altitudes.All(a => Math.Abs(a - Math.Round(a)) < 1e-9))
            return $"{createur} — modèle de terrain";

        var ecarts = new List<double>();
        for (int i = 1; i < altitudes.Count; i++)
            ecarts.Add(Math.Abs(altitudes[i] - altitudes[i - 1]));

        ecarts.Sort();
        double median = ecarts.Count > 0 ? ecarts[ecarts.Count / 2] : 0;

        return median < 0.5
            ? $"{createur} — baromètre probable"
            : $"{createur} — GPS probable";
    }
}
