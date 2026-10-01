using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Vault.App.Wpf.Services;

/// <summary>
/// Minimal view-model-first navigation for the single-window shell.
/// </summary>
public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }

    event EventHandler? CurrentViewModelChanged;

    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;

    void NavigateTo<TViewModel>(Action<TViewModel> configure) where TViewModel : ViewModelBase;
}

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public ViewModelBase? CurrentViewModel { get; private set; }

    public event EventHandler? CurrentViewModelChanged;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        => NavigateTo<TViewModel>(_ => { });

    public void NavigateTo<TViewModel>(Action<TViewModel> configure) where TViewModel : ViewModelBase
    {
        var viewModel = _services.GetRequiredService<TViewModel>();
        configure(viewModel);
        viewModel.OnNavigatedTo();
        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Base class for all view models.</summary>
public abstract class ViewModelBase : ObservableObject
{
    public virtual void OnNavigatedTo() { }
}
