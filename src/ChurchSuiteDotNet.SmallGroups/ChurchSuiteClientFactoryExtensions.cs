namespace ChurchSuiteDotNet.SmallGroups;

/// <summary>Creates the SmallGroups client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static SmallGroupsClient CreateSmallGroupsClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new SmallGroupsClient(adapter));
}
