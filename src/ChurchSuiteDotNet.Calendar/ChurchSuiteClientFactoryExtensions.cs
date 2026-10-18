namespace ChurchSuiteDotNet.Calendar;

/// <summary>Creates the Calendar client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static CalendarClient CreateCalendarClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new CalendarClient(adapter));
}
