namespace SaltBox.Config;

public abstract class ConfigBase
{
    public int ConfigVersion { get; set; } = 1;
    public DateTime? LastUpdatedUtc { get; set; }
}
