using SaltBox.Config;
using SaltBox.Contracts;

namespace SaltBox.Modules.FileExtractor;

[ConfigFileName("fileextractor")]
public class FileExtractorConfig : ConfigBase
{
    public bool IsEnabled { get; set; } = false;
    public int NotificationMode { get; set; } = 1;
}
