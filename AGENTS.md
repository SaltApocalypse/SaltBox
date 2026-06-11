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
├── AGENTS.md
├── Dev.md
├── README.md
├── IdentityPackage/                 # Sparse package for package identity
│   ├── AppxManifest.xml
│   └── BuildIdentityPackage.cmd
├── publish/                         # Published output (after dotnet publish)
├── Releases/                        # Velopack release artifacts
├── SaltBox/                         # Main WinUI 3 project (unpackaged)
│   ├── SaltBox.csproj
│   ├── App.xaml / .cs               # DI host + Serilog + Velopack setup
│   ├── MainWindow.xaml / .cs        # NavigationView shell
│   ├── app.manifest                 # Contains msix element for identity binding
│   ├── Views/                       # App-level pages (HomePage, SettingsPage, ...)
│   ├── ViewModels/                  # App-level ViewModels (Home, Settings, Main)
│   ├── Services/                    # Shared application services
│   │   ├── LogService.cs
│   │   ├── ToolRegistry.cs
│   │   ├── ThemeService.cs
│   │   ├── CultureService.cs
│   │   ├── TrayService.cs
│   │   ├── UpdateService.cs
│   │   ├── ShortcutRegistry.cs
│   │   ├── ContextMenuManager.cs
│   │   ├── ConfigService.cs         # JSON config store
│   │   └── InMemoryLogSink.cs
│   ├── Contracts/
│   │   ├── IToolModule.cs
│   │   ├── IConfigService.cs        # Config store interface
│   │   ├── IConfigMigration.cs      # Config upgrade interface
│   │   └── ConfigFileNameAttribute.cs
│   ├── Models/
│   ├── Config/                      # Configuration data
│   │   ├── appsettings.json         # Build-time app settings (Update URL, etc.)
│   │   ├── ConfigBase.cs            # Abstract base (ConfigVersion + LastUpdatedUtc)
│   │   └── AppConfig.cs             # Global settings (Theme, Language, Version)
│   ├── Helpers/
│   ├── Extensions/                  # Available for future use
│   ├── Controls/                    # Custom controls (e.g. KeyCap)
│   ├── Converters/                  # Value converters
│   ├── Modules/                     # Tool modules (one folder per tool)
│   │   ├── FileExtractor/
│   │   │   ├── FileExtractorPage.xaml / .cs
│   │   │   ├── FileExtractorViewModel.cs
│   │   │   ├── FileExtractorService.cs
│   │   │   └── FileExtractorConfig.cs
│   │   ├── Screenshot/
│   │   │   ├── ScreenshotPage.xaml / .cs
│   │   │   ├── ScreenshotViewModel.cs
│   │   │   ├── ScreenshotService.cs
│   │   │   └── ScreenshotConfig.cs
│   │   └── DeveloperMode/
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

