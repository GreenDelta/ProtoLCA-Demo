using Grpc.Core;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp
{
    // This example shows how you get the total impact assessment Result of a process 
    static class TolalImpactResultExample
    {

        internal static async void Run(Channel channel)
        {
            Log("Get the total LCIA results of a process");

            var result = await Examples.CalculateFirstProcessResult(channel);

            // get and print the LCIA results
            var results = new ResultService.ResultServiceClient(channel);
            var impacts = results.GetTotalImpacts(result).ResponseStream;
            while (await impacts.MoveNext())
            {
                var r = impacts.Current;
                Log($"  .. {r.ImpactCategory.Name} = {r.Value} {r.ImpactCategory.RefUnit}");
            }
            results.Dispose(result);
        }
    }
}
