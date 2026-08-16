namespace RandoTracker.Core.Modele;

/// <summary>Un point du profil calculé : distance cumulée, altitude, pente locale, position.</summary>
public record PointProfil(double DistanceCumulee, double Altitude, double Pente, DateTime? Time, double Lat, double Lon);
