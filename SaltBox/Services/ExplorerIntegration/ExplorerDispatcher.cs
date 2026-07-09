using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SaltBox.Contracts;

namespace SaltBox.Services.ExplorerIntegration;

public class ExplorerDispatcher
{
    private readonly LogService _log;
    private readonly IServiceProvider _services;
    private readonly IConfigService _configService;
    private readonly ExplorerVariableResolver _resolver;

    public ExplorerDispatcher(LogService log, IServiceProvider services, IConfigService configService, ExplorerVariableResolver resolver)
    {
        _log = log;
        _services = services;
        _configService = configService;
        _resolver = resolver;
    }

    public bool Dispatch(string[] cmdArgs)
    {
        for (int i = 0; i < cmdArgs.Length; i++)
        {
            if (string.Equals(cmdArgs[i], "--extract-files", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdArgs.Length)
            {
                var context = new ExplorerContext
                {
                    ActionId = "SaltBox.BA.FileExtractor",
                    Target = ExplorerTarget.Directory,
                    Paths = new[] { cmdArgs[i + 1] },
                };
                return DispatchToHandler(context);
            }

            if (string.Equals(cmdArgs[i], "--saltbox-action", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdArgs.Length)
            {
                var actionId = cmdArgs[i + 1];
                var paths = new List<string>();

                for (int j = i + 2; j < cmdArgs.Length; j++)
                {
                    if (cmdArgs[j] == "--paths" && j + 1 < cmdArgs.Length)
                    {
                        j++;
                        while (j < cmdArgs.Length && !cmdArgs[j].StartsWith("--"))
                        {
                            paths.Add(cmdArgs[j]);
                            j++;
                        }
                        break;
                    }
                }

                var context = new ExplorerContext
                {
                    ActionId = actionId,
                    Target = ExplorerTarget.Directory,
                    Paths = paths,
                };
                return DispatchToHandler(context);
            }
        }

        return false;
    }

    private bool DispatchToHandler(ExplorerContext context)
    {
        var handlers = _services.GetRequiredService<IEnumerable<IExplorerActionHandler>>();
        foreach (var handler in handlers)
        {
            if (string.Equals(handler.ActionId, context.ActionId, StringComparison.OrdinalIgnoreCase))
            {
                _log.Info($"Dispatching Explorer action: {context.ActionId} with {context.Paths.Count} path(s)");
                handler.Execute(context);
                return true;
            }
        }

        var config = _configService.Load<ExplorerActionConfig>();
        var customAction = config.CustomActions.FirstOrDefault(a =>
            string.Equals(a.Id, context.ActionId, StringComparison.OrdinalIgnoreCase) && a.IsEnabled);

        if (customAction != null)
        {
            _log.Info($"Dispatching custom Explorer action: {context.ActionId}");
            var resolvedArgs = _resolver.Resolve(customAction.Arguments, context);
            var resolvedWorkDir = string.IsNullOrEmpty(customAction.WorkDirectory)
                ? Environment.CurrentDirectory
                : _resolver.Resolve(customAction.WorkDirectory, context);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = customAction.CommandPath,
                    Arguments = resolvedArgs,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = resolvedWorkDir,
                };
                Process.Start(psi);
                _log.Info($"Custom action executed: {customAction.Id} ({customAction.CommandPath})");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to execute custom action '{customAction.Id}': {ex.Message}");
                return false;
            }
        }

        _log.Warn($"No handler found for Explorer action: {context.ActionId}");
        return false;
    }
}
