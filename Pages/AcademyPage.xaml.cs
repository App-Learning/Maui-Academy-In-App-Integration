using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Platform;

namespace MauiAcademyApp.Pages;

public partial class AcademyPage : ContentPage
{
    private const string AcademyBaseUrl = "https://your-academy-domain.example/inapp";
    private const string AcademyUserId = "put-the-user-id-here";
    private static readonly bool UseJwtToken = false;
    private static string AcademyUrl => UseJwtToken
        ? AcademyBaseUrl
        : $"{AcademyBaseUrl}?userID={Uri.EscapeDataString(AcademyUserId)}";
    private const string JwtToken = "put-your-jwt-token-here";
    private const string MessageHost = "maui-bridge://message";

    private readonly List<string> _messageHistory = new();
    private bool _preloaded;

    private static readonly IReadOnlyList<string> BridgeScriptSteps =
    [
        "window.__mauiBridgeInstalled===true?'already-installed':'continue';",
        "window.__mauiBridgePostToNative=function(payload){var s='';try{s=typeof payload==='string'?payload:JSON.stringify(payload);}catch(e){s=String(payload);}window.location.href='maui-bridge://message?data='+encodeURIComponent(s);return s;};'post-fn-ready';",
        "window.ReactNativeWebView=(typeof window.ReactNativeWebView==='object'&&window.ReactNativeWebView!==null)?window.ReactNativeWebView:{};window.ReactNativeWebView.postMessage=window.__mauiBridgePostToNative;'rn-ready';",
        "window.MauiBridge=(typeof window.MauiBridge==='object'&&window.MauiBridge!==null)?window.MauiBridge:{};window.MauiBridge.postMessage=window.__mauiBridgePostToNative;'maui-ready';",
        "window.__mauiBridgeInstalled=true;'installed';"
    ];

    public AcademyPage()
    {
        InitializeComponent();

        if (UseJwtToken)
        {
            AcademyWebView.Source = "about:blank";
            AcademyWebView.HandlerChanged += OnAcademyWebViewHandlerChanged;
        }
        else
        {
            AcademyWebView.Source = AcademyUrl;
        }
    }

    private void OnAcademyWebViewHandlerChanged(object? sender, EventArgs e)
    {
        if (!UseJwtToken)
        {
            return;
        }

        LoadAcademyWithJwtHeader();
    }

    private void LoadAcademyWithJwtHeader()
    {
#if ANDROID
        var nativeWebView = AcademyWebView.Handler?.PlatformView as Android.Webkit.WebView;
        if (nativeWebView is not null)
        {
            var headers = new Dictionary<string, string>
            {
                ["token"] = JwtToken
            };
            nativeWebView.LoadUrl(AcademyUrl, headers);
        }
#elif IOS || MACCATALYST
        var nativeWebView = AcademyWebView.Handler?.PlatformView as WebKit.WKWebView;
        if (nativeWebView is not null)
        {
            var request = new Foundation.NSMutableUrlRequest(new Foundation.NSUrl(AcademyUrl));
            request["token"] = JwtToken;
            nativeWebView.LoadRequest(request);
        }
#else
        AcademyWebView.Source = AcademyUrl;
#endif
    }

    public bool Preload(IMauiContext? preferredContext)
    {
        if (_preloaded)
        {
            return true;
        }

        var mauiContext = preferredContext
            ?? AcademyWebView.Handler?.MauiContext
            ?? Handler?.MauiContext
            ?? (Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page?.Handler?.MauiContext
                : null);
        if (mauiContext is null)
        {
            return false;
        }

        if (Handler is null)
        {
            this.ToHandler(mauiContext);
        }

        if (Handler is null)
        {
            return false;
        }

        if (AcademyWebView.Handler is null)
        {
            AcademyWebView.ToHandler(mauiContext);
        }

        if (AcademyWebView.Handler is null)
        {
            return false;
        }

        _preloaded = true;

        if (UseJwtToken)
        {
            LoadAcademyWithJwtHeader();
            return true;
        }

#if ANDROID
        var androidWebView = AcademyWebView.Handler?.PlatformView as Android.Webkit.WebView;
        androidWebView?.LoadUrl(AcademyUrl);
#elif IOS || MACCATALYST
        var iosWebView = AcademyWebView.Handler?.PlatformView as WebKit.WKWebView;
        if (iosWebView is not null)
        {
            var request = new Foundation.NSUrlRequest(new Foundation.NSUrl(AcademyUrl));
            iosWebView.LoadRequest(request);
        }
#else
        AcademyWebView.Source = AcademyUrl;
#endif
        return true;
    }

    private void OnAcademyWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith(MessageHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;

        var message = TryExtractMessage(e.Url);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        OnAcademyMessageReceived(message);
    }

