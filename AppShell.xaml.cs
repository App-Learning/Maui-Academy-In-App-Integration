using MauiAcademyApp.Pages;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;

namespace MauiAcademyApp;

public partial class AppShell : Shell
{
    private const int AcademyPreloadIntervalMs = 100;
    private const int MaxAcademyPreloadAttempts = 100;

    private readonly AcademyPage _academyPage = new();
    private int _academyPreloadAttempts;
    private bool _academyPreloadTimerRunning;
    private bool _academyPreloaded;

    public AppShell()
    {
        InitializeComponent();
        AcademyShellContent.Content = _academyPage;
        CurrentItem = HomeTab;
        Loaded += OnLoaded;
        HandlerChanged += OnHandlerChanged;
        TryStartAcademyPreloadTimer();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        TryStartAcademyPreloadTimer();
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        TryStartAcademyPreloadTimer();
    }

    private void TryStartAcademyPreloadTimer()
    {
        if (_academyPreloaded || _academyPreloadTimerRunning)
        {
            return;
        }

        if (Dispatcher is null)
        {
            return;
        }

        _academyPreloadTimerRunning = true;
        Dispatcher.StartTimer(
            TimeSpan.FromMilliseconds(AcademyPreloadIntervalMs),
            TryPreloadAcademy);
    }

    private bool TryPreloadAcademy()
    {
        _academyPreloadAttempts++;

        _academyPreloaded = _academyPage.Preload(ResolveMauiContext());
        if (_academyPreloaded || _academyPreloadAttempts >= MaxAcademyPreloadAttempts)
        {
            _academyPreloadTimerRunning = false;
            return false;
        }

        return true;
    }

    private IMauiContext? ResolveMauiContext()
    {
        return Handler?.MauiContext
            ?? _academyPage.Handler?.MauiContext
            ?? (Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page?.Handler?.MauiContext
                : null);
    }

    public void SetBottomTabBarVisible(bool visible)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MainTabBar.IsVisible = visible;
            Shell.SetTabBarIsVisible(_academyPage, visible);
            if (CurrentPage is not null)
            {
                Shell.SetTabBarIsVisible(CurrentPage, visible);
            }
        });
    }
}
