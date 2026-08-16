using System.Xml.Linq;
using RandoTracker.Core.Calculs;
using RandoTracker.Core.Modele;

namespace RandoTracker.Core.Gpx;

/// <summary>Analyse complète d'un fichier GPX : lecture, calculs, agrégation.</summary>
public static class AnalyseurTrace
{
    /// <summary>
    /// Analyse le fichier. En cas d'échec, Analyse est null et Erreur décrit la
    /// raison (fichier absent du disque, XML invalide, aucun point exploitable) —
    /// c'est à l'appelant de décider comment la signaler (console, API...).
    ///
    /// `nomFichier` sert d'étiquette (nom affiché, repli si le GPX n'a pas de
    /// balise &lt;name&gt;) quand `chemin` est un fichier temporaire sans rapport
    /// avec le nom d'origine — cas d'un upload web.
    /// </summary>
    public static (Analyse? Analyse, string? Erreur) AnalyserFichier(
        string chemin, ParametresAnalyse parametres, string? nomFichier = null)
    {
        nomFichier ??= chemin;

        XDocument doc;

        try
        {
            doc = XDocument.Load(chemin);
        }
        catch (System.Xml.XmlException ex)
        {
            return (null, $"XML invalide ({ex.Message})");
        }

        List<Point> points = LecteurGpx.LirePoints(doc);

        if (points.Count == 0)
        {
            return (null, "aucun point GPS exploitable");
        }

        string nom = doc.Descendants(LecteurGpx.Namespace + "name").FirstOrDefault()?.Value
                     ?? Path.GetFileNameWithoutExtension(nomFichier);

        List<PointProfil> profil = ProfilCalculateur.CalculerProfil(points, parametres.FenetrePente);

        // Une seule extraction de segments filtrés alimente à la fois le cumul
        // du dénivelé et la répartition par pente : les deux sont cohérents.
        List<Segment> montees = SegmentExtracteur.ExtraireSegments(profil, parametres.SeuilDenivele, montee: true);
        List<Segment> descentes = SegmentExtracteur.ExtraireSegments(profil, parametres.SeuilDenivele, montee: false);

        DateTime? depart = points.FirstOrDefault(p => p.Time is not null)?.Time;
        DateTime? arrivee = points.LastOrDefault(p => p.Time is not null)?.Time;

        var analyse = new Analyse
        {
            Fichier = nomFichier,
            Nom = nom,
            Source = LecteurGpx.DetecterSource(doc, points),
            Points = points,
            Profil = profil,
            Montees = montees,
            Descentes = descentes,
            Depart = depart,
            DureeTotale = arrivee - depart,
            DureeMouvement = DureeCalculateur.CalculerDureeEnMouvement(points, parametres.SeuilVitesse),
            TempsEnMontee = DureeCalculateur.CalculerTempsEnMontee(profil, parametres.SeuilDenivele),
            DistanceTotale = Geo.CalculerDistanceTotale(points),
            DenivelePositif = montees.Sum(s => s.Denivele),
            DeniveleNegatif = descentes.Sum(s => s.Denivele),
            SeuilDenivele = parametres.SeuilDenivele,
            SeuilVitesse = parametres.SeuilVitesse,
        };

        return (analyse, null);
    }
}
