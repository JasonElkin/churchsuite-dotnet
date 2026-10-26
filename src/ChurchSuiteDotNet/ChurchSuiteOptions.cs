using System.ComponentModel.DataAnnotations;

namespace ChurchSuiteDotNet;

/// <summary>Settings used to authenticate with and call the ChurchSuite v2 API.</summary>
public class ChurchSuiteOptions
{
    /// <summary>Base address of the API.</summary>
    [Required, AbsoluteUri]
    public Uri BaseUrl { get; set; } = new("https://api.churchsuite.com/v2");

    /// <summary>OAuth2 token endpoint used for the client credentials grant.</summary>
    [Required, AbsoluteUri]
    public Uri TokenUrl { get; set; } = new("https://login.churchsuite.com/oauth2/token");

    /// <summary>OAuth2 client id.</summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret.</summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>OAuth2 scope requested with the token.</summary>
    [Required]
    public string Scope { get; set; } = "full_access";

    /// <summary>Throws an <see cref="ArgumentException"/> listing every invalid setting.</summary>
    internal void Validate(string paramName)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(this, new ValidationContext(this), results, validateAllProperties: true))
        {
            throw new ArgumentException(
                $"Invalid ChurchSuite options: {string.Join(" ", results.Select(r => r.ErrorMessage))}",
                paramName);
        }
    }

    private sealed class AbsoluteUriAttribute : ValidationAttribute
    {
        public AbsoluteUriAttribute()
            : base("The {0} field must be an absolute URI.")
        {
        }

        public override bool IsValid(object? value) => value is not Uri uri || uri.IsAbsoluteUri;
    }
}
