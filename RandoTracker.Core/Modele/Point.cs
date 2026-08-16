namespace RandoTracker.Core.Modele;

/// <summary>Un point brut du GPX, avant tout calcul.</summary>
public record Point(double Lat, double Lon, double? Alt, DateTime? Time);
