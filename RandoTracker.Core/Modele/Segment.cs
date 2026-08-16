namespace RandoTracker.Core.Modele;

/// <summary>Une montée ou une descente significative, au sens de l'hystérésis.</summary>
public record Segment(double Denivele, double Pente, double DistanceCumulee, DateTime? Time);
