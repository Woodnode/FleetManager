using AutoNexusMobile.Controls;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;

namespace AutoNexusMobile.Views;

/// <summary>Saisie du changement de statut ; null si l'utilisateur renonce.</summary>
public sealed record StatusChangeInput(string? Comment, int? Mileage);

public partial class InterventionActionSheet : BottomSheet
{
    private readonly InterventionStatus _target;
    private readonly int? _currentMileage;

    private InterventionActionSheet(InterventionDto intervention, InterventionStatus target, int? currentMileage)
    {
        InitializeComponent();
        _target = target;
        _currentMileage = currentMileage;

        TitleLabel.Text = target switch
        {
            InterventionStatus.InProgress => "Démarrer l'intervention",
            InterventionStatus.Completed => "Terminer l'intervention",
            _ => "Annuler l'intervention",
        };
        VehicleLabel.Text = FleetFormat.Vehicle(intervention.VehicleBrand, intervention.VehicleModel);
        VinLabel.Text = intervention.VehicleVin;

        CommentBlock.IsVisible = target is InterventionStatus.Completed or InterventionStatus.Cancelled;
        CommentLabel.Text = target == InterventionStatus.Cancelled ? "Raison *" : "Commentaire de clôture";
        CommentEditor.Placeholder = target == InterventionStatus.Cancelled ? "Raison de l'annulation..." : "Optionnel...";

        MileageBlock.IsVisible = target == InterventionStatus.Completed;
        if (currentMileage is { } km)
        {
            MileageEntry.Placeholder = km.ToString();
            MileageHint.Text = $"Dernier relevé : {FleetFormat.Kilometers(km)}. Laisser vide pour le garder.";
        }
        else
        {
            MileageHint.Text = "Facultatif.";
        }

        ConfirmButton.Text = FleetFormat.ActionLabel(target);
        if (target == InterventionStatus.Cancelled)
            ConfirmButton.Style = (Style)Microsoft.Maui.Controls.Application.Current!.Resources["DestructiveButton"];
    }

    public static Task<StatusChangeInput?> AskAsync(InterventionDto intervention, InterventionStatus target, int? currentMileage) =>
        ShowAsync<StatusChangeInput>(new InterventionActionSheet(intervention, target, currentMileage));

    private async void OnCancelClicked(object? sender, EventArgs e) => await CloseAsync();

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        var comment = string.IsNullOrWhiteSpace(CommentEditor.Text) ? null : CommentEditor.Text.Trim();
        int? mileage = null;

        if (_target == InterventionStatus.Cancelled && comment is null)
        {
            ShowError("Une raison est requise pour annuler");
            return;
        }

        if (_target == InterventionStatus.Completed && !string.IsNullOrWhiteSpace(MileageEntry.Text))
        {
            var digits = new string(MileageEntry.Text.Where(char.IsDigit).ToArray());
            if (!int.TryParse(digits, out var km) || km > 2_000_000)
            {
                ShowError("Kilométrage invalide.");
                return;
            }
            if (_currentMileage is { } current && km < current)
            {
                ShowError($"Le kilométrage ne peut pas descendre sous {FleetFormat.Kilometers(current)}.");
                return;
            }
            mileage = km;
        }

        await CloseAsync(new StatusChangeInput(comment, mileage));
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
