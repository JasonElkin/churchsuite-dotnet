namespace ChurchSuiteDotNet.Account;

/// <summary>Creates the Account client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static AccountClient CreateAccountClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new AccountClient(adapter));
}
