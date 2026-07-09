using SaltBox.Config;
using SaltBox.Contracts;

namespace SaltBox.Services.ExplorerIntegration;

[ConfigFileName("explorer-actions")]
public class ExplorerActionConfig : ConfigBase
{
    public List<ExplorerActionItem> CustomActions { get; set; } = new();
}

public class ExplorerActionItem
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public ExplorerTarget Target { get; set; }
    public string CommandPath { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string? WorkDirectory { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
}
