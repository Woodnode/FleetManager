using AutoNexusMobile.Core.Realtime;

namespace AutoNexusMobile.Services;

/// <summary>
/// Le serveur signale un changement (temps réel) ou l'app vient d'en faire un : les écrans
/// concernés rechargent leurs données. Diffusé par WeakReferenceMessenger : un écran fermé ne
/// reste pas abonné.
/// </summary>
public sealed record FleetChangedMessage(FleetChange Change);
