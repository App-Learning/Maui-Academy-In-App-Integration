using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.ApplicationModel;

namespace MauiAcademyApp.Pages;

public partial class AcademyPage : ContentPage
{
    private const string AcademyBaseUrl = "https://your-academy-domain.example/inapp";
    private const string AcademyUserId = "1241";
    private static string AcademyUrl => $"{AcademyBaseUrl}?userID={Uri.EscapeDataString(AcademyUserId)}";
    private const string MessageHost = "maui-bridge://message";

    private readonly List<string> _messageHistory = new();

    private static readonly string BridgeScript = """
(function () {
  if (window.__mauiBridgeInstalled) return;
  window.__mauiBridgeInstalled = true;

  function toMessageString(payload) {
    if (typeof payload === 'string') return payload;
    try {
      return JSON.stringify(payload);
    } catch {
      return String(payload);
    }
  }

  function sendToNative(payload) {
    var encoded = encodeURIComponent(toMessageString(payload));
    window.location.href = 'maui-bridge://message?data=' + encoded;
  }

  window.ReactNativeWebView = window.ReactNativeWebView || {};
  window.ReactNativeWebView.postMessage = sendToNative;

  var originalPostMessage = window.postMessage;
  window.postMessage = function (message, targetOrigin, transfer) {
    sendToNative(message);
    if (typeof originalPostMessage === 'function') {
      return originalPostMessage.apply(window, arguments);
    }
  };

  window.MauiBridge = {
    postMessage: sendToNative
  };
})();
""";

    public AcademyPage()
    {
        InitializeComponent();
        AcademyWebView.Source = AcademyUrl;
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
            await AcademyWebView.EvaluateJavaScriptAsync(BridgeScript);
        }
        catch (Exception ex)
        {
            LastMessageLabel.Text = $"Bridge injection failed: {ex.Message}";
        }
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
        var script = $"window.postMessage({JsonSerializer.Serialize(payload)}, '*');";

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
