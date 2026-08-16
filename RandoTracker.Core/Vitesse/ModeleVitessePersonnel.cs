namespace RandoTracker.Core.Vitesse;

/// <summary>
/// Le modèle de vitesse appris depuis l'historique des sorties archivées :
/// une vitesse par tranche de pente, plus une vitesse globale de repli pour
/// les tranches encore sans données.
/// </summary>
public record ModeleVitessePersonnel(List<TrancheVitesse> Tranches, double? VitesseGlobaleKmh, int NombreSegments);
