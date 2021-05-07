using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;
using Service = ProtoLCA.Services.ResultService.ResultServiceClient;

namespace DemoApp
{
    class TolalResultExample : Example
    {
        private readonly Channel channel;
        private readonly Service results;

        public TolalResultExample(Channel channel)
        {
            this.channel = channel;
            this.results = new Service(channel);
        }

        public string Description()
        {
            return "Calling GetTotalImpacts and GetTotalInventory on a process result";
        }

        public void Run()
        {
            Exec().Wait();
        }

        public async Task<bool> Exec()
        {
            Log("  .. calculate some process result");
            var result = await Examples.CalculateSomeProcessResult(channel);
            if (result == null)
                return false;

            Log("\n  .. get the total LCIA results");
            var impacts = results.GetTotalImpacts(result).ResponseStream;
            while (await impacts.MoveNext())
            {
                var r = impacts.Current;
                if (r.Impact == null)
                    continue;
                Log($"  .. {r.Impact.Name} = {r.Value} {r.Impact.RefUnit}");
            }

            Log("\n  .. get the total inventory results");
            var inventory = results.GetTotalInventory(result).ResponseStream;
            var inputs = new List<ResultValue>();
            var outputs = new List<ResultValue>();
            while (await inventory.MoveNext())
            {
                var r = inventory.Current;
                if (r.EnviFlow == null)
                    continue;
                if (r.EnviFlow.IsInput)
                    inputs.Add(r);
                else
                    outputs.Add(r);
            }

            // print some inputs
            Log($"\n  .. {inputs.Count} inputs");
            PrintTopOfInventory(inputs);
            Log($"\n  .. {outputs.Count} outputs");
            PrintTopOfInventory(outputs);

            results.Dispose(result);
            return true;
        }

        private void PrintTopOfInventory(List<ResultValue> flowResults)
        {
            if (flowResults.Count == 0)
                return;

            int i = 0;
            foreach (var r in flowResults)
            {
                i++;
                if (i > 10)
                    break;
                var flow = r.EnviFlow.Flow;
                var name = flow.Name;
                var unit = flow.RefUnit;
                var amount = r.Value;

                var category = "";
                if (flow.CategoryPath != null)
                {
                    category = flow.CategoryPath.Join("/");
                }

                Log($"  .. {name} ({category}): {amount} {unit}");

                i++;
                if (i > 10)
                    break;
            }
            if (i < flowResults.Count)
            {
                Log($"  .. {flowResults.Count - i} more");
            }
        }
    }
}
