namespace RandoTracker.Core.Calculs;

public static class Tranches
{
    public static int IndiceTranche(double pente, double[] bornes)
    {
        double p = Math.Abs(pente);
        int index = Array.FindIndex(bornes, b => p <= b);
        return index == -1 ? bornes.Length : index;
    }
}
