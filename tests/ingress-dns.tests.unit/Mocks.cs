using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Testing;

namespace Gandt.IngressDnsTests.Unit;

public class Mocks : IMocks
{
    public bool AzureLookupCalled { get; private set; }

    public Task<(string? id, object state)> NewResourceAsync(MockResourceArgs args)
    {
        var outputs = ImmutableDictionary.CreateBuilder<string, object>();

        if (args.Inputs != null)
        {
            outputs.AddRange(args.Inputs);
        }

        if (args.Type == "aws:route53/record:Record")
        {
            var name = args.Inputs?.GetValueOrDefault("name")?.ToString() ?? "";
            var zoneId = args.Inputs?.GetValueOrDefault("zoneId")?.ToString() ?? "";
            outputs["fqdn"] = string.IsNullOrEmpty(name)
                ? "test.com"
                : $"{name}.test.com";
        }

        args.Id ??= $"{args.Name}_id";
        return Task.FromResult<(string? id, object state)>((args.Id, (object)outputs.ToImmutable()));
    }

    public Task<object> CallAsync(MockCallArgs args)
    {
        var outputs = ImmutableDictionary.CreateBuilder<string, object>();

        if (args.Token == "aws:route53/getZone:getZone")
        {
            outputs.Add("id", "Z1234567890");
            outputs.Add("name", "test.com");
            outputs.Add("zoneId", "Z1234567890");
        }

        if (args.Token == "azure-native:network:getPublicIPAddress")
        {
            AzureLookupCalled = true;
            outputs.Add("ipAddress", "1.2.3.4");
        }

        return Task.FromResult((object)outputs.ToImmutable());
    }
}
