using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Auth;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using CommunityToolkit.Mvvm.Messaging;

namespace AutoNexusMobile.Services;

/// <summary>
/// Cycle de vie de la session dans l'app : restauration au lancement, connexion, onglets selon
/// le rôle, canal temps réel ouvert pendant la session, retour à la connexion à la déconnexion
/// ou quand le refresh token est refusé.
/// </summary>
public sealed class AppSession
{
    private readonly SessionManager _sessions;
    private readonly FleetRealtime _realtime;
    private bool _expiredNotice;

    public AppSession(SessionManager sessions, FleetRealtime realtime, RealtimeState realtimeState)
    {
        _sessions = sessions;
        _realtime = realtime;

        _sessions.SessionExpired += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await SignOutAsync(expired: true));
        _realtime.Changed += (_, change) =>
            MainThread.BeginInvokeOnMainThread(() => WeakReferenceMessenger.Default.Send(new FleetChangedMessage(change)));
        _realtime.StatusChanged += (_, status) => MainThread.BeginInvokeOnMainThread(() => realtimeState.Status = status);
    }

    public AuthSession? Current => _sessions.Current;

    public UserRole? Role => _sessions.Current?.Role;

    public Guid? UserId => _sessions.Current?.UserId;

    /// <summary>L'utilisateur peut-il faire avancer cette intervention (voir RolePermissions) ?</summary>
    public bool CanActOn(InterventionDto intervention) =>
        RolePermissions.CanActOnIntervention(Role, UserId, intervention.TechnicianId);

    public async Task StartAsync()
    {
        var session = await _sessions.RestoreAsync();
        if (session is null)
            await Shell.Current.GoToAsync("//login");
        else
            await EnterAsync(session);
    }

    public async Task LoginAsync(string email, string password)
    {
        var session = await _sessions.LoginAsync(email, password);
        await EnterAsync(session);
    }

    public async Task SignOutAsync(bool expired = false)
    {
        await _realtime.StopAsync();
        if (!expired)
            await _sessions.LogoutAsync();

        _expiredNotice = expired;
        await Shell.Current.GoToAsync("//login");
    }

    /// <summary>Vrai une seule fois après une expiration : la page de connexion l'explique.</summary>
    public bool TakeExpiredNotice()
    {
        var notice = _expiredNotice;
        _expiredNotice = false;
        return notice;
    }

    private async Task EnterAsync(AuthSession session)
    {
        if (Shell.Current is AppShell shell)
            shell.ConfigureFor(session.Role);

        await Shell.Current.GoToAsync(session.Role == UserRole.Technician ? "//main/today" : "//main/dashboard");
        await _realtime.StartAsync();
    }
}
