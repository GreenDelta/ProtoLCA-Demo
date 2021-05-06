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

            var result = await Examples.CalculateSomeProcessResult(channel);
            if (result == null)
                return;

            // get and print the LCIA results
            var results = new ResultService.ResultServiceClient(channel);
            var impacts = results.GetTotalImpacts(result).ResponseStream;
            while (await impacts.MoveNext())
            {
                var r = impacts.Current;
                if (r.Impact == null)
                    continue;
                Log($"  .. {r.Impact.Name} = {r.Value} {r.Impact.RefUnit}");
            }
            results.Dispose(result);
        }
    }
}
