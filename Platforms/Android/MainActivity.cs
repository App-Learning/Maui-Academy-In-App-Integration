using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MauiAcademyApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private static bool _webViewWarmupDone;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        WarmupWebViewEngine();
    }

    private void WarmupWebViewEngine()
    {
        if (_webViewWarmupDone)
        {
            return;
        }

        _webViewWarmupDone = true;

        try
        {
            using var warmupWebView = new Android.Webkit.WebView(this);
            warmupWebView.LoadDataWithBaseURL(
                "about:blank",
                "<html><body></body></html>",
                "text/html",
                "utf-8",
                null);
            warmupWebView.StopLoading();
        }
        catch
        {
            // Best-effort startup warmup only.
        }
    }
}
