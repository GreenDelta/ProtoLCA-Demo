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
    public static class Examples
    {

        // Get the first data set descriptor (Ref) of the given type from the
        // data service.
        public static async Task<Ref> GetFirstDescriptorOf(
            Channel channel, ModelType type)
        {
            Log($"  .. fetch all descriptors of type {type}");
            var fetch = new DataFetchService.DataFetchServiceClient(channel);
            var descriptors = fetch.GetDescriptors(new GetDescriptorsRequest
            {
                ModelType = type
            }).ResponseStream;
            
            if (await descriptors.MoveNext())
            {
                var d = descriptors.Current;
                Log($"  .. selected {type} {d.Name}");
                return d;
            }
            Log($"  .. no data set of type {type} found");
            return null;
        }

        // Tries to calculate the result of the first process in the database.
        public static async Task<Result> CalculateFirstProcessResult(
            Channel channel)
        {
            var process = await GetFirstDescriptorOf(
                channel, ModelType.Process);
            if (process == null)
            {
                Log("=> database has no processes; cannot calculate result");
                return null;
            }
            Log($"  .. try to calculate result of process {process.Name}");

            var method = await GetFirstDescriptorOf(
                channel, ModelType.ImpactMethod);
            if (method != null)
            {
                Log($"  .. calculate results with LCIA method {method.Name}");
            } else 
            {
                Log("  .. calculate results without LCIA method");
            }
            var setup = new CalculationSetup
            {
                ProductSystem = process,
                ImpactMethod = method,
                // calculate it for 1 unit of the reference flow
                // of the process
                Amount = 1.0,
            };
            var results = new ResultService.ResultServiceClient(channel);
            return results.Calculate(setup);
        }

    }
}
