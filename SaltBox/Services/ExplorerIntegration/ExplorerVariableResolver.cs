namespace SaltBox.Services.ExplorerIntegration;

public class ExplorerVariableResolver
{
    public string Resolve(string template, ExplorerContext context)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        return template
            .Replace("%FullPath%", context.PrimaryPath)
            .Replace("%File%", context.FileName)
            .Replace("%Folder%", context.Folder)
            .Replace("%Name%", context.Name)
            .Replace("%Extension%", context.Extension)
            .Replace("%Parent%", context.Parent)
            .Replace("%Drive%", context.Drive)
            .Replace("%CurrentDirectory%", Environment.CurrentDirectory);
    }
}
