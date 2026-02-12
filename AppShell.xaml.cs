using MauiAcademyApp.Pages;
using Microsoft.Maui.ApplicationModel;

namespace MauiAcademyApp;

public partial class AppShell : Shell
{
    private readonly AcademyPage _academyPage = new();
    private bool _academyPreloaded;

    public AppShell()
    {
        InitializeComponent();
        AcademyShellContent.Content = _academyPage;
        Loaded += OnLoaded;
    }

    public void SetBottomTabBarVisible(bool visible)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MainTabBar.IsVisible = visible;
            Shell.SetTabBarIsVisible(_academyPage, visible);
        });
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        await PreloadAcademyWebViewAsync();
    }

    private async Task PreloadAcademyWebViewAsync()
    {
        if (_academyPreloaded)
        {
            return;
        }

        _academyPreloaded = true;

        try
        {
            // Trigger the WebView to load and execute the bridge script to warm up the WebView and its JavaScript engine.
            await Task.Yield();
            await GoToAsync("//academy", false);
            await Task.Delay(150);
            await GoToAsync("//home", false);
        }
        catch
        {
            // Best-effort warmup only.
        }
    }
}