    private async void OnAcademyWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result != WebNavigationResult.Success)
        {
            LastMessageLabel.Text = $"WebView navigation failed: {e.Result}";
            return;
        }

        try
        {
            var bridgeResult = await InstallBridgeAsync();

            if (bridgeResult.StartsWith("bridge-error:", StringComparison.OrdinalIgnoreCase))
            {
                LastMessageLabel.Text = $"Bridge injection failed: {bridgeResult}";
            }
        }
        catch (Exception ex)
        {
            LastMessageLabel.Text = $"Bridge injection failed: {ex.Message}";
        }
    }

    private async Task<string> InstallBridgeAsync()
    {
        string first;
        try
        {
            first = await AcademyWebView.EvaluateJavaScriptAsync(BridgeScriptSteps[0]) ?? string.Empty;
            if (string.Equals(first, "already-installed", StringComparison.OrdinalIgnoreCase))
            {
                return "already-installed";
            }
        }
        catch (Exception ex)
        {
            return $"bridge-error:step-0:{ex.Message}";
        }

        for (var i = 1; i < BridgeScriptSteps.Count; i++)
        {
            try
            {
                var stepResult = await AcademyWebView.EvaluateJavaScriptAsync(BridgeScriptSteps[i]) ?? string.Empty;
            }
            catch (Exception ex)
            {
                return $"bridge-error:step-{i}:{ex.Message}";
            }
        }

        return "installed";
    }

    private void OnAcademyMessageReceived(string message)
    {
        HandleIntegrationAction(message);

        var stamped = $"[{DateTime.Now:HH:mm:ss}] {message}";

        _messageHistory.Insert(0, stamped);
        if (_messageHistory.Count > 4)
        {
            _messageHistory.RemoveAt(_messageHistory.Count - 1);
        }

        var summary = new StringBuilder();
        foreach (var item in _messageHistory)
        {
            if (summary.Length > 0)
            {
                summary.AppendLine();
            }

            summary.Append(item);
        }

        LastMessageLabel.Text = summary.ToString();
    }

    protected override bool OnBackButtonPressed()
    {
        SendHostEvent("navigation_go_back");
        return true;
    }

    private async void SendHostEvent(string action, string? value = null)
    {
        var payload = JsonSerializer.Serialize(new MessageData
        {
            Action = action,
            Value = value
        });
        var script = $$"""
        (function () {
          var data = {{JsonSerializer.Serialize(payload)}};
          if (typeof MessageEvent === 'function') {
            window.dispatchEvent(new MessageEvent('message', { data: data }));
            return;
          }

          if (typeof window.postMessage === 'function') {
            window.postMessage(data, '*');
          }
        })();
        """;

        try
        {
            await AcademyWebView.EvaluateJavaScriptAsync(script);
        }
        catch (Exception ex)
        {
            LastMessageLabel.Text = $"Failed sending host event: {ex.Message}";
        }
    }

    private void HandleIntegrationAction(string rawMessage)
    {
        var data = TryParseMessageData(rawMessage);
        if (string.IsNullOrWhiteSpace(data.Action))
        {
            return;
        }

        switch (data.Action.Trim().ToLowerInvariant())
        {
            case "open_subpage":
                SetBottomTabBarVisible(false);
                break;
            case "close_subpage":
                SetBottomTabBarVisible(true);
                break;
            case "launch_url":
                if (!string.IsNullOrWhiteSpace(data.Value))
                {
                    _ = Launcher.OpenAsync(data.Value);
                }
                break;
            case "game_points_redeemed":
            case "claim_reward":
                // Keep payload in history; integrate app-side business logic here.
                break;
        }
    }

    private static MessageData TryParseMessageData(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            return MessageData.Empty;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<MessageData>(rawMessage);
            if (!string.IsNullOrWhiteSpace(parsed?.Action))
            {
                return parsed;
            }
        }
        catch
        {
            // Ignore and fall back below.
        }

        try
        {
            var innerJson = JsonSerializer.Deserialize<string>(rawMessage);
            if (!string.IsNullOrWhiteSpace(innerJson))
            {
                var innerParsed = JsonSerializer.Deserialize<MessageData>(innerJson);
                if (!string.IsNullOrWhiteSpace(innerParsed?.Action))
                {
                    return innerParsed;
                }
            }
        }
        catch
        {
            // Ignore and fall back below.
        }

        try
        {
            using var doc = JsonDocument.Parse(rawMessage);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("data", out var dataProperty))
            {
                var innerJson = dataProperty.ValueKind == JsonValueKind.String
                    ? dataProperty.GetString()
                    : dataProperty.ToString();

                if (!string.IsNullOrWhiteSpace(innerJson))
                {
                    var innerParsed = JsonSerializer.Deserialize<MessageData>(innerJson);
                    if (!string.IsNullOrWhiteSpace(innerParsed?.Action))
                    {
                        return innerParsed;
                    }
                }
            }
        }
        catch
        {
            // Ignore and fall back below.
        }

        var normalized = rawMessage.Trim().Trim('"');
        if (normalized.Equals("open_subpage", StringComparison.OrdinalIgnoreCase))
        {
            return new MessageData { Action = "open_subpage" };
        }

        if (normalized.Equals("close_subpage", StringComparison.OrdinalIgnoreCase))
        {
            return new MessageData { Action = "close_subpage" };
        }

        return MessageData.Empty;
    }

    private void SetBottomTabBarVisible(bool visible)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Shell.Current is AppShell appShell)
            {
                appShell.SetBottomTabBarVisible(visible);
            }

            Shell.SetTabBarIsVisible(this, visible);

            if (Shell.Current?.CurrentPage is Page currentPage)
            {
                Shell.SetTabBarIsVisible(currentPage, visible);
            }
        });
    }

    private static string? TryExtractMessage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var query = uri.Query.TrimStart('?');
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var keyValue = pair.Split('=', 2);
            if (!keyValue[0].Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var encoded = keyValue.Length > 1 ? keyValue[1] : string.Empty;
            encoded = encoded.Replace("+", "%20", StringComparison.Ordinal);
            return Uri.UnescapeDataString(encoded);
        }

        return null;
    }

    private sealed class MessageData
    {
        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("value")]
        public string? Value { get; init; }

        public static MessageData Empty { get; } = new();
    }
}
