using Microsoft.Win32;
using Serilog;
using System.Runtime.InteropServices;

namespace SaltBox.Services;

public class ContextMenuManager
{
    private const string RegRoot = @"Software\Classes\Directory\shell\SaltBox";
    private const string RegShell = RegRoot + @"\shell";

    private readonly LogService _log;
    private readonly HashSet<string> _registeredItems = new();
    private readonly object _lock = new();

    public ContextMenuManager(LogService log)
    {
        _log = log;
    }

    public void Cleanup()
    {
        lock (_lock)
        {
            _registeredItems.Clear();

            string[] targets =
            [
                RegRoot,
                @"Software\Classes\Directory\shell\SaltBoxFileExtractor",
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
            _log.Info("Cleaned up all SaltBox context menu entries");
        }
    }

    public void RegisterSubItem(string toolId, string displayName, string commandLine)
    {
        lock (_lock)
        {
            var exePath = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exePath))
            {
                _log.Error("Cannot register sub-item: unable to determine executable path");
                return;
            }

            if (_registeredItems.Count == 0)
            {
                using var parentKey = Registry.CurrentUser.CreateSubKey(RegRoot);
                if (parentKey == null)
                {
                    _log.Error("Failed to create parent menu key");
                    return;
                }
                parentKey.SetValue("MUIVerb", "SaltBox");
                parentKey.SetValue("Icon", exePath);
                parentKey.SetValue("SubCommands", "", RegistryValueKind.String);
                parentKey.Flush();
                _log.Info("Registered parent menu: SaltBox");
            }

            var subKey = $@"{RegShell}\{toolId}";
            using (var itemKey = Registry.CurrentUser.CreateSubKey(subKey))
            {
                if (itemKey == null)
                {
                    _log.Error($"Failed to create sub-item key for {toolId}");
                    return;
                }
                itemKey.SetValue("MUIVerb", displayName);
                itemKey.Flush();
            }

            var cmdKey = $@"{subKey}\command";
            using (var cmdRegKey = Registry.CurrentUser.CreateSubKey(cmdKey))
            {
                if (cmdRegKey == null)
                {
                    _log.Error($"Failed to create command key for {toolId}");
                    return;
                }
                cmdRegKey.SetValue("", commandLine);
                cmdRegKey.Flush();
            }

            _registeredItems.Add(toolId);
            _log.Info($"Registered context menu sub-item: {toolId}");
            NotifyShellRefresh();
        }
    }

    public void UnregisterSubItem(string toolId)
    {
        lock (_lock)
        {
            var subKey = $@"{RegShell}\{toolId}";
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(subKey, false);
                _log.Info($"Removed context menu sub-item: {toolId}");
            }
            catch (ArgumentException) { }

            _registeredItems.Remove(toolId);

            if (_registeredItems.Count == 0)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(RegRoot, false);
                    _log.Info("No remaining sub-items, removed SaltBox parent menu");
                }
                catch (ArgumentException) { }
            }

            NotifyShellRefresh();
        }
    }

    private static void NotifyShellRefresh()
    {
        try
        {
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Log.Warning("Shell refresh notification failed: {Message}", ex.Message);
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, nint dwItem1, nint dwItem2);
}
