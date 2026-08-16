using RandoTracker.Core.Modele;

enum Commande { Analyser, ListerSorties, ListerTraces }

class Options
{
    public Commande Commande { get; set; } = Commande.Analyser;

    public long? SortieId { get; set; }
    public string? NouvelleSortie { get; set; }

    public ParametresAnalyse Parametres { get; } = new();

    public bool Graphes { get; set; } = true;
}
