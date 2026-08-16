using RandoTracker.Core.Calculs;
using RandoTracker.Core.Modele;

static class AffichageAnalyse
{
    public static void Afficher(Analyse a)
    {
        string Duree(TimeSpan? t) => t is TimeSpan d
            ? $"{(int)d.TotalHours} h {d.Minutes:00} min"
            : "inconnue";

        Console.WriteLine($"Randonnée         : {a.Nom}");
        Console.WriteLine($"Source            : {a.Source}");
        Console.WriteLine($"Date              : {a.Depart?.ToString("dd/MM/yyyy HH:mm") ?? "inconnue"}");
        Console.WriteLine($"Points            : {a.Points.Count}");
        Console.WriteLine($"Distance          : {a.DistanceTotale / 1000:F2} km");
        Console.WriteLine($"Altitude          : {a.AltitudeMin:F0} → {a.AltitudeMax:F0} m "
                        + $"(sommet au km {a.DistanceAuSommet / 1000:F1})");
        Console.WriteLine($"Dénivelé          : +{a.DenivelePositif:F0} m / -{a.DeniveleNegatif:F0} m");
        Console.WriteLine($"Pente max montée  : {a.PenteMaxMontee:P1}");
        Console.WriteLine($"Pente max descente: {a.PenteMaxDescente:P1}");
        Console.WriteLine($"Durée totale      : {Duree(a.DureeTotale)}");
        Console.WriteLine($"Durée en mouvement: {Duree(a.DureeMouvement)}"
                        + (a.PausesMinutes is double pm ? $"  ({pm:F0} min de pause)" : ""));

        if (a.VitesseMoyenne is double v)
            Console.WriteLine($"Vitesse moyenne   : {v:F1} km/h en mouvement");

        if (a.VitesseAscensionnelle is double va)
            Console.WriteLine($"Vitesse ascension.: {va:F0} m/h");

        Console.WriteLine($"Seuils            : dénivelé {a.SeuilDenivele:F1} m, "
                        + $"vitesse {a.SeuilVitesse:F2} m/s");
        Console.WriteLine();
    }

    public static void AfficherProfilAltimetrique(List<PointProfil> profil, int largeur = 80, int hauteur = 20)
    {
        if (profil.Count == 0) return;

        double altMin = profil.Min(p => p.Altitude);
        double altMax = profil.Max(p => p.Altitude);
        double amplitude = altMax - altMin;
        if (amplitude <= 0) amplitude = 1;

        double distTotale = profil[^1].DistanceCumulee;
        if (distTotale <= 0) return;

        double[] colonnes = new double[largeur];
        bool[] remplie = new bool[largeur];

        foreach (PointProfil p in profil)
        {
            int x = Math.Clamp((int)(p.DistanceCumulee / distTotale * (largeur - 1)), 0, largeur - 1);

            if (!remplie[x] || p.Altitude > colonnes[x])
            {
                colonnes[x] = p.Altitude;
                remplie[x] = true;
            }
        }

        for (int x = 1; x < largeur; x++)
            if (!remplie[x]) { colonnes[x] = colonnes[x - 1]; remplie[x] = true; }

        Console.WriteLine("Profil altimétrique");
        Console.WriteLine();

        for (int y = hauteur; y >= 0; y--)
        {
            double seuil = altMin + amplitude * y / hauteur;
            Console.Write($"{seuil,6:F0} m |");

            for (int x = 0; x < largeur; x++)
                Console.Write(colonnes[x] >= seuil - 1e-9 ? '#' : ' ');

            Console.WriteLine();
        }

        Console.WriteLine(new string(' ', 8) + "+" + new string('-', largeur));

        var graduation = new System.Text.StringBuilder(new string(' ', largeur));

        for (int k = 0; k <= 4; k++)
        {
            string texte = $"{distTotale / 1000.0 * k / 4:F1}";
            int pos = Math.Clamp(k * (largeur - 1) / 4 - texte.Length / 2, 0, largeur - texte.Length);

            for (int c = 0; c < texte.Length; c++) graduation[pos + c] = texte[c];
        }

        Console.WriteLine(new string(' ', 9) + graduation + "  km");
        Console.WriteLine();
    }

