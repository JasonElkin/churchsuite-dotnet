namespace ChurchSuiteDotNet.Giving;

/// <summary>Creates the Giving client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static GivingClient CreateGivingClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new GivingClient(adapter));
}
