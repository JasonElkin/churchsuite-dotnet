namespace ChurchSuiteDotNet.Children;

/// <summary>Creates the Children client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static ChildrenClient CreateChildrenClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new ChildrenClient(adapter));
}
