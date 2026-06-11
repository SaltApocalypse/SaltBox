namespace SaltBox.Contracts;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ConfigFileNameAttribute : Attribute
{
    public string Name { get; }
    public ConfigFileNameAttribute(string name) => Name = name;
}