Modules with persisted settings also include a `*Config.cs` file (see [Configuration System](#configuration-system)).

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
- User selection is persisted in `AppConfig` via `IConfigService`.
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

## i18n Key Naming Convention

- **Feature-specific keys**: prefix with feature name — `ScreenshotXXX`, `FileExtractorXXX`, `SettingsXXX`, `DevXXX`, `HomeXXX`, `NavXXX`.
- **Reusable generic keys**: prefix with `Common` — `CommonOn`, `CommonOff`, `CommonConfig`, `CommonCopy`, `CommonSave`, `CommonBrowse`, `CommonNotificationSystemHint`.
- Do NOT create `Common` keys for strings that carry feature-specific semantics or may diverge in translation.
- Do NOT use meaningless names like `Label1`, `Button1`, `Text1`.

# Notification Rules

- Use `Microsoft.Windows.AppNotifications.AppNotificationManager` + `AppNotificationBuilder`.
- Three modes: `None` (silent), `Text` (toast with status), `Preview` (toast + thumbnail).
- Notification mode is persisted in `ScreenshotConfig.NotificationMode` via `IConfigService`.
- Always call `AppNotificationManager.IsSupported()` before `Register()` or `Show()`.
- Call `AppNotificationManager.Default.Register()` in `MainWindow.OnLoaded()` under try-catch.
- Set `SetScenario(AppNotificationScenario.Urgent)` to bypass Focus Assist silently (no sound).
- For preview images, use `SetAppLogoOverride(Uri)` — `AddInlineImage` is not available in Windows App SDK 2.0.1.
- Notifications depend on package identity (provided by sparse package `IdentityPackage/`).
- Notifications are sent from two paths:
  - Hotkey: `ScreenshotService.TrySendNotification()` (singleton service, always alive).
  - Button: `ScreenshotViewModel.SendNotification()` (transient ViewModel).
- Add an `InfoBar` in the settings page to remind users to enable system notifications when notification mode is not `None`.

# Configuration System

SaltBox 使用基于 JSON 文件的模块化配置系统，不依赖 `ApplicationData.Current.LocalSettings`。

## 架构概览

```
IConfigService (Contracts/IConfigService.cs)
  └─ ConfigService (Services/ConfigService.cs)
       ├─ 泛型 LoadAsync<T>() / SaveAsync<T>()
       ├─ [ConfigFileName] 特性 → 文件名（与类型解耦）
       ├─ ConfigBase 基类：ConfigVersion + LastUpdatedUtc
       ├─ 原子写入：File.Replace → File.Move 回退
       ├─ 损坏恢复：备份 → 返回默认
       ├─ 线程安全：SemaphoreSlim 逐文件锁 (Save 独占)
       └─ 自动升级链：IConfigMigration<T>
```

## 配置目录结构

```
%LOCALAPPDATA%\SaltBox\
├── config/     ← 所有 JSON 配置（IConfigService 写入）
│   ├── app.json
│   ├── screenshot.json
│   └── fileextractor.json
├── data/       ← 模块用户数据（IConfigService 不碰）
├── cache/      ← 预留
├── logs/       ← Serilog 日志
└── temp/       ← 预留
```

首次启动自动创建所有目录。

## 配置模型规则

每个模块拥有自己的 `*Config.cs`，继承 `ConfigBase`，标注 `[ConfigFileName]`。

```csharp
[ConfigFileName("screenshot")]
public class ScreenshotConfig : ConfigBase
{
    public bool IsEnabled { get; set; } = true;
    public int NotificationMode { get; set; } = 1;
    // ...
}
```

- `ConfigBase` 提供 `ConfigVersion`（升级用）和 `LastUpdatedUtc`（自动更新）。
- 文件名通过 `[ConfigFileName]` 特性标注，不依赖类型名。
- 模块不存在时不产生对应配置文件（ConfigService 是被动的，只响应调用）。

## 接口

```csharp
public interface IConfigService
{
    Task<T> LoadAsync<T>() where T : ConfigBase, new();
    Task SaveAsync<T>(T config) where T : ConfigBase;
    T Load<T>() where T : ConfigBase, new();    // 同步包装
    void Save<T>(T config) where T : ConfigBase; // 同步包装
}
```

## 升级扩展点

```csharp
public interface IConfigMigration<T> where T : ConfigBase
{
    int FromVersion { get; }
    int ToVersion { get; }
    T Migrate(T config);
}
```

在 DI 中注册后，`ConfigService.LoadAsync` 自动链式调用迁移。

## 使用规则

1. 所有模块配置通过 `IConfigService` 读写，不可直接操作文件。
2. 配置模型放在模块目录（`Modules/<ToolName>/<ToolName>Config.cs`）。
3. 全局配置 (`AppConfig`) 放在 `Config/AppConfig.cs`。
4. 首次使用配置的模块在构造函数中 `_config = _configService.Load<T>()`。
5. 每次属性变更时更新 `_config` 并调用 `Save()`。
6. 配置模型使用 `int` 而非 `enum` 存储，ViewModel 层做 int ↔ enum 转换。
7. 新增配置模型必须标注 `[ConfigFileName("...")]`。
8. 新增配置模型在 `App.xaml.cs` 中无需额外注册（ConfigService 通过泛型调用自动发现）。

# Logging Guidelines

## Core Rules

- Use Serilog exclusively — no other logging frameworks.
- All services MUST use the injected `LogService` (`private readonly LogService _log`).
- Static helper classes may use `Serilog.Log.ForContext<T>()` or `Serilog.Log` directly only when DI injection is impractical.
- Logging must be initialized in `App` static constructor before the DI host is built (see `App.xaml.cs` `InitLogging()`).
- All errors and important operations must be logged.
- No temporary diagnostic logs may be committed — cleanup all investigation logs after feature completion.

## Log Level Usage

### Information — 用户可感知的行为

Use for:
- Application lifecycle: started, shut down
- User-initiated operations: capture, shortcut change, update triggered
- Feature completion: screenshot saved, update applied
- Status changes: theme changed, hotkey registered

Examples:
- `"Application started"`
- `"Hotkey triggered — capturing {display} to {path}"`
- `"[Capture:{captureId}] FramePool created"`
- `"[HDR:{captureId}] HDR display detected"`
- `"Screenshot saved: {path}"`
- `"Global hotkey registered (modifier={mod}, key={key})"`
- `"MainWindow loaded"`

### Warning — 可恢复的异常和自动降级

Use for:
- Fallback paths activated: hotkey fallback from RegisterHotKey → low-level hook, D3D → GDI
- Feature unavailable but app continues: notification unsupported, identity package missing
- Configuration anomalies: setting load failed, update source not configured

Examples:
- `"RegisterHotKey failed, falling back to low-level hook"`
- `"[D3D:{captureId}] D3D capture failed ({message})"`
- `"Notification show failed: {message}"`
- `"Failed to load setting: {key}"`

### Debug — 开发调试信息

Use for:
- COM/WinRT object creation and ABI bridging details
- D3D/DXGI device creation process
- Graphics pipeline object lifecycle: FramePool, Session, Frame
- Update flow diagnostics: version detection, check cycle, download status
- Intermediate states and performance timing

Examples:
- `"[D3D:{captureId}] Creating Hardware device"`
- `"[DXGI:{captureId}] IDXGIDevice acquired"`
- `"[Capture:{captureId}] Waiting for frame"`
- `"CheckForUpdatesAsync: update available: Current {v} -> Latest {v}"`

### Error — 功能失败和未处理异常

Use for:
- Operations that fail entirely: capture failure, update failure, notification failure
- Unhandled exceptions caught at boundary
- COM/WinRT interop failures

Examples:
- `"Hotkey capture failed: {message}"`
- `"CreateForMonitor FAILED"`
- `"Update download failed: {message}"`
- `"Screenshot capture threw: {detail}"`

## Log Content Principles

1. **面向用户的日志使用 Information** — 用户在日志文件中应能看到有意义的活动记录。
2. **面向开发者的诊断日志使用 Debug** — 排查问题时的第一信息来源，默认不显示给用户。
3. **不要输出原始指针地址** — 如 `ptr=0x...` 无实际排查价值。
4. **不要输出托管对象类型全名和程序集信息** — 如 `Device Type=...`、`Device Assembly=...`。
5. **不要在日志中泄露安全敏感信息** — 如注册表值、文件路径含用户名等（但应用自身的路径可以）。
6. **上下文标签优先于全量信息** — 使用 `[Component:OperationId] message` 格式（如 `[Capture:ABC12345] FramePool created` 而非 `Direct3D11CaptureFramePool created with device argument`）。
7. **每条日志应包含足够的上下文** — 对于捕获操作，带上 `captureId`；对于更新操作，带上 `CurrentVersion`/`LatestVersion`。
8. **日志不应包含换行符** — 保持单行输出，便于 grep。

## 日志路径

- Primary: `{ApplicationData.Current.LocalFolder.Path}/Logs/` (i.e. `%LOCALAPPDATA%\SaltBox\SaltBox\LocalState\Logs\` when sparse package registered).
- Fallback (dev without sparse package): `%TEMP%\SaltBox\Logs\`.
- Filename format: `log-yyyyMMdd.txt`.
- Rolling interval: daily, 30 days retention.

## 开发注意事项

- 在添加新功能时，优先使用正确的日志等级（不是所有日志都是 Information）。
- 功能完成后，必须审查并清理排查阶段添加的临时日志（标记所有临时日志为 TODO 或删除）。
- 在提交 PR 前，使用 `git diff` 检查是否还有临时的 `Log.Debug("testing..."`) 类日志。
- 对于复杂功能（如截图），使用 `captureId` 作为上下文标签以关联同一操作的所有日志。

# Velopack Update Rules

- Add `VelopackApp.Build().Run()` in `App` static constructor before anything else.
- `UpdateService` (singleton) wraps `UpdateManager` for check/download/apply flows.
- Update source URL is configurable via `appsettings.json` → `Update:Url`.
- If no URL is configured, the check button shows "Update source not configured".
- Settings page shows a `SettingsExpander` section with Check/Download/Install buttons based on `UpdateStatus`.
- Use `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` for settings storage (the same base path as the JSON config system).
- The `vpk` CLI tool is required for packaging: `dotnet tool install -g vpk`.
- Package command: `vpk pack --packId SaltBox --packVersion <version> --packDir <publish> --mainExe SaltBox.exe`.

# Sparse Package (Identity Package) Rules

- Identity package (`SaltBox.Identity.msix`) provides package identity for notifications (`AppNotificationManager`) and `Package.Current`.
- **Settings persistence no longer depends on `ApplicationData.Current`** — all settings use `IConfigService` with JSON files in `%LOCALAPPDATA%\SaltBox\config\`.
- Built by `IdentityPackage/BuildIdentityPackage.cmd` using `MakeAppx.exe` + optional `SignTool.exe`.
- Must be signed for production (self-signed cert works if installed to Trusted People store on target machine).
- Must be present next to `SaltBox.exe` in the publish directory.
- The `.csproj` includes it via a conditional `<Content>` reference; CI builds it and copies to `./publish` before `vpk pack`.
- On first run after install, `RegisterIdentityPackage()` is called via `_ = Task.Run(() => RegisterIdentityPackage())` in `App.xaml.cs` `OnLaunched()`, not in the static constructor's `OnFirstRun` callback — this avoids deadlocks and ensures Serilog is initialized.
- Registry fallback (`%TEMP%`) is used when no package identity is detected (try-catch around all `ApplicationData.Current` calls).
- GitHub Actions workflow (`release.yml`) generates a self-signed cert in CI, signs the identity package, and copies it to publish directory.

# Publish Rules

- Always set `PublishTrimmed` to `false` for WinUI 3 — trimming removes necessary WinRT metadata and causes silent startup failure.
- Use `WindowsAppSDKSelfContained = true` to bundle the WinAppSDK runtime (no MSIX dependency).
- Target `win-x64` for releases.
- Run `dotnet publish -c Release -r win-x64 --self-contained -o ./publish` before `vpk pack`.
- Always test `./publish/SaltBox.exe` directly on a clean machine before packaging with Velopack.

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
