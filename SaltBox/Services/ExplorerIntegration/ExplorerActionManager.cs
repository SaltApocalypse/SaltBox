using SaltBox.Contracts;

namespace SaltBox.Services.ExplorerIntegration;

public class ExplorerActionManager
{
    private const string CustomActionsGroupId = "SaltBox.BB";
    private const string CustomActionsGroupDisplayName = "自定义处理程序";

    private static readonly ExplorerTarget[] GroupTargets = { ExplorerTarget.File, ExplorerTarget.Directory };

    private readonly ExplorerRegistration _registration;
    private readonly IConfigService _configService;
    private readonly LogService _log;
    private readonly IEnumerable<IExplorerActionHandler> _handlers;
    private ExplorerActionConfig _config;

    public ExplorerActionManager(
        ExplorerRegistration registration,
        IConfigService configService,
        LogService log,
        IEnumerable<IExplorerActionHandler> handlers)
    {
        _registration = registration;
        _configService = configService;
        _log = log;
        _handlers = handlers;
        _config = configService.Load<ExplorerActionConfig>();
    }

    public void Initialize()
    {
        _registration.CleanupAll();

        foreach (var handler in _handlers)
        {
            if (handler.IsEnabled)
                RegisterHandlerAction(handler);
        }

        RebuildCustomActionsGroup();

        _log.Info("ExplorerActionManager initialized");
    }

    public void RefreshHandler(IExplorerActionHandler handler)
    {
        _registration.UnregisterAction(handler.Target, handler.ActionId);
        if (handler.IsEnabled)
            RegisterHandlerAction(handler);
    }

    public bool IsCustomActionsEnabled => _config.IsEnabled;

    public void SetCustomActionsEnabled(bool enabled)
    {
        _config.IsEnabled = enabled;
        SaveConfig();
        RebuildCustomActionsGroup();
        _log.Info(enabled ? "Custom Explorer actions enabled" : "Custom Explorer actions disabled");
    }

    public IReadOnlyList<ExplorerActionItem> GetCustomActions() => _config.CustomActions;

    public void AddCustomAction(ExplorerActionItem item)
    {
        _config.CustomActions.Add(item);
        SaveConfig();
        RebuildCustomActionsGroup();
        _log.Info($"Added custom Explorer action: {item.Id}");
    }

    public void RemoveCustomAction(string id)
    {
        _config.CustomActions.RemoveAll(a => a.Id == id);
        SaveConfig();
        RebuildCustomActionsGroup();
        _log.Info($"Removed custom Explorer action: {id}");
    }

    public void RefreshCustomAction(ExplorerActionItem item)
    {
        var idx = _config.CustomActions.FindIndex(a => a.Id == item.Id);
        if (idx >= 0)
        {
            _config.CustomActions[idx] = item;
            SaveConfig();
        }
        RebuildCustomActionsGroup();
    }

    private void RebuildCustomActionsGroup()
    {
        var enabledActions = _config.CustomActions
            .Where(a => a.IsEnabled)
            .ToList();

        foreach (var target in GroupTargets)
        {
            _registration.UnregisterGroup(target, CustomActionsGroupId);

            if (!_config.IsEnabled)
                continue;

            if (enabledActions.Count == 0)
                continue;

            _registration.RegisterGroup(target, CustomActionsGroupId, CustomActionsGroupDisplayName);

            foreach (var item in enabledActions)
                RegisterCustomAction(item, target);
        }
    }

    private void RegisterHandlerAction(IExplorerActionHandler handler)
    {
        var exePath = Environment.ProcessPath ?? "";
        var cmd = $"\"{exePath}\" --saltbox-action {handler.ActionId} --paths \"%1\"";
        _registration.RegisterAction(handler.Target, handler.ActionId, handler.DisplayName, cmd);
    }

    private void RegisterCustomAction(ExplorerActionItem item, ExplorerTarget target)
    {
        var exePath = Environment.ProcessPath ?? "";
        var cmd = $"\"{exePath}\" --saltbox-action {item.Id} --paths \"%1\"";
        _registration.RegisterActionInGroup(target, CustomActionsGroupId, item.Id, item.DisplayName, cmd);
    }

    private void SaveConfig()
    {
        try
        {
            _configService.Save(_config);
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to save ExplorerAction config: {ex.Message}");
        }
    }
}
