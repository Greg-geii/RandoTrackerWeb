namespace RandoTracker.Core.Modele;

/// <summary>Réglages numériques qui pilotent les calculs d'une analyse.</summary>
public class ParametresAnalyse
{
    public double SeuilDenivele { get; set; } = 0.6;   // m — trace baro
    public double SeuilVitesse { get; set; } = 0.1;    // m/s
    public double FenetrePente { get; set; } = 50.0;   // m de part et d'autre
    public double PasProfil { get; set; } = 25.0;      // m entre points stockés
}
