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

            var subKeyPath = $@"{regPath}\shell\{actionId}";
            using (var itemKey = Registry.CurrentUser.CreateSubKey(subKeyPath))
            {
                if (itemKey == null)
                {
                    _log.Error($"Failed to create sub-item key for {actionId}");
                    return;
                }
                itemKey.SetValue("MUIVerb", displayName);
            }

            var cmdKeyPath = $@"{subKeyPath}\command";
            using (var cmdKey = Registry.CurrentUser.CreateSubKey(cmdKeyPath))
            {
                if (cmdKey == null)
                {
                    _log.Error($"Failed to create command key for {actionId}");
                    return;
                }
                cmdKey.SetValue("", commandLine);
            }

            _log.Info($"Registered Explorer action: {actionId} (target={target})");
            NotifyShellRefresh();
        }
    }

    public void UnregisterAction(ExplorerTarget target, string actionId)
    {
        lock (_lock)
        {
            var regPath = GetRegPath(target);
            var subKeyPath = $@"{regPath}\shell\{actionId}";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, false);
                _log.Info($"Unregistered Explorer action: {actionId}");
            }
            catch (ArgumentException) { }

            var shellPath = $@"{regPath}\shell";
            try
            {
                using var shellKey = Registry.CurrentUser.OpenSubKey(shellPath);
                if (shellKey == null || shellKey.GetSubKeyNames().Length == 0)
                {
                    RemoveParentKey(regPath);
                }
            }
            catch
            {
                RemoveParentKey(regPath);
            }

            NotifyShellRefresh();
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

            foreach (var target in targets)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(target, false);
                }
                catch (ArgumentException) { }
            }

            NotifyShellRefresh();
            _log.Info("Cleaned up all SaltBox Explorer menu entries");
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
