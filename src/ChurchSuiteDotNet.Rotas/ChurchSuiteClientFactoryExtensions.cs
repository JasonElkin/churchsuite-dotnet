namespace ChurchSuiteDotNet.Rotas;

/// <summary>Creates the Rotas client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static RotasClient CreateRotasClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new RotasClient(adapter));
}
