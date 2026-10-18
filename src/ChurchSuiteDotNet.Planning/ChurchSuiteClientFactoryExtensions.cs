namespace ChurchSuiteDotNet.Planning;

/// <summary>Creates the Planning client from a <see cref="ChurchSuiteClientFactory"/>.</summary>
public static class ChurchSuiteClientFactoryExtensions
{
    public static PlanningClient CreatePlanningClient(this ChurchSuiteClientFactory factory) =>
        factory.Create(adapter => new PlanningClient(adapter));
}
