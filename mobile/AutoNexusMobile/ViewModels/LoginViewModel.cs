using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>Compte de démonstration proposé sous le formulaire (Debug uniquement).</summary>
public sealed record DemoAccount(string Role, string Email);

public sealed partial class LoginViewModel : ViewModelBase
{
    // Mot de passe des comptes de démonstration du seed (DatabaseSeeder), publié sur le web.
    public const string DemoPassword = "Fleet@2024";

    private readonly AppSession _session;

    public LoginViewModel(AppSession session)
    {
        _session = session;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isPasswordHidden = true;

    [ObservableProperty]
    private string? _emailError;

    [ObservableProperty]
    private string? _passwordError;

    [ObservableProperty]
    private string? _notice;

    /// <summary>Les mêmes trois comptes que la page de connexion du web.</summary>
    public IReadOnlyList<DemoAccount> DemoAccounts { get; } =
    [
        new("Admin", "admin@fleetmanager.fr"),
        new("Manager", "directeur.paris@fleetmanager.fr"),
        new("Technicien", "tech1.paris@fleetmanager.fr"),
    ];

#if DEBUG
    public bool ShowDemoAccounts => true;
#else
    public bool ShowDemoAccounts => false;
#endif

    public string DemoPasswordText => DemoPassword;

    protected override bool ReloadOnEveryVisit => true;

    protected override Task LoadAsync()
    {
        Notice = _session.TakeExpiredNotice() ? "Votre session a expiré. Reconnectez-vous." : null;
        Password = string.Empty;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void TogglePassword() => IsPasswordHidden = !IsPasswordHidden;

    [RelayCommand]
    private void UseDemo(DemoAccount account)
    {
        Email = account.Email;
        Password = DemoPassword;
        EmailError = PasswordError = null;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        // Mêmes règles que le schéma Zod du web (schemas/login.ts).
        EmailError = System.Net.Mail.MailAddress.TryCreate(Email.Trim(), out _) ? null : "Email invalide";
        PasswordError = string.IsNullOrEmpty(Password) ? "Mot de passe requis" : null;
        if (EmailError is not null || PasswordError is not null)
            return;

        Notice = null;
        await RunAsync(() => _session.LoginAsync(Email, Password));
    }

    partial void OnEmailChanged(string value) => EmailError = null;

    partial void OnPasswordChanged(string value) => PasswordError = null;
}
