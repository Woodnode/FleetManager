using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Views;
using CommunityToolkit.Mvvm.Messaging;

namespace AutoNexusMobile.Services;

/// <summary>
/// Changement de statut d'une intervention, partagé par la journée, la liste et la fiche :
/// feuille de saisie, appel de l'API, confirmation. Rend l'intervention mise à jour, ou null si
/// l'utilisateur renonce ou si l'API refuse (le message d'erreur est alors affiché).
/// </summary>
public sealed class InterventionFlow
{
    private readonly FleetApi _api;

    public InterventionFlow(FleetApi api) => _api = api;

    public async Task<InterventionDto?> ChangeStatusAsync(InterventionDto intervention, InterventionStatus target)
    {
        int? mileage = null;
        if (target == InterventionStatus.Completed)
        {
            try
            {
                mileage = (await _api.GetVehicleAsync(intervention.VehicleId)).Mileage;
            }
            catch (ApiException)
            {
                // Sans le dernier relevé, la saisie reste possible : le serveur vérifie la baisse.
            }
        }

        var input = await InterventionActionSheet.AskAsync(intervention, target, mileage);
        if (input is null)
            return null;

        try
        {
            var updated = await _api.ChangeInterventionStatusAsync(intervention.Id, target, input.Comment, input.Mileage);
            Toasts.Show(target switch
            {
                InterventionStatus.InProgress => "Intervention démarrée",
                InterventionStatus.Completed => "Intervention terminée",
                _ => "Intervention annulée",
            });
            WeakReferenceMessenger.Default.Send(new FleetChangedMessage(FleetChange.Interventions | FleetChange.Vehicles));
            return updated;
        }
        catch (ApiException ex)
        {
            Toasts.Show(ex.Message, error: true);
            return null;
        }
    }
}
