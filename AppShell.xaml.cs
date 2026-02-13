using MauiAcademyApp.Pages;
using Microsoft.Maui.ApplicationModel;

namespace MauiAcademyApp;

public partial class AppShell : Shell
{
    private readonly AcademyPage _academyPage = new();

    public AppShell()
    {
        InitializeComponent();
        AcademyShellContent.Content = _academyPage;
        CurrentItem = HomeTab;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
#if ANDROID
        _ = WarmupAcademyByTabSwitchAsync();
#else
        _ = PreloadAcademyAsync();
#endif
    }

// TODO: Hacky workaround on Android to make initial Academy loading smooth --> should be fixed by WebView2 on Android when it becomes available in .NET MAUI. See
#if ANDROID
    private async Task WarmupAcademyByTabSwitchAsync()
    {
        try
        {
            await Task.Yield();
            await GoToAsync("//academy", false);
            await Task.Delay(10);
            await GoToAsync("//home", false);
        }
        catch
        {
            // Best-effort warmup only.
        }
    }
#endif

    private async Task PreloadAcademyAsync()
    {
        // Give Shell handler/context a brief moment to become fully available.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var mauiContext = Handler?.MauiContext;
            if (mauiContext is not null)
            {
                _academyPage.Preload(mauiContext);
                return;
            }

            await Task.Delay(100);
        }

        // Final best-effort attempt with any available context.
        _academyPage.Preload(null);
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
