using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SaltBox.Config;
using SaltBox.Contracts;
using Serilog;

namespace SaltBox.Services;

public class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string BaseDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SaltBox");

    private static readonly string ConfigDir = Path.Combine(BaseDir, "config");

    private static readonly string DataDir = Path.Combine(BaseDir, "data");
    private static readonly string CacheDir = Path.Combine(BaseDir, "cache");
    private static readonly string TempDir = Path.Combine(BaseDir, "temp");

    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

    private static readonly HashSet<string> CreatedDirs = new();

    public ConfigService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void EnsureDirectories()
    {
        foreach (var dir in new[] { ConfigDir, DataDir, CacheDir, TempDir })
        {
            try
            {
                if (CreatedDirs.Add(dir))
                    Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to create directory {Dir}: {Message}", dir, ex.Message);
            }
        }
    }

    public T Load<T>() where T : ConfigBase, new()
    {
        var filePath = GetFilePath<T>();
        return LoadInternal<T>(filePath);
    }

    public async Task<T> LoadAsync<T>() where T : ConfigBase, new()
    {
        var filePath = GetFilePath<T>();
        return await Task.Run(() => LoadInternal<T>(filePath));
    }

    public void Save<T>(T config) where T : ConfigBase
    {
        config.LastUpdatedUtc = DateTime.UtcNow;
        var filePath = GetFilePath<T>();
        var semaphore = GetFileLock(filePath);
        semaphore.Wait();
        try
        {
            SaveInternal(filePath, config);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task SaveAsync<T>(T config) where T : ConfigBase
    {
        config.LastUpdatedUtc = DateTime.UtcNow;
        var filePath = GetFilePath<T>();
        var semaphore = GetFileLock(filePath);
        await semaphore.WaitAsync();
        try
        {
            await Task.Run(() => SaveInternal(filePath, config));
        }
        finally
        {
            semaphore.Release();
        }
    }

    private T LoadInternal<T>(string filePath) where T : ConfigBase, new()
    {
        if (!File.Exists(filePath))
            return new T();

        T config;
        try
        {
            var json = File.ReadAllText(filePath);
            var deserialized = JsonSerializer.Deserialize<T>(json, JsonOptions);
            if (deserialized is null)
                return RecoverFromCorruption<T>(filePath);

            config = deserialized;
        }
        catch (JsonException)
        {
            return RecoverFromCorruption<T>(filePath);
        }
        catch (IOException)
        {
            return new T();
        }

        config = ApplyMigrations(config);
        return config;
    }

    private T RecoverFromCorruption<T>(string filePath) where T : ConfigBase, new()
    {
        try
        {
            var backupPath = filePath + $".corrupted.{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(filePath, backupPath);
            Log.Warning("Config file corrupted, backed up to {Path}", backupPath);
        }
        catch
        {
        }

        return new T();
    }

    private T ApplyMigrations<T>(T config) where T : ConfigBase
    {
        var migrations = _serviceProvider
            .GetServices<IConfigMigration<T>>()
            .OrderBy(m => m.FromVersion)
            .ToList();

        if (migrations.Count == 0)
            return config;

        var latestVersion = migrations.Max(m => m.ToVersion);
        bool upgraded = false;

        while (config.ConfigVersion < latestVersion)
        {
            var migration = migrations.FirstOrDefault(m => m.FromVersion == config.ConfigVersion);
            if (migration is null)
            {
                Log.Warning("No migration found for {Type} from v{Version}",
                    typeof(T).Name, config.ConfigVersion);
                break;
            }

            config = migration.Migrate(config);
            config.ConfigVersion = migration.ToVersion;
            upgraded = true;
        }

        if (upgraded)
        {
            try
            {
                var filePath = GetFilePath<T>();
                SaveInternal(filePath, config);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to save upgraded config: {Message}", ex.Message);
            }
        }

        return config;
    }

    private static void SaveInternal<T>(string filePath, T config) where T : ConfigBase
    {
        var dir = Path.GetDirectoryName(filePath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        var tempPath = filePath + ".tmp";
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(tempPath, json);

        if (File.Exists(filePath))
        {
            try
            {
                File.Replace(tempPath, filePath, null);
                return;
            }
            catch
            {
            }
        }

        File.Move(tempPath, filePath, overwrite: true);
    }

    private SemaphoreSlim GetFileLock(string filePath)
    {
        return _fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
    }

    private static string GetFilePath<T>() where T : ConfigBase
    {
        var attr = typeof(T).GetCustomAttribute<ConfigFileNameAttribute>(false);
        if (attr is null)
            throw new InvalidOperationException(
                $"Config type {typeof(T).Name} is missing [ConfigFileName] attribute.");

        return Path.Combine(ConfigDir, attr.Name + ".json");
    }
}
