# Maui Academy Integration

This project contains a 3-tab .NET MAUI app skeleton where tab 3 hosts your academy web app in a `WebView`.

## What is implemented

- 3 tabs via `Shell`:
  - `Home`
  - `Courses`
  - `Academy`
- Academy tab loads a web URL in `WebView`.
- JavaScript bridge that maps React Native-style messaging to MAUI:
  - `window.ReactNativeWebView.postMessage(data)`
  - `window.postMessage(data, ...)`
  - `window.MauiBridge.postMessage(data)`
- Native listener receives messages via `WebView.Navigating` and handles payload from `maui-bridge://message?data=...`.
- Message contract from your PDF:
  - JSON payload: `{ "action": "...", "value": "..." }`
  - Handled actions: `open_subpage`, `close_subpage`, `launch_url`, `game_points_redeemed`, `claim_reward`
- Host-to-integration event from MAUI:
  - Sends `navigation_go_back` on Android back-button press inside academy tab.

## Configure your academy URL

Edit `Pages/AcademyPage.xaml.cs`:

```csharp
private const string AcademyUrl = "https://your-academy-domain.example";
```

Set it to your real academy URL.

## JS usage example in your web app

```javascript
window.ReactNativeWebView.postMessage(JSON.stringify({
  action: "open_subpage"
}));
```
