You are assisting in the development of a Windows desktop application project named SaltBox.

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
├── SaltBox/                         # Main WinUI 3 project
│   ├── SaltBox.csproj
│   ├── App.xaml / .cs               # DI host + Serilog setup
│   ├── MainWindow.xaml / .cs        # NavigationView shell
│   ├── app.manifest
│   ├── Views/                       # Pages (HomePage, SettingsPage, ScreenshotPage, ...)
│   ├── ViewModels/                  # MVVM ViewModels
│   ├── Services/                    # Application services
│   │   ├── LogService.cs
│   │   ├── ToolRegistry.cs
│   │   ├── ThemeService.cs
│   │   └── CultureService.cs
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
└── SaltBox (Package)/               # MSIX packaging project (.wapproj)
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

# Logging Rules

Use Serilog only.

- All errors and important operations must be logged.
- Log path: `Logs/` (relative to app base directory).
- Filename format: `log-yyyyMMdd.txt`.
- Rolling interval: daily, 30 days retention.
- Serilog is initialized in `App` static constructor before the DI host is built.

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
