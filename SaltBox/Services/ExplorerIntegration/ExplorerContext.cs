namespace SaltBox.Services.ExplorerIntegration;

public class ExplorerContext
{
    public string ActionId { get; init; } = "";
    public ExplorerTarget Target { get; init; }
    public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();

    public string PrimaryPath => Paths.Count > 0 ? Paths[0] : "";
    public string FileName => Path.GetFileName(PrimaryPath);
    public string Folder => Path.GetDirectoryName(PrimaryPath) ?? "";
    public string Extension => Path.GetExtension(PrimaryPath);
    public string Name => Path.GetFileNameWithoutExtension(PrimaryPath);
    public string Parent => Path.GetFileName(Path.GetDirectoryName(PrimaryPath) ?? "");
    public string Drive => Path.GetPathRoot(PrimaryPath) ?? "";
}
