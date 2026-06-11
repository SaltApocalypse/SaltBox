using SaltBox.Config;

namespace SaltBox.Contracts;

public interface IConfigService
{
    Task<T> LoadAsync<T>() where T : ConfigBase, new();
    Task SaveAsync<T>(T config) where T : ConfigBase;
    T Load<T>() where T : ConfigBase, new();
    void Save<T>(T config) where T : ConfigBase;
    void EnsureDirectories();
}
