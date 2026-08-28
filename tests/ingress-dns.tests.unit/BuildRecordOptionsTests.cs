using NUnit.Framework;
using Pulumi;

namespace Gandt.IngressDnsTests.Unit;

[TestFixture]
public class BuildRecordOptionsTests
{
    [Test]
    public void SetsImportIdWhenSupplied()
    {
        var options = IngressDns.BuildRecordOptions(null, "", "existing-record-id");

        Assert.That(options.ImportId, Is.EqualTo("existing-record-id"));
    }

    [Test]
    public void OmitsImportIdWhenEmpty()
    {
        var options = IngressDns.BuildRecordOptions(null, "", "");

        Assert.That(options.ImportId, Is.Null);
    }

    [Test]
    public void AddsAliasWhenSupplied()
    {
        var options = IngressDns.BuildRecordOptions(null, "urn:pulumi:stack::project::type::name", "");

        Assert.That(options.Aliases, Has.Count.EqualTo(1));
    }

    [Test]
    public void OmitsAliasWhenEmpty()
    {
        var options = IngressDns.BuildRecordOptions(null, "", "");

        Assert.That(options.Aliases, Is.Empty);
    }
}
