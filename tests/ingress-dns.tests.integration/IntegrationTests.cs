using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Route53;
using Amazon.Route53.Model;
using NUnit.Framework;
using Pulumi;
using Pulumi.Automation;

namespace Gandt.IngressDnsTests.Integration;

public static class TestHelpers
{
    public const string LocalStackEndpoint = "http://localhost:4566";
    public const string ProjectName = "ingress-dns-integration-test";

    public static AmazonRoute53Client CreateRoute53Client()
    {
        var config = new AmazonRoute53Config
        {
            ServiceURL = LocalStackEndpoint,
            AuthenticationRegion = "us-east-1"
        };
        return new AmazonRoute53Client("test", "test", config);
    }

    public static async Task<string> CreateHostedZone(
        AmazonRoute53Client client, string zoneName)
    {
        var response = await client.CreateHostedZoneAsync(
            new CreateHostedZoneRequest
            {
                Name = zoneName,
                CallerReference = Guid.NewGuid().ToString()
            });
        return response.HostedZone.Id;
    }

    public static async Task<WorkspaceStack> CreateStack(
        PulumiFn program, string stackName)
    {
        var stackArgs = new InlineProgramArgs(ProjectName, stackName, program)
        {
            ProjectSettings = new ProjectSettings(ProjectName, ProjectRuntimeName.Dotnet)
            {
                Backend = new ProjectBackend { Url = "file://~" }
            },
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["PULUMI_CONFIG_PASSPHRASE"] = ""
            }
        };

        var stack = await LocalWorkspace.CreateOrSelectStackAsync(stackArgs);

        await stack.SetAllConfigAsync(new Dictionary<string, ConfigValue>
        {
            ["aws:region"] = new ConfigValue("us-east-1"),
            ["aws:accessKey"] = new ConfigValue("test"),
            ["aws:secretKey"] = new ConfigValue("test"),
            ["aws:skipCredentialsValidation"] = new ConfigValue("true"),
            ["aws:skipRequestingAccountId"] = new ConfigValue("true"),
        });

        // Set endpoint config using path syntax for structured config
        await stack.SetConfigAsync(
            "aws:endpoints[0].route53",
            new ConfigValue(LocalStackEndpoint),
            path: true);

        return stack;
    }
}

[TestFixture]
public class PrimaryRecordTests
{
    private const string TestZoneName = "integration-test.com";
    private const string TestIpAddress = "10.0.0.1";
    private const string StackName = "primary";

    private AmazonRoute53Client _route53Client = null!;
    private string _hostedZoneId = null!;
    private WorkspaceStack _stack = null!;
    private UpResult _upResult = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _route53Client = TestHelpers.CreateRoute53Client();

        _hostedZoneId = await TestHelpers.CreateHostedZone(
            _route53Client, TestZoneName);

        var program = PulumiFn.Create(() =>
        {
            var dns = new IngressDns("integration-test", new IngressDnsArgs
            {
                ZoneName = TestZoneName,
                IpAddressResourceGroupName = "unused",
                IpAddressResourceName = "unused",
                IpAddress = TestIpAddress,
                PrimaryRecordName = "www",
                CreateRootRecord = false
            });

            return new Dictionary<string, object?>
            {
                ["primaryRecord"] = dns.primaryRecord
            };
        });

        _stack = await TestHelpers.CreateStack(program, StackName);

        await _stack.Workspace.InstallPluginAsync("aws", "v7.11.1");

        _upResult = await _stack.UpAsync(new UpOptions
        {
            OnStandardOutput = Console.WriteLine,
            OnStandardError = Console.Error.WriteLine
        });
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        if (_stack != null)
        {
            await _stack.DestroyAsync(new DestroyOptions
            {
                OnStandardOutput = Console.WriteLine
            });
            await _stack.Workspace.RemoveStackAsync(StackName);
        }

