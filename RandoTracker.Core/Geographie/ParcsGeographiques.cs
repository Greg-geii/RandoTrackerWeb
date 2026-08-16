using System.Text.Json;

namespace RandoTracker.Core.Geographie;

/// <summary>
/// Chargement des contours de parcs (GeoJSON, simplifié depuis la couche IGN
/// BDCARTO « parc_ou_reserve ») et classification d'un point dans un parc.
/// </summary>
public static class ParcsGeographiques
{
    public static List<Parc> Charger(string cheminGeoJson)
    {
        using var flux = File.OpenRead(cheminGeoJson);
        using var doc = JsonDocument.Parse(flux);

        var parcs = new List<Parc>();

        foreach (JsonElement feature in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            JsonElement proprietes = feature.GetProperty("properties");
            string nom = proprietes.GetProperty("toponyme").GetString() ?? "?";
            string type = proprietes.GetProperty("nature").GetString() ?? "?";

            JsonElement geometrie = feature.GetProperty("geometry");
            string typeGeometrie = geometrie.GetProperty("type").GetString()!;
            JsonElement coordonnees = geometrie.GetProperty("coordinates");

            // Un MultiPolygon est un tableau de polygones ; un Polygon en est un seul —
            // on uniformise pour traiter les deux cas de la même façon.
            IEnumerable<JsonElement> polygonesJson = typeGeometrie == "MultiPolygon"
                ? coordonnees.EnumerateArray()
                : new[] { coordonnees };

            var polygones = new List<(double Lon, double Lat)[]>();

            foreach (JsonElement polygone in polygonesJson)
            {
                // Premier anneau = contour extérieur ; les anneaux suivants
                // (le cas échéant) sont des trous, ignorés ici.
                JsonElement anneauExterieur = polygone.EnumerateArray().First();

                (double Lon, double Lat)[] points = anneauExterieur.EnumerateArray()
                    .Select(point =>
                    {
                        var coords = point.EnumerateArray().ToArray();
                        return (coords[0].GetDouble(), coords[1].GetDouble());
                    })
                    .ToArray();

                polygones.Add(points);
            }

            parcs.Add(new Parc(nom, type, polygones));
        }

        return parcs;
    }

    /// <summary>Le premier parc dont un polygone contient le point donné, ou null.</summary>
    public static Parc? Trouver(List<Parc> parcs, double lat, double lon) =>
        parcs.FirstOrDefault(parc => parc.Polygones.Any(polygone => DansPolygone(polygone, lon, lat)));

    /// <summary>Test d'appartenance par lancer de rayon (ray casting) sur un anneau simple.</summary>
    private static bool DansPolygone((double Lon, double Lat)[] anneau, double x, double y)
    {
        bool dedans = false;
        int j = anneau.Length - 1;

        for (int i = 0; i < anneau.Length; i++)
        {
            (double xi, double yi) = anneau[i];
            (double xj, double yj) = anneau[j];

            if (yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
            {
                dedans = !dedans;
            }

            j = i;
        }

        return dedans;
    }
}
