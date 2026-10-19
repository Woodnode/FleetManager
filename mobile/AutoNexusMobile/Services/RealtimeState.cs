using AutoNexusMobile.Core.Realtime;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoNexusMobile.Services;

/// <summary>État de la connexion temps réel, affiché par la pastille du tableau de bord et du compte.</summary>
public sealed partial class RealtimeState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLive), nameof(Label), nameof(Tone), nameof(Pulses))]
    private RealtimeStatus _status = RealtimeStatus.Offline;

    public bool IsLive => Status == RealtimeStatus.Connected;

    /// <summary>Libellés et teintes du RealtimeBadge du web.</summary>
    public string Label => Status switch
    {
        RealtimeStatus.Connected => "En direct",
        RealtimeStatus.Connecting => "Connexion...",
        RealtimeStatus.Reconnecting => "Reconnexion...",
        _ => "Hors ligne",
    };

    public string Tone => Status switch
    {
        RealtimeStatus.Connected => "Emerald",
        RealtimeStatus.Reconnecting => "Amber",
        _ => "Slate",
    };

    public bool Pulses => Status is RealtimeStatus.Connected or RealtimeStatus.Reconnecting;
}
