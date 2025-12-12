using System;
using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Aws = Pulumi.Aws;
using Azure = Pulumi.AzureNative;

public sealed class IngressDnsArgs : ResourceArgs {
    [Input("zoneName")]
    public Input<string> ZoneName { get; set; } = null!;
    [Input("ipAddressResourceGroupName")]
    public Input<string> IpAddressResourceGroupName { get; set; } = null!;
    [Input("ipAddressResourceName")]
    public Input<string> IpAddressResourceName { get; set; } = null!;
    [Input("primaryRecordName")]
    public Input<string> PrimaryRecordName { get; set; } = null!;
    [Input("primaryRecordImportId", false)]
    public Input<string> PrimaryRecordImportId { get; set; } = null!;
    [Input("primaryRecordAlias", false)]
    public Input<Alias> PrimaryRecordAlias { get; set; } = null!;
    [Input("createRootRecord")]
    public Input<bool> CreateRootRecord { get; set; } = null!;
    [Input("rootRecordImportId", false)]
    public Input<string> RootRecordImportId { get; set; } = null!;
    [Input("rootRecordAlias", false)]
    public Input<Alias> RootRecordAlias { get; set; } = null!;
}

class IngressDns : ComponentResource {

    [Output("primaryRecord")]
    public Output<string> primaryRecord { get; private set; }

    public IngressDns(string name, IngressDnsArgs args, ComponentResourceOptions? opts = null)
        : base("gandt.infrastructure.components.ingress-dns:index:IngressDns", name, opts)
    {

    string zoneName = String.Empty;
    args.ZoneName.Apply(z => zoneName = z);
    Log.Info($"Getting zone resource for '{zoneName}'");
    var dns_zone = Aws.Route53.GetZone.Invoke(new()
    {
        Name = zoneName
    });

    InvokeOptions invokeOptions = new InvokeOptions();
    if (opts != null && opts.Providers.Count > 0)
        invokeOptions.Provider = opts.Providers.First();
    Log.Info("Getting public ip address");
    var publicIpAddress = Azure.Network.GetPublicIPAddress.Invoke(new()
    {
        ResourceGroupName = args.IpAddressResourceGroupName,
        PublicIpAddressName = args.IpAddressResourceName
    },
    invokeOptions);

    if (publicIpAddress.Apply(x => x.IpAddress) == null)
    {
        throw new Exception($"Public IP address {args.IpAddressResourceName} not found in resource group {args.IpAddressResourceGroupName}.");
    }

    string primaryImportId = String.Empty;
    args.PrimaryRecordImportId.Apply(r => primaryImportId = r);
    var primaryRecord = new Aws.Route53.Record("primaryRecord", new()
    {
        ZoneId = dns_zone.Apply(x => x.Id),
        Name = args.PrimaryRecordName,
        Type = Aws.Route53.RecordType.A,
        Ttl = 300,
        Records = new[]
        {
            publicIpAddress.Apply(x => $"{x.IpAddress}")
        }
    },
    new CustomResourceOptions
    {
        Aliases = args.PrimaryRecordAlias != null ? new List<Input<Alias>>() { args.PrimaryRecordAlias} : new List<Input<Alias>>(),
        ImportId = primaryImportId,
        Parent = this
    });

    args.CreateRootRecord.Apply(createRootRecord =>
    {
        if (createRootRecord && args.RootRecordImportId != null)
        {
            string rootImportId = String.Empty;
            args.RootRecordImportId.Apply(r => rootImportId = r);
            var rootRecord = new Aws.Route53.Record("rootRecord", new()
            {
                ZoneId = dns_zone.Apply(x => x.Id),
                Name = "",
                Type = Aws.Route53.RecordType.A,
                Ttl = 300,
                Records = new[]
                {
                    publicIpAddress.Apply(x => $"{x.IpAddress}")
                }
            },
            new CustomResourceOptions
            {
                Aliases = args.RootRecordAlias != null ? new List<Input<Alias>>() { args.RootRecordAlias} : new List<Input<Alias>>(),
                ImportId = rootImportId,
                Parent = this
            });
        }
        return createRootRecord;
    });

    this.primaryRecord = primaryRecord.Fqdn;

    this.RegisterOutputs(new Dictionary<string, object?> {
        ["primaryRecord"] = primaryRecord.Fqdn
    });
    }
}
