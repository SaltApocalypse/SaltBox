using SaltBox.Contracts;

namespace SaltBox.Config;

[ConfigFileName("app")]
public class AppConfig : ConfigBase
{
    public string Theme { get; set; } = "Default";
    public string Language { get; set; } = "en-US";
    public string? Version { get; set; }
}
