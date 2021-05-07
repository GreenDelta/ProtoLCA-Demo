using System;
using System.Threading.Tasks;

using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using ProtoLCA;
using ProtoLCA.Services;

using static DemoApp.Util;

namespace DemoApp
{
    class Program
    {
        static void Main()
        {
            var chan = new Channel("localhost:8080", ChannelCredentials.Insecure);
            // Task.Run(() => UnitMappingExample(chan)).Wait();
            // Task.Run(() => FlowMappingExample(chan)).Wait();
            // Task.Run(() => ProcessExample.Run(chan)).Wait();
            // Task.Run(() => PrintAllMappingFiles(chan)).Wait();
            // Task.Run(() => PrintImpacts(chan)).Wait();

            var examples = new Example[]
            {
                new CategoryTreeExample(chan),
                new CategoryContentExample(chan),
                new GetAllExample(chan),
                new GetFlowDescriptorsExample(chan),
                new ProductProviderExample(chan),
                new GetImpactMethodsExample(chan),
                new TolalResultExample(chan),
                new ContributionResultExample(chan),
                new ResultImpactFactorExample(chan),
            };

            while (true)
            {

                // print the examples
                Log("\nEnter the number of the example that you want to execute:");
                for (int i = 0; i < examples.Length; i++)
                {
                    Log($"  {i + 1} - {examples[i].GetType().Name}");
                    Log($"      {examples[i].Description()}\n");
                }
                Log("  or type exit (e) or quit (q) to exit\n");

                // select an example
                var line = Console.ReadLine().Trim().ToLower();
                if (line.StartsWith("e") || line.StartsWith("q")) break;
                Example example = null;
                try
                {
                    var i = int.Parse(line);
                    if (i >= 1 && i <= examples.Length)
                    {
                        example = examples[i - 1];
                    }

                }
                catch (Exception) { }
                if (example == null)
                {
                    Log("invalid number; try again\n");
                    continue;
                }

                // execute the example and measure the execution time
                Log($"\nExecuting example: {example.GetType().Name}...");
                var start = DateTime.Now;
                example.Run();
                var time = (int)DateTime.Now.Subtract(start).TotalMilliseconds;
                if (time > 1000)
                {
                    Log($"\n  .. finished in {time / 1000} s");
                }
                else
                {
                    Log($"\n  .. finished in {time} ms");
                }

                // ask to run again
                Log("\nRun again? yes (y)?");
                line = Console.ReadLine().Trim().ToLower();
                if (line.StartsWith("y"))
                {
                    continue;
                }
                else
                {
                    break;
                }

            }
        }

        /// <summary>
        /// Shows the mapping of flows. It first checks the mapping file
        /// in openLCA (which is created if it does not exist) for a
        /// matching entry. If this does not exist, it searches for a
        /// matching existing flow and creates it if no matching flow
        /// can be found. Finally, it updates the mapping and returns
        /// the corresponding flow mapping. Note that in a real
        /// application you probably want to separate these steps. See
        /// the `FlowFetch` for implementation details.
        /// </summary>
        private static async void FlowMappingExample(Channel chan)
        {
            var flows = await FlowFetch.Create(
                chan, mapping: "ProtoLCA-Demo.csv");

            // this should find a matching flow in the reference data
            var entry = await flows.Get(
                FlowQuery.ForElementary("Carbon dioxide")
                .WithUnit("g")
                .WithCategory("air/unspecified"));

            Log("Carbon dioxide | g | air/unspecified is mapped to:");
            Log($"  flow: {entry.To.Flow.Name}");
            Log($"  category: {entry.To.Flow.CategoryPath}");
            Log($"  unit: {entry.To.Flow.RefUnit}");
            Log($"  conversion factor: {entry.ConversionFactor}\n");

            // this should create a new flow
            entry = await flows.Get(
                FlowQuery.ForElementary("SARS-CoV-2 viruses")
                .WithUnit("Item(s)")
                .WithCategory("air/urban"));

            Log("SARS-CoV-2 viruses | Item(s) | air/unspecified is mapped to:");
            Log($"  flow: {entry.To.Flow.Name}");
            Log($"  category: {entry.To.Flow.CategoryPath}");
            Log($"  unit: {entry.To.Flow.RefUnit}");
            Log($"  conversion factor: {entry.ConversionFactor}\n");
        }

        /// <summary>
        /// Shows how to map a unit name to the corresponding unit,
        /// unit group, and flow property objects in openLCA.
        /// </summary>
        private static async void UnitMappingExample(Channel chan)
        {
            var index = await UnitIndex.Build(chan);
            var tons = index.EntryOf("t");
            Log($"unit: {tons.Unit.Name}");
            Log($"unit group: {tons.UnitGroup.Name}");
            Log($"flow property (quantity): {tons.FlowProperty.Name}");
            Log($"conversion factor: {tons.Factor}");
        }

        private static async void PrintAllMappingFiles(Channel chan)
        {
            var service = new FlowMapService.FlowMapServiceClient(chan);
            var mappings = service.GetAll(new Empty()).ResponseStream;
            while (await mappings.MoveNext())
            {
                var name = mappings.Current.Name;
                Log($"Found mapping: {name}");
            }
        }
    }
}
