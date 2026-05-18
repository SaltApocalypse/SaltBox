You are assisting in the development of a Windows desktop application project named SaltBox.

IMPORTANT: Use Chinese when communicating with the user. All responses, explanations, and discussions must be in Chinese.

# Project Overview

SaltBox is a personal Windows toolbox application built with WinUI 3 and .NET 8.

Purpose:

- A collection of small personal utility tools.
- New tools may be added at any time.
- Fast prototyping and rapid iteration are priorities.
- Strong Windows integration is required.
- Only Windows 10 and above are targeted.
- No database is currently used.
- Logging is required.
- Code should remain easy for AI agents to understand and extend.

# Core Technology Stack

- .NET 8
- WinUI 3 (Windows App SDK 2.0.1)
- CommunityToolkit.Mvvm
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.DependencyInjection
- Serilog

# Architecture Rules

This project follows a strict module-based architecture.

Each tool MUST be implemented as an independent module.

Rule:

- One tool = one folder under `Modules/`.
- Do not scatter files across unrelated directories.

# Directory Structure

Current project layout:

```
SaltBox/
├── SaltBox.slnx
├── Agent.md
├── IdentityPackage/                 # Sparse package for package identity
│   ├── AppxManifest.xml
│   └── BuildIdentityPackage.cmd
├── SaltBox/                         # Main WinUI 3 project (unpackaged)
│   ├── SaltBox.csproj
│   ├── App.xaml / .cs               # DI host + Serilog + Velopack setup
│   ├── MainWindow.xaml / .cs        # NavigationView shell
│   ├── app.manifest                 # Contains msix element for identity binding
│   ├── Views/                       # Pages (HomePage, SettingsPage, ScreenshotPage, ...)
│   ├── ViewModels/                  # MVVM ViewModels
│   ├── Services/                    # Application services
│   │   ├── LogService.cs
│   │   ├── ToolRegistry.cs
│   │   ├── ThemeService.cs
│   │   ├── CultureService.cs
│   │   ├── TrayService.cs
│   │   ├── UpdateService.cs
│   │   ├── ScreenshotService.cs
│   │   └── ...
│   ├── Contracts/
│   │   └── IToolModule.cs
│   ├── Models/
│   ├── Helpers/
│   ├── Extensions/
│   ├── Modules/                     # Tool modules (one folder per tool)
│   ├── Config/
│   │   └── appsettings.json
│   ├── Assets/
│   └── Logs/
```

# Module Structure

Each module must follow this layout:

```
Modules/<ToolName>/
- <ToolName>Page.xaml
- <ToolName>Page.xaml.cs
- <ToolName>ViewModel.cs
- <ToolName>Service.cs
```

Example:

```
Modules/JsonFormatter/
- JsonFormatterPage.xaml
- JsonFormatterPage.xaml.cs
- JsonFormatterViewModel.cs
- JsonFormatterService.cs
```

When adding a new module:

1. Create folder under `Modules/`.
2. Register the page and ViewModel in `App.xaml.cs` DI container.
3. Add a `NavigationViewItem` in `MainWindow.xaml`.
4. Add the route in `MainWindow.xaml.cs` `NavigateTo()`.

# MVVM Rules

Use MVVM strictly.

- UI logic belongs in ViewModel.
- UI pages should only contain presentation logic.
- Business logic must be in Services.
- Avoid code-behind unless absolutely necessary.
- Use CommunityToolkit.Mvvm attributes (`[ObservableProperty]`, `[RelayCommand]`) when possible.

For pages that need DI, use constructor injection and expose the ViewModel as a public property:

```csharp
public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }
    public HomePage(HomeViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
```

# Dependency Injection Rules

