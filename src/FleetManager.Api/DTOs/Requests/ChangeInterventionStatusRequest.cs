using FleetManager.Domain.Enums;

namespace FleetManager.Api.DTOs.Requests;

/// <param name="NewStatus">Statut visé : InProgress, Completed ou Cancelled.</param>
/// <param name="Comment">Commentaire de clôture, ou raison d'annulation (obligatoire pour Cancelled).</param>
/// <param name="Mileage">Kilométrage relevé à la clôture (Completed uniquement), facultatif.</param>
public record ChangeInterventionStatusRequest(InterventionStatus NewStatus, string? Comment, int? Mileage = null);
