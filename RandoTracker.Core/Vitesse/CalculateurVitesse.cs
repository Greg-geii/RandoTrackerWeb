using RandoTracker.Core.Donnees;
using RandoTracker.Core.Modele;

namespace RandoTracker.Core.Vitesse;

/// <summary>
/// Construit un modèle de vitesse personnel par tranche de pente à partir de
/// l'historique archivé, puis l'applique à un profil non parcouru pour en
/// prédire la durée — remplace les formules génériques (Naismith, Munter,
/// DIN 33466) par les performances réelles de l'utilisateur.
/// </summary>
public static class CalculateurVitesse
{
    private const double LargeurTranche = 0.10;  // 10 points de pente par tranche
    private const double PenteMin = -0.50;
    private const double PenteMax = 0.50;

    // En dessous, on considère que c'est une pause et pas de la marche — même
    // seuil que celui utilisé par défaut pour la durée en mouvement (voir
    // ParametresAnalyse.SeuilVitesse), pour rester cohérent avec le reste de
    // l'application. Sans ce filtre, une pause déjeuner sur terrain plat
    // écraserait la vitesse « à plat » du modèle.
    private const double SeuilVitesseMinimum = 0.1;

    public static ModeleVitessePersonnel Calculer(List<SegmentVitesse> segments)
    {
        List<SegmentVitesse> utiles = segments
            .Where(s => s.DistanceM / s.DureeSecondes >= SeuilVitesseMinimum)
            .ToList();

        int nombreTranches = (int)Math.Round((PenteMax - PenteMin) / LargeurTranche);
        var distance = new double[nombreTranches];
        var duree = new double[nombreTranches];
        var compte = new int[nombreTranches];

        foreach (SegmentVitesse s in utiles)
        {
            int indice = IndiceTranche(s.Pente, nombreTranches);
            distance[indice] += s.DistanceM;
            duree[indice] += s.DureeSecondes;
            compte[indice]++;
        }

        var tranches = new List<TrancheVitesse>();

        for (int i = 0; i < nombreTranches; i++)
        {
            double min = PenteMin + i * LargeurTranche;
            double? vitesse = compte[i] > 0 ? distance[i] / duree[i] * 3.6 : null;
            tranches.Add(new TrancheVitesse(min, min + LargeurTranche, vitesse, compte[i]));
        }

        double? vitesseGlobale = utiles.Count > 0
            ? utiles.Sum(s => s.DistanceM) / utiles.Sum(s => s.DureeSecondes) * 3.6
            : null;

        return new ModeleVitessePersonnel(tranches, vitesseGlobale, utiles.Count);
    }

    /// <summary>
    /// Durée estimée pour parcourir ce profil au rythme personnel habituel sur
    /// chaque tranche de pente rencontrée, et la part de la distance couverte
    /// par des tranches où l'on a déjà des données réelles — le reste retombe
    /// sur la vitesse globale de repli, moins fiable.
    /// </summary>
    public static (TimeSpan? Duree, double CouverturePourcent) Predire(List<PointProfil> profil, ModeleVitessePersonnel modele)
    {
        if (modele.VitesseGlobaleKmh is not double vitesseGlobale || profil.Count < 2)
            return (null, 0);

        double distanceCouverteM = 0;
        double distanceTotaleM = 0;
        double dureeSecondes = 0;

        for (int i = 1; i < profil.Count; i++)
        {
            double distanceM = profil[i].DistanceCumulee - profil[i - 1].DistanceCumulee;
            if (distanceM <= 0) continue;

            TrancheVitesse tranche = modele.Tranches[IndiceTranche(profil[i].Pente, modele.Tranches.Count)];
            double vitesseKmh = tranche.VitesseMoyenneKmh ?? vitesseGlobale;

            if (tranche.VitesseMoyenneKmh is not null) distanceCouverteM += distanceM;

            dureeSecondes += distanceM / (vitesseKmh / 3.6);
            distanceTotaleM += distanceM;
        }

        double couverture = distanceTotaleM > 0 ? distanceCouverteM / distanceTotaleM * 100 : 0;
        return (TimeSpan.FromSeconds(dureeSecondes), couverture);
    }

    /// <summary>Les pentes hors de [PenteMin, PenteMax) tombent dans la tranche extrême la plus proche.</summary>
    private static int IndiceTranche(double pente, int nombreTranches)
    {
        double penteClampee = Math.Clamp(pente, PenteMin, PenteMax - 1e-9);
        int indice = (int)((penteClampee - PenteMin) / LargeurTranche);
        return Math.Clamp(indice, 0, nombreTranches - 1);
    }
}