- Use `Microsoft.Extensions.Hosting` and DI.
- All services must be registered in `App.xaml.cs` `CreateHost()`.
- Do not manually instantiate services if DI can be used.
- The DI container is built in `App.xaml.cs` and the `MainWindow` is resolved from it.
- Pages are resolved via `IServiceProvider.GetRequiredService()` in `MainWindow.NavigateTo()` (not `Frame.Navigate()` which uses parameterless constructors).

# Navigation Rules

- `MainWindow` uses `NavigationView` + `Frame` for page switching.
- Navigation is driven by `Tag` strings on `NavigationViewItem`.
- `ContentFrame.Content = _services.GetRequiredService(pageType)` sets the page directly (DI-friendly).
- The Settings page is handled automatically via `IsSettingsVisible="True"` + `args.IsSettingsInvoked`.

# Theme Rules

- Managed by `ThemeService` (singleton).
- Supports three modes: Default (follow system), Dark, Light.
- User selection is persisted in `ApplicationData.LocalSettings`.
- Apply via `rootElement.RequestedTheme = ...`.
- Theme changes take effect immediately.

# Localization Rules

- Managed by `CultureService` (singleton, `ObservableObject`).
- Strings are stored in C# dictionaries embedded in code (no `.resw` files).
- XAML binds via `{x:Bind ViewModel.Lang.SomeKey, Mode=OneWay}`.
- Language change fires `PropertyChanged(null)` to refresh all bindings immediately — no restart needed.
- Supported languages: en-US (English), zh-CN (简体中文).
- System language is auto-detected on first launch; English is the fallback.

```xml
<TextBlock Text="{x:Bind ViewModel.Lang.HomeTitle, Mode=OneWay}" />
```

# Notification Rules

- Use `Microsoft.Windows.AppNotifications.AppNotificationManager` + `AppNotificationBuilder`.
- Three modes: `None` (silent), `Text` (toast with status), `Preview` (toast + thumbnail).
- Notification mode is persisted in `ApplicationData.LocalSettings["ScreenshotNotificationMode"]`.
- Always call `AppNotificationManager.IsSupported()` before `Register()` or `Show()`.
- Call `AppNotificationManager.Default.Register()` in `MainWindow.OnLoaded()` under try-catch.
- Set `SetScenario(AppNotificationScenario.Urgent)` to bypass Focus Assist silently (no sound).
- For preview images, use `SetAppLogoOverride(Uri)` — `AddInlineImage` is not available in Windows App SDK 2.0.1.
- Notifications depend on package identity (provided by sparse package `IdentityPackage/`).
- Notifications are sent from two paths:
  - Hotkey: `ScreenshotService.TrySendNotification()` (singleton service, always alive).
  - Button: `ScreenshotViewModel.SendNotification()` (transient ViewModel).
- Add an `InfoBar` in the settings page to remind users to enable system notifications when notification mode is not `None`.

# Logging Rules

Use Serilog only.

