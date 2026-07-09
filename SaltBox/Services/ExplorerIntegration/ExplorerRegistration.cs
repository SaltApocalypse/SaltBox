using Microsoft.Win32;
using System.Runtime.InteropServices;
using SaltBox.Services;

namespace SaltBox.Services.ExplorerIntegration;

public class ExplorerRegistration
{
    private readonly LogService _log;
    private readonly object _lock = new();

    public ExplorerRegistration(LogService log)
    {
        _log = log;
    }

    private static string GetRegPath(ExplorerTarget target) => target switch
    {
        ExplorerTarget.File => @"Software\Classes\*\shell\SaltBox",
        ExplorerTarget.Directory => @"Software\Classes\Directory\shell\SaltBox",
        _ => throw new ArgumentException($"Unsupported target: {target}")
    };

    public void RegisterAction(ExplorerTarget target, string actionId, string displayName, string commandLine)
    {
        lock (_lock)
        {
            var regPath = GetRegPath(target);
            EnsureParentMenu(regPath);
            WriteActionKey($@"{regPath}\shell\{actionId}", displayName, commandLine);
            NotifyShellRefresh();
            _log.Info($"Registered Explorer action: {actionId} (target={target})");
        }
    }

    public void UnregisterAction(ExplorerTarget target, string actionId, string? parentId = null)
    {
        lock (_lock)
        {
            var regPath = GetRegPath(target);
            var parentPrefix = parentId != null ? $@"\shell\{parentId}" : "";
            var subKeyPath = $@"{regPath}{parentPrefix}\shell\{actionId}";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, false);
                _log.Info($"Unregistered Explorer action: {actionId}");
            }
            catch (ArgumentException) { }

            TryRemoveParentIfEmpty($@"{regPath}{parentPrefix}");
            NotifyShellRefresh();
        }
    }

    public void RegisterGroup(ExplorerTarget target, string groupId, string displayName)
    {
        lock (_lock)
        {
            var regPath = GetRegPath(target);
            EnsureParentMenu(regPath);

            var groupPath = $@"{regPath}\shell\{groupId}";
            using (var key = Registry.CurrentUser.CreateSubKey(groupPath))
            {
                if (key == null)
                {
                    _log.Error($"Failed to create group key: {groupPath}");
                    return;
                }
                key.SetValue("MUIVerb", displayName);
                key.SetValue("SubCommands", "", RegistryValueKind.String);
            }
            NotifyShellRefresh();
            _log.Info($"Registered group: {groupId} (target={target})");
        }
    }

    public void UnregisterGroup(ExplorerTarget target, string groupId)
    {
        lock (_lock)
        {
            var regPath = GetRegPath(target);
            var groupPath = $@"{regPath}\shell\{groupId}";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(groupPath, false);
                _log.Info($"Unregistered group: {groupId}");
            }
            catch (ArgumentException) { }

            TryRemoveParentIfEmpty(regPath);
            NotifyShellRefresh();
        }
    }

    public void RegisterActionInGroup(ExplorerTarget target, string groupId, string actionId, string displayName, string commandLine)
    {
        lock (_lock)
        {
            var regPath = GetRegPath(target);
            WriteActionKey($@"{regPath}\shell\{groupId}\shell\{actionId}", displayName, commandLine);
            NotifyShellRefresh();
            _log.Info($"Registered action in group: {actionId} -> {groupId} (target={target})");
        }
    }

    public void CleanupAll()
    {
        lock (_lock)
        {
            string[] targets =
            [
                @"Software\Classes\*\shell\SaltBox",
                @"Software\Classes\Directory\shell\SaltBox",
                @"Software\Classes\Directory\Background\shell\SaltBox",
                @"Software\Classes\Drive\shell\SaltBox",
            ];

            foreach (var t in targets)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(t, false);
                }
                catch (ArgumentException) { }
            }

            NotifyShellRefresh();
            _log.Info("Cleaned up all SaltBox Explorer menu entries");
        }
    }

    private void EnsureParentMenu(string regPath)
    {
        using (var parentKey = Registry.CurrentUser.CreateSubKey(regPath))
        {
            if (parentKey == null)
            {
                _log.Error($"Failed to create parent menu key: {regPath}");
                return;
            }
            parentKey.SetValue("MUIVerb", "SaltBox");
            var exePath = Environment.ProcessPath ?? "";
            parentKey.SetValue("Icon", exePath);
            parentKey.SetValue("SubCommands", "", RegistryValueKind.String);
        }
    }

    private static void WriteActionKey(string itemPath, string displayName, string commandLine)
    {
        using (var itemKey = Registry.CurrentUser.CreateSubKey(itemPath))
        {
            if (itemKey == null)
                return;
            itemKey.SetValue("MUIVerb", displayName);
        }

        var cmdPath = $@"{itemPath}\command";
        using (var cmdKey = Registry.CurrentUser.CreateSubKey(cmdPath))
        {
            if (cmdKey == null)
                return;
            cmdKey.SetValue("", commandLine);
        }
    }

    private void TryRemoveParentIfEmpty(string basePath)
    {
        var shellPath = $@"{basePath}\shell";
        try
        {
            using var shellKey = Registry.CurrentUser.OpenSubKey(shellPath);
            if (shellKey == null || shellKey.GetSubKeyNames().Length == 0)
            {
                RemoveParentKey(basePath);
            }
        }
        catch
        {
            RemoveParentKey(basePath);
        }
    }

    private void RemoveParentKey(string regPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(regPath, false);
            _log.Info($"Removed empty parent menu: {regPath}");
        }
        catch (ArgumentException) { }
    }

    private static void NotifyShellRefresh()
    {
        try
        {
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, nint dwItem1, nint dwItem2);
}
