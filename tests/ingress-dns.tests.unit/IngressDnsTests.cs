using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Pulumi;
using Aws = Pulumi.Aws;

namespace Gandt.IngressDnsTests.Unit;

[TestFixture]
public class IngressDnsTests
{
    [Test]
    public async Task CreatesPrimaryRoute53Record()
    {
        var mocks = new Mocks();
        var resources = await TestHelpers.RunAsync<PrimaryRecordOnlyStack>(mocks);

        var records = resources.OfType<Aws.Route53.Record>().ToList();
        Assert.That(records, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PrimaryRecordHasCorrectZoneId()
    {
        var mocks = new Mocks();
        var resources = await TestHelpers.RunAsync<PrimaryRecordOnlyStack>(mocks);

        var record = resources.OfType<Aws.Route53.Record>().First();
        var zoneId = await record.ZoneId.GetValueAsync();
        Assert.That(zoneId, Is.EqualTo("Z1234567890"));
    }

    [Test]
    public async Task PrimaryRecordHasCorrectTtl()
    {
        var mocks = new Mocks();
        var resources = await TestHelpers.RunAsync<PrimaryRecordOnlyStack>(mocks);

        var record = resources.OfType<Aws.Route53.Record>().First();
        var ttl = await record.Ttl.GetValueAsync();
        Assert.That(ttl, Is.EqualTo(300));
    }

    [Test]
    public async Task PrimaryRecordPointsToAzureIp()
    {
        var mocks = new Mocks();
        var resources = await TestHelpers.RunAsync<PrimaryRecordOnlyStack>(mocks);

        var record = resources.OfType<Aws.Route53.Record>().First();
        var recordValues = await record.Records.GetValueAsync();
        Assert.That(recordValues, Does.Contain("1.2.3.4"));
    }

    [Test]
    public async Task OutputsFqdn()
    {
        var mocks = new Mocks();
        var resources = await TestHelpers.RunAsync<PrimaryRecordOnlyStack>(mocks);

        var stack = resources.OfType<PrimaryRecordOnlyStack>().First();
        var fqdn = await stack.PrimaryRecord.GetValueAsync();
        Assert.That(fqdn, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task RootRecordCreatedWhenFlagIsTrue()
    {
        var mocks = new Mocks();
        var resources = await TestHelpers.RunAsync<WithRootRecordStack>(mocks);

        var records = resources.OfType<Aws.Route53.Record>().ToList();
        Assert.That(records, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task RootRecordNotCreatedWhenFlagIsFalse()
    {
        var mocks = new Mocks();
        var resources = await TestHelpers.RunAsync<PrimaryRecordOnlyStack>(mocks);

        var records = resources.OfType<Aws.Route53.Record>().ToList();
        Assert.That(records, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task IpAddressOverrideSkipsAzureLookup()
    {
        var mocks = new Mocks();
        var resources = await TestHelpers.RunAsync<IpAddressOverrideStack>(mocks);

        Assert.That(mocks.AzureLookupCalled, Is.False);

        var record = resources.OfType<Aws.Route53.Record>().First();
        var recordValues = await record.Records.GetValueAsync();
        Assert.That(recordValues, Does.Contain("5.6.7.8"));
    }
}
