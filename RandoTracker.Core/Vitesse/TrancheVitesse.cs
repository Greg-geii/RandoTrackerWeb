namespace RandoTracker.Core.Vitesse;

/// <summary>
/// Vitesse moyenne personnelle observée sur une tranche de pente donnée.
/// VitesseMoyenneKmh est null tant qu'aucun segment archivé n'y correspond —
/// une tranche vide n'est pas une vitesse nulle, elle est inconnue.
/// </summary>
public record TrancheVitesse(double PenteMin, double PenteMax, double? VitesseMoyenneKmh, int NombreEchantillons);
