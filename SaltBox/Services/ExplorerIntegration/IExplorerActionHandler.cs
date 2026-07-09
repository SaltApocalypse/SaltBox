namespace SaltBox.Services.ExplorerIntegration;

public interface IExplorerActionHandler
{
    string ActionId { get; }
    ExplorerTarget Target { get; }
    string DisplayName { get; }
    bool IsEnabled { get; }
    void Execute(ExplorerContext context);
}