    /// <summary>
    /// Profil de pente : une ligne par tranche de distance, barre à droite pour
    /// les montées, à gauche pour les descentes. L'échelle est FIXE, ce qui rend
    /// deux traces comparables entre elles.
    /// </summary>
    public static void AfficherProfilPente(
        List<PointProfil> profil, int lignes = 40, int demiLargeur = 25, double penteEchelle = 0.50)
    {
        if (profil.Count == 0) return;

        double distTotale = profil[^1].DistanceCumulee;
        if (distTotale <= 0) return;

        double longueurTranche = distTotale / lignes;

        Console.WriteLine($"Profil de pente — barre pleine = {penteEchelle:P0}, "
                        + $"tranche = {longueurTranche:F0} m");
        Console.WriteLine();

        int index = 0;   // le profil est trié par distance : un seul parcours suffit

        for (int k = 0; k < lignes; k++)
        {
            double finTranche = (k + 1) * longueurTranche;

            double somme = 0;
            int compte = 0;

            while (index < profil.Count && profil[index].DistanceCumulee < finTranche)
            {
                somme += profil[index].Pente;
                compte++;
                index++;
            }

            double pente = compte > 0 ? somme / compte : 0;

            char[] ligne = new char[demiLargeur * 2 + 1];
            Array.Fill(ligne, ' ');
            ligne[demiLargeur] = '|';

            int longueur = (int)Math.Round(Math.Abs(pente) / penteEchelle * demiLargeur);
            bool tronquee = longueur > demiLargeur;
            longueur = Math.Clamp(longueur, 0, demiLargeur);

            for (int i = 1; i <= longueur; i++)
                ligne[pente > 0 ? demiLargeur + i : demiLargeur - i] = '#';

            if (tronquee) ligne[pente > 0 ? demiLargeur * 2 : 0] = '>';

            Console.WriteLine($"{k * longueurTranche / 1000.0,5:F1} km {new string(ligne)} {pente,8:P1}");
        }

        Console.WriteLine();
    }

    public static void AfficherStatsPente(Analyse a)
    {
        double[] bornes = { 0.10, 0.20, 0.30, 0.50 };

        void Repartition(string titre, List<Segment> segments)
        {
            if (segments.Count == 0) return;

            double total = segments.Sum(s => s.Denivele);
            if (total <= 0) return;

            var parTranche = segments
                .GroupBy(s => Tranches.IndiceTranche(s.Pente, bornes))
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Denivele));

            Console.WriteLine(titre);

            // On part des bornes et non des données : les tranches vides
            // apparaissent aussi, ce qui rend deux sorties comparables.
            for (int t = 0; t <= bornes.Length; t++)
            {
                string label = t == 0             ? $"0-{bornes[0] * 100:F0} %"
                             : t == bornes.Length ? $"> {bornes[^1] * 100:F0} %"
                             : $"{bornes[t - 1] * 100:F0}-{bornes[t] * 100:F0} %";

                double d = parTranche.TryGetValue(t, out double v) ? v : 0;

                Console.WriteLine($"{label,10} : {d,5:F0} m   ({d / total,5:P0})");
            }

            Console.WriteLine();
        }

        Repartition("Répartition du dénivelé positif", a.Montees);
        Repartition("Répartition du dénivelé négatif", a.Descentes);
    }

    public static void AfficherResumeLot(List<Analyse> analyses, int echecs)
    {
        Console.WriteLine($"{analyses.Count} trace(s) analysée(s)"
                        + (echecs > 0 ? $", {echecs} en échec" : ""));
        Console.WriteLine();

        Console.WriteLine($"{"Trace",-38} {"km",7} {"D+",7} {"D-",7} {"Mouv.",8}");
        Console.WriteLine(new string('-', 72));

        foreach (Analyse a in analyses.OrderBy(x => x.Depart))
        {
            string nom = a.Nom.Length > 37 ? a.Nom[..34] + "..." : a.Nom;
            string mouv = a.DureeMouvement is TimeSpan d ? $"{(int)d.TotalHours}h{d.Minutes:00}" : "—";

            Console.WriteLine($"{nom,-38} {a.DistanceTotale / 1000,7:F2} "
                            + $"{a.DenivelePositif,6:F0}m {a.DeniveleNegatif,6:F0}m {mouv,8}");
        }

        Console.WriteLine(new string('-', 72));
        Console.WriteLine($"{"TOTAL",-38} {analyses.Sum(a => a.DistanceTotale) / 1000,7:F2} "
                        + $"{analyses.Sum(a => a.DenivelePositif),6:F0}m "
                        + $"{analyses.Sum(a => a.DeniveleNegatif),6:F0}m");
        Console.WriteLine();
    }
}
