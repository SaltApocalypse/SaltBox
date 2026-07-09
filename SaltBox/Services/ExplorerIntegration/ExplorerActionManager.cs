using SaltBox.Contracts;

namespace SaltBox.Services.ExplorerIntegration;

public class ExplorerActionManager
{
    private readonly ExplorerRegistration _registration;
    private readonly IConfigService _configService;
    private readonly LogService _log;
    private readonly IEnumerable<IExplorerActionHandler> _handlers;

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
    }

    public void Initialize()
    {
        _registration.CleanupAll();

        foreach (var handler in _handlers)
        {
            if (handler.IsEnabled)
                RegisterHandlerAction(handler);
        }

        _log.Info("ExplorerActionManager initialized");
    }

    public void RefreshHandler(IExplorerActionHandler handler)
    {
        _registration.UnregisterAction(handler.Target, handler.ActionId);
        if (handler.IsEnabled)
            RegisterHandlerAction(handler);
    }

    private void RegisterHandlerAction(IExplorerActionHandler handler)
    {
        var exePath = Environment.ProcessPath ?? "";
        var cmd = $"\"{exePath}\" --saltbox-action {handler.ActionId} --paths \"%1\"";
        _registration.RegisterAction(handler.Target, handler.ActionId, handler.DisplayName, cmd);
    }
}