- All errors and important operations must be logged.
- Log path: `{ApplicationData.Current.LocalFolder.Path}/Logs/` (i.e. `%LOCALAPPDATA%\SaltBox\SaltBox\LocalState\Logs\` when sparse package registered).
- Fallback path (dev without sparse package): `%TEMP%\SaltBox\Logs\`.
- Filename format: `log-yyyyMMdd.txt`.
- Rolling interval: daily, 30 days retention.
- Serilog is initialized in `App` static constructor before the DI host is built.

# Velopack Update Rules

- Add `VelopackApp.Build().Run()` in `App` static constructor before anything else.
- `UpdateService` (singleton) wraps `UpdateManager` for check/download/apply flows.
- Update source URL is configurable via `appsettings.json` → `Update:Url`.
- If no URL is configured, the check button shows "Update source not configured".
- Settings page shows a `SettingsExpander` section with Check/Download/Install buttons based on `UpdateStatus`.
- Use `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` for settings storage (fallback when sparse package not registered).
- The `vpk` CLI tool is required for packaging: `dotnet tool install -g vpk`.
- Package command: `vpk pack --packId SaltBox --packVersion <version> --packDir <publish> --mainExe SaltBox.exe`.

# Sparse Package (Identity Package) Rules

- Identity package (`SaltBox.Identity.msix`) provides package identity for notifications (`AppNotificationManager`), `Package.Current`, and `ApplicationData.Current`.
- Built by `IdentityPackage/BuildIdentityPackage.cmd` using `MakeAppx.exe` + optional `SignTool.exe`.
- Must be signed for production (self-signed cert works if installed to Trusted People store on target machine).
- Must be present next to `SaltBox.exe` in the publish directory.
- The `.csproj` includes it via a conditional `<Content>` reference; CI builds it and copies to `./publish` before `vpk pack`.
- On first run after install, `App.xaml.cs` `RegisterIdentityPackage()` calls `PackageManager.AddPackageByUriAsync()` with `ExternalLocationUri` set to `AppContext.BaseDirectory`.
- Registry fallback (`%TEMP%`) is used when no package identity is detected (try-catch around all `ApplicationData.Current` calls).
- GitHub Actions workflow (`release.yml`) generates a self-signed cert in CI, signs the identity package, and copies it to publish directory.

# UI Rules

Use WinUI 3 controls.

Main window layout:

- NavigationView
- Left navigation panel (Home, separator, Tools header, tool items, Settings at bottom)
- Right content area (Frame)

The `Window.SystemBackdrop` uses `MicaBackdrop` for the modern Windows 11 look.

## Content Layout & Spacing (Content-basics)

Follow the [Content layout and spacing](https://learn.microsoft.com/windows/apps/design/basics/content-basics) specification:

| Value | Usage                                                                   |
| ----- | ----------------------------------------------------------------------- |
| 4epx  | Spacing used for compact sizing.                                        |
| 8epx  | Spacing between Ul controls, control + label.                           |
| 12epx | Spacing between control + header, surface and edge text, text sections. |
| 16epx | Padding used in list styles, cards                                      |
| 24epx | Spacing between content sections.                                       |
| 36epx | Padding on pages.                                                       |
| 48epx | Spacing between page sections with tile.                                |

**Type ramp hierarchy:**

- Title / Subtitle / Body: 12 epx spacing between blocks
- Confined space: use Body Strong for titles (no extra spacing)
- Very confined (command buttons): use Caption

**Settings page layout:**

- SettingsCards spacing = 12 epx between cards
- Page left/right margin = 16 epx
- Group related settings into an expandable group under `CommunityToolkit.WinUI.Controls.SettingsExpander` when a category has 3+ related items, with child controls indented 48 epx
- For simple 1–2 item groups, continue using plain `SettingsCard` in a flat `StackPanel`

# Development Rules

When generating code:

1. Preserve existing comments.
2. Do not rename files unless requested.
3. Do not refactor unrelated code.
4. Only modify necessary parts.
5. Keep code complete and compilable.
6. Prefer minimal changes.
7. Follow existing naming conventions.
8. Keep modules independent.
9. New tools must be added as new modules.
10. Register new modules in `ToolRegistry` and DI container.

# Preferred Coding Style

- Clear class names
- Explicit namespaces
- Simple constructors
- Readable XAML
- No unnecessary abstraction
- Practical over theoretical design
- File-scoped namespaces (`namespace X.Y;`)

# When Adding New Features

Always prefer:
Create a new module instead of expanding unrelated existing modules.

# Example Task Behavior

If asked: "Add a QR code generator"

You should:

1. Create `Modules/QRGenerator/`
2. Add page (`QRGeneratorPage.xaml` / `.cs`)
3. Add ViewModel (`QRGeneratorViewModel.cs`)
4. Add service (`QRGeneratorService.cs`)
5. Register in DI (`App.xaml.cs`)
6. Add NavigationViewItem (`MainWindow.xaml`)
7. Add route (`MainWindow.xaml.cs` `NavigateTo()`)
