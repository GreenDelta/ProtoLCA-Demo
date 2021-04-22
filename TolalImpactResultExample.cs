using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp
{
    // In this example we calculate the total impact assessment result of
    // a process including its supply chain. Note that this only works 
    // when we are connected to a database with impact assessment methods
    // and processes that can be auto-connected. 
    class TolalImpactResultExample
    {

        private readonly Channel channel;

        internal TolalImpactResultExample(Channel channel)
        {
            this.channel = channel;
        }

        internal async void Run()
        {
            Log("Calculate the total LCIA result of a process");

            // try to find a process and impact method
            Log("  .. try to find a process");
            Ref process = await GetFirstOf(ModelType.Process);
            if (process == null)
            {
                Log("=> no process in database; exit");
                return;
            }
            Log("  .. try to find a LCIA method");
            Ref method = await GetFirstOf(ModelType.ImpactMethod);
            if (method == null)
            {
                Log("=> no LCIA method in database; exit");
                return;
            }

            // now calculate the result; note that we can take a
            // process for the product system in the calculation
            // setup
            Log($"  .. calculate result of {process.Name}"
                + $" with method {method.Name}");
            var setup = new CalculationSetup
            {
                ProductSystem = process,
                ImpactMethod = method,
                // calculate it for 1 unit of the reference flow
                // of the process
                Amount = 1.0,
            };
            var results = new ResultService.ResultServiceClient(channel);
            var result = results.Calculate(setup);

            // get and print the LCIA results
            var impacts = results.GetTotalImpacts(result).ResponseStream;
            while (await impacts.MoveNext())
            {
                var r = impacts.Current;
                Log($"  .. {r.ImpactCategory.Name} = {r.Value} {r.ImpactCategory.RefUnit}");
            }
        }

        // Get the first descriptor (Ref) of the given type from
        private async Task<Ref> GetFirstOf(ModelType type)
        {
            var fetch = new DataFetchService.DataFetchServiceClient(channel);
            var descriptors = fetch.GetDescriptors(new GetDescriptorsRequest
            {
                ModelType = type
            }).ResponseStream;
            return await descriptors.MoveNext()
                ? descriptors.Current
                : null;
        }

    }
}
