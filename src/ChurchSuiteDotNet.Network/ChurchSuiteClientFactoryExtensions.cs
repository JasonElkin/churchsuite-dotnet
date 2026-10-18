namespace ChurchSuiteDotNet.Network;

/// <summary>Creates the Network client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static NetworkClient CreateNetworkClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new NetworkClient(adapter));
}
