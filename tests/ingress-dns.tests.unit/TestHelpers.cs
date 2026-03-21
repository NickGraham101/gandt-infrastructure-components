using System.Collections.Immutable;
using System.Threading.Tasks;
using Pulumi;
using Pulumi.Testing;

namespace Gandt.IngressDnsTests.Unit;

public static class TestHelpers
{
    public static Task<ImmutableArray<Resource>> RunAsync<T>(Mocks mocks) where T : Stack, new()
    {
        return Deployment.TestAsync<T>(mocks, new TestOptions { IsPreview = false });
    }

    public static Task<T> GetValueAsync<T>(this Output<T> output)
    {
        var tcs = new TaskCompletionSource<T>();
        output.Apply(v =>
        {
            tcs.SetResult(v);
            return v;
        });
        return tcs.Task;
    }
}
