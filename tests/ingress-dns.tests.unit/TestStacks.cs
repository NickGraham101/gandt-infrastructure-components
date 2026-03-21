using Pulumi;

namespace Gandt.IngressDnsTests.Unit;

class PrimaryRecordOnlyStack : Stack
{
    [Output("primaryRecord")]
    public Output<string> PrimaryRecord { get; set; }

    public PrimaryRecordOnlyStack()
    {
        var dns = new IngressDns("test", new IngressDnsArgs
        {
            ZoneName = "test.com",
            IpAddressResourceGroupName = "test-rg",
            IpAddressResourceName = "test-ip",
            PrimaryRecordName = "www",
            CreateRootRecord = false
        });

        PrimaryRecord = dns.primaryRecord;
    }
}

class WithRootRecordStack : Stack
{
    public WithRootRecordStack()
    {
        var dns = new IngressDns("test", new IngressDnsArgs
        {
            ZoneName = "test.com",
            IpAddressResourceGroupName = "test-rg",
            IpAddressResourceName = "test-ip",
            PrimaryRecordName = "www",
            CreateRootRecord = true,
            RootRecordImportId = "root-import-id"
        });
    }
}

class IpAddressOverrideStack : Stack
{
    public IpAddressOverrideStack()
    {
        var dns = new IngressDns("test", new IngressDnsArgs
        {
            ZoneName = "test.com",
            IpAddressResourceGroupName = "unused",
            IpAddressResourceName = "unused",
            IpAddress = "5.6.7.8",
            PrimaryRecordName = "www",
            CreateRootRecord = false
        });
    }
}
