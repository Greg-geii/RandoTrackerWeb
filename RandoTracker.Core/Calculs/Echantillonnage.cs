using RandoTracker.Core.Modele;

namespace RandoTracker.Core.Calculs;

public static class Echantillonnage
{
    /// <summary>
    /// Réduit le profil à un point tous les `pas` mètres. Premier et dernier
    /// points sont toujours conservés. À 25 m, une sortie de 3 000 points
    /// tombe à ~500 lignes sans rien perdre pour l'analyse statistique.
    /// </summary>
    public static List<PointProfil> EchantillonnerProfil(List<PointProfil> profil, double pas)
    {
        if (pas <= 0 || profil.Count < 3) return profil;

        var echantillon = new List<PointProfil> { profil[0] };
        double derniere = profil[0].DistanceCumulee;

        for (int i = 1; i < profil.Count - 1; i++)
        {
            if (profil[i].DistanceCumulee - derniere >= pas)
            {
                echantillon.Add(profil[i]);
                derniere = profil[i].DistanceCumulee;
            }
        }

        echantillon.Add(profil[^1]);
        return echantillon;
    }
}
