using NUnit.Framework;

namespace ChurchSuiteDotNet.Tests;

[TestFixture]
public class ChurchSuiteOptionsTests
{
    [Test]
    public void FactoryListsEveryMissingCredential()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ChurchSuiteClientFactory(new ChurchSuiteOptions()));

        Assert.That(ex!.ParamName, Is.EqualTo("options"));
        Assert.That(ex.Message, Does.Contain("ClientId").And.Contain("ClientSecret"));
    }

    [Test]
    public void FactoryRejectsRelativeUrls()
    {
        var options = new ChurchSuiteOptions { ClientId = "id", ClientSecret = "secret", BaseUrl = new("v2", UriKind.Relative) };

        var ex = Assert.Throws<ArgumentException>(() => new ChurchSuiteClientFactory(options));

        Assert.That(ex!.Message, Does.Contain("BaseUrl field must be an absolute URI"));
    }

    [Test]
    public void FactoryRejectsBlankScope()
    {
        var options = new ChurchSuiteOptions { ClientId = "id", ClientSecret = "secret", Scope = " " };

        Assert.Throws<ArgumentException>(() => new ChurchSuiteClientFactory(options));
    }
}
