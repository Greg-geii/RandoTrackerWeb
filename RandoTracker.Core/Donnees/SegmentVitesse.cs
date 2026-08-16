namespace RandoTracker.Core.Donnees;

/// <summary>
/// Le trajet entre deux points consécutifs d'un même profil archivé : distance
/// parcourue, pente moyenne du segment, durée réelle — matière première du
/// modèle de vitesse personnel.
/// </summary>
public record SegmentVitesse(double DistanceM, double Pente, double DureeSecondes);
