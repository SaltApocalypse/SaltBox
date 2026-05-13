namespace SaltBox.Contracts;

public interface IToolModule
{
    string Name { get; }
    string Description { get; }
    Type PageType { get; }
}
