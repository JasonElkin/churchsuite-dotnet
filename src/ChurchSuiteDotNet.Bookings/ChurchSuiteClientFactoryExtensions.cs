namespace ChurchSuiteDotNet.Bookings;

/// <summary>Creates the Bookings client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static BookingsClient CreateBookingsClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new BookingsClient(adapter));
}
