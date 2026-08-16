using RandoTracker.Core.Modele;

namespace RandoTracker.Core.Calculs;

public static class SegmentExtracteur
{
    /// <summary>
    /// Découpe le parcours en segments de montée (ou de descente) significatifs,
    /// filtrés par hystérésis. Source unique de vérité pour le dénivelé cumulé
    /// comme pour la répartition par tranche de pente.
    /// </summary>
    public static List<Segment> ExtraireSegments(List<PointProfil> profil, double seuil, bool montee)
    {
        var segments = new List<Segment>();
        double? altRef = null;

        foreach (PointProfil p in profil)
        {
            if (altRef is double r)
            {
                double ecart = p.Altitude - r;

                if (ecart > seuil)
                {
                    if (montee) segments.Add(new Segment(ecart, p.Pente, p.DistanceCumulee, p.Time));
                    altRef = p.Altitude;
                }
                else if (ecart < -seuil)
                {
                    if (!montee) segments.Add(new Segment(-ecart, p.Pente, p.DistanceCumulee, p.Time));
                    altRef = p.Altitude;
                }
                // entre les deux : bruit, la référence ne bouge pas
            }
            else
            {
                altRef = p.Altitude;
            }
        }

        return segments;
    }
}