        _route53Client?.Dispose();
    }

    [Test]
    public void PrimaryRecordOutputIsSet()
    {
        Assert.That(_upResult.Outputs.ContainsKey("primaryRecord"), Is.True);

        var fqdn = _upResult.Outputs["primaryRecord"].Value?.ToString();
        Assert.That(fqdn, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task RecordPointsToSuppliedIp()
    {
        var records = await GetRecordSets();

        var aRecord = records.FirstOrDefault(r =>
            r.Name == $"www.{TestZoneName}." && r.Type == RRType.A);

        Assert.That(aRecord, Is.Not.Null, "A record for www should exist");
        Assert.That(aRecord!.ResourceRecords[0].Value, Is.EqualTo(TestIpAddress));
    }

    [Test]
    public async Task RecordHasCorrectTtl()
    {
        var records = await GetRecordSets();

        var aRecord = records.FirstOrDefault(r =>
            r.Name == $"www.{TestZoneName}." && r.Type == RRType.A);

        Assert.That(aRecord, Is.Not.Null);
        Assert.That(aRecord!.TTL, Is.EqualTo(300));
    }

    [Test]
    public async Task IdempotentRerun()
    {
        var secondResult = await _stack.UpAsync(new UpOptions
        {
            OnStandardOutput = Console.WriteLine
        });

        var changes = secondResult.Summary.ResourceChanges;
        if (changes != null)
        {
            var nonSameChanges = changes
                .Where(c => c.Key != OperationType.Same)
                .Sum(c => c.Value);
            Assert.That(nonSameChanges, Is.EqualTo(0),
                "Second pulumi up should produce no changes");
        }
    }

    private async Task<List<ResourceRecordSet>> GetRecordSets()
    {
        var response = await _route53Client.ListResourceRecordSetsAsync(
            new ListResourceRecordSetsRequest
            {
                HostedZoneId = _hostedZoneId
            });
        return response.ResourceRecordSets;
    }
}

[TestFixture]
public class RootRecordTests
{
    private const string TestZoneName = "root-record-test.com";
    private const string TestIpAddress = "10.0.0.2";
    private const string StackName = "root";

    private AmazonRoute53Client _route53Client = null!;
    private string _hostedZoneId = null!;
    private WorkspaceStack _stack = null!;

    [OneTimeSetUp]
    public async Task Setup()
    {
        _route53Client = TestHelpers.CreateRoute53Client();

        _hostedZoneId = await TestHelpers.CreateHostedZone(
            _route53Client, TestZoneName);

        var program = PulumiFn.Create(() =>
        {
            var dns = new IngressDns("root-test", new IngressDnsArgs
            {
                ZoneName = TestZoneName,
                IpAddressResourceGroupName = "unused",
                IpAddressResourceName = "unused",
                IpAddress = TestIpAddress,
                PrimaryRecordName = "www",
                CreateRootRecord = true,
                RootRecordImportId = ""
            });

            return new Dictionary<string, object?>
            {
                ["primaryRecord"] = dns.primaryRecord
            };
        });

        _stack = await TestHelpers.CreateStack(program, StackName);

        await _stack.Workspace.InstallPluginAsync("aws", "v7.11.1");

        await _stack.UpAsync(new UpOptions
        {
            OnStandardOutput = Console.WriteLine,
            OnStandardError = Console.Error.WriteLine
        });
    }

    [OneTimeTearDown]
    public async Task Teardown()
    {
        if (_stack != null)
        {
            await _stack.DestroyAsync(new DestroyOptions
            {
                OnStandardOutput = Console.WriteLine
            });
            await _stack.Workspace.RemoveStackAsync(StackName);
        }

        _route53Client?.Dispose();
    }

    [Test]
    public async Task BothRecordsExist()
    {
        var response = await _route53Client.ListResourceRecordSetsAsync(
            new ListResourceRecordSetsRequest
            {
                HostedZoneId = _hostedZoneId
            });

        var aRecords = response.ResourceRecordSets
            .Where(r => r.Type == RRType.A)
            .ToList();

        Assert.That(aRecords, Has.Count.EqualTo(2),
            "Should have both primary and root A records");
    }
}
