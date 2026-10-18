namespace ChurchSuiteDotNet.Addressbook;

/// <summary>Creates the Addressbook client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static AddressbookClient CreateAddressbookClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new AddressbookClient(adapter));
}
