using Microsoft.Extensions.DependencyInjection;

namespace SaltBox.Services.ExplorerIntegration;

public class ExplorerDispatcher
{
    private readonly LogService _log;
    private readonly IServiceProvider _services;

    public ExplorerDispatcher(LogService log, IServiceProvider services)
    {
        _log = log;
        _services = services;
    }

    public bool Dispatch(string[] cmdArgs)
    {
        for (int i = 0; i < cmdArgs.Length; i++)
        {
            if (string.Equals(cmdArgs[i], "--extract-files", StringComparison.OrdinalIgnoreCase) && i + 1 < cmdArgs.Length)
            {
                var context = new ExplorerContext
                {
                    ActionId = "SaltBox.FileExtractor",
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

        _log.Warn($"No handler found for Explorer action: {context.ActionId}");
        return false;
    }
}
