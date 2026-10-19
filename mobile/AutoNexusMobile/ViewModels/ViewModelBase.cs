using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AutoNexusMobile.ViewModels;

/// <summary>
/// Base des écrans : chargement au premier affichage, rechargement quand le serveur signale un
/// changement des données qu'il montre (temps réel), gestion commune de l'attente et des erreurs.
/// Un écran masqué ne recharge pas : il se marque périmé et recharge à son retour.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject, IRecipient<FleetChangedMessage>
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(400);

    private bool _active;
    private bool _stale = true;
    private bool _reloadScheduled;

    protected ViewModelBase()
    {
        WeakReferenceMessenger.Default.Register(this);
    }

    /// <summary>Chargement initial ou complet (squelette affiché).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    private bool _isLoading;

    /// <summary>Action en cours (boutons désactivés).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsIdle => !IsBusy;

    public bool IsReady => !IsLoading;

    /// <summary>Ensembles de données affichés par l'écran.</summary>
    protected virtual FleetChange Watches => FleetChange.None;

    /// <summary>Recharger à chaque affichage, même sans changement signalé (page de connexion).</summary>
    protected virtual bool ReloadOnEveryVisit => false;

    protected abstract Task LoadAsync();

    public async Task ActivateAsync()
    {
        _active = true;
        if (_stale || ReloadOnEveryVisit)
        {
            _stale = false;
            await ReloadAsync(showSkeleton: true);
        }
    }

    public void Deactivate() => _active = false;

    /// <summary>À appeler après une modification faite par l'écran lui-même.</summary>
    protected void MarkStale() => _stale = true;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ReloadAsync(showSkeleton: false);
        IsRefreshing = false;
    }

    public void Receive(FleetChangedMessage message)
    {
        if ((message.Change & Watches) == 0)
            return;

        if (!_active)
        {
            _stale = true;
            return;
        }

        if (_reloadScheduled)
            return;

        _reloadScheduled = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(Debounce);
            _reloadScheduled = false;
            await ReloadAsync(showSkeleton: false);
        });
    }

    protected async Task ReloadAsync(bool showSkeleton)
    {
        if (showSkeleton)
            IsLoading = true;

        try
        {
            await LoadAsync();
            ErrorMessage = null;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Exécute une action de l'utilisateur ; l'erreur éventuelle est affichée.</summary>
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        if (IsBusy)
            return false;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
            return true;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Prévient les autres écrans d'une modification faite ici (sans attendre le serveur).</summary>
    protected static void Broadcast(FleetChange change) =>
        WeakReferenceMessenger.Default.Send(new FleetChangedMessage(change));
}
