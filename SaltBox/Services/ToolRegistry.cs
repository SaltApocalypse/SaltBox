using SaltBox.Contracts;

namespace SaltBox.Services;

public class ToolRegistry
{
    private readonly List<IToolModule> _modules = new();
    private readonly LogService _log;

    public ToolRegistry(LogService log)
    {
        _log = log;
    }

    public IReadOnlyList<IToolModule> Modules => _modules.AsReadOnly();

    public void Register(IToolModule module)
    {
        _modules.Add(module);
        _log.Info($"Tool registered: {module.Name}");
    }

    public IToolModule? GetModule(string name)
    {
        return _modules.FirstOrDefault(m => m.Name == name);
    }
}
