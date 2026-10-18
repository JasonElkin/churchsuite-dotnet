namespace ChurchSuiteDotNet;

/// <summary>Settings used to authenticate with and call the ChurchSuite v2 API.</summary>
public class ChurchSuiteOptions
{
    /// <summary>Base address of the API.</summary>
    public Uri BaseUrl { get; set; } = new("https://api.churchsuite.com/v2");

    /// <summary>OAuth2 token endpoint used for the client credentials grant.</summary>
    public Uri TokenUrl { get; set; } = new("https://login.churchsuite.com/oauth2/token");

    /// <summary>OAuth2 client id.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>OAuth2 scope requested with the token.</summary>
    public string Scope { get; set; } = "full_access";
}
