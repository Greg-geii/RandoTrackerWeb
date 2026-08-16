namespace RandoTracker.Core.Geographie;

/// <summary>
/// Un parc national ou naturel régional, réduit à son nom et aux anneaux
/// extérieurs de ses polygones — les trous éventuels (enclaves) sont ignorés,
/// sans conséquence pour une classification approximative.
/// </summary>
public record Parc(string Nom, string Type, List<(double Lon, double Lat)[]> Polygones);
