using SaltBox.Config;

namespace SaltBox.Contracts;

public interface IConfigMigration<T> where T : ConfigBase
{
    int FromVersion { get; }
    int ToVersion { get; }
    T Migrate(T config);
}
