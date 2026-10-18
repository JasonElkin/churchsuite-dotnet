namespace ChurchSuiteDotNet.Attendance;

/// <summary>Creates the Attendance client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static AttendanceClient CreateAttendanceClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new AttendanceClient(adapter));
}
