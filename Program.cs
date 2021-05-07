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
            // Task.Run(() => CategoryTreeExample(chan)).Wait();
            // Task.Run(() => PrintImpacts(chan)).Wait();
            // Task.Run(() => ProviderExample(chan)).Wait();

            // Task.Run(() => ProductProviderExample.Run(chan)).Wait();
            // Task.Run(() => TolalImpactResultExample.Run(chan)).Wait();

            var examples = new Example[]
            {
                new CategoryTreeExample(chan),
                new GetAllExample(chan),
                new GetFlowDescriptorsExample(chan),
                new TolalResultExample(chan),
                new ContributionResultExample(chan),
            };

            while (true)
            {

                // print the examples
                Log("\nEnter the number of the example that you want to execute:");
                for (int i = 0; i < examples.Length; i++)
                {
                    Log($"  - {i + 1} - {examples[i].Description()}");
                }
                Log("  - or type exit (e) or quit (q) to exit\n");

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
                Log($"\nExecuting example: {example.GetType().FullName}...");
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

        private static async void PrintImpacts(Channel chan)
        {
            var client = new DataFetchService.DataFetchServiceClient(chan);
            var request = new GetAllRequest
            {
                ModelType = ModelType.ImpactCategory,
                PageSize = 100,
            };
            var response = client.GetAll(request);
            foreach (var ds in response.DataSet)
            {
                var method = ds.ImpactMethod;
                Console.WriteLine("+ " + method.Name);
                foreach (var impact in method.ImpactCategories)
                {
                    Console.WriteLine(string.Format("  - {0} [{1}]",
                        impact.Name, impact.RefUnit));
                }

            }
        }

        /// <summary>
        /// Prints the category tree of the database on the screen. Also,
        /// it writes the first 5 content items of the non-empty
        /// categories. Thus, this produces quite some output and for
        /// large databases.
        /// </summary>
        private static async void CategoryTreeExample(Channel chan)
        {
            var data = new DataFetchService.DataFetchServiceClient(chan);
            var tree = await CategoryTree.Build(data);
            var roots = tree.RootsOf(ModelType.Flow);

            /*
            // a function that returns the first five items of a
            // category in the tree
            async Task<List<Ref>> descriptorsOf(CategoryNode node)
            {
                var stream = data.GetDescriptors(new DescriptorRequest
                {
                    Type = node.ModelType,
                    Category = node.Category.Id,
                }).ResponseStream;
                var descritpors = new List<Ref>();
                var i = 0;
                while (await stream.MoveNext())
                {
                    i++;
                    descritpors.Add(stream.Current);
                    if (i > 5) break;
                }
                return descritpors;
            }

            async Task print((int, List<CategoryNode>) pair)
            {
                var (depth, nodes) = pair;
                var offset = " ".Repeat(depth * 2);
                foreach (var node in nodes)
                {
                    var line = string.Format("{0}+ {1}", offset, node.Name);
                    Console.WriteLine(line);
                    // expand the tree recursively
                    await print((depth + 1, node.Childs));
                    // print the category content
                    foreach (var d in await descriptorsOf(node))
                    {
                        var dLine = string.Format("{0} - {1}", offset, d.Name);
                        Console.WriteLine(dLine);
                    }
                }
            }

            await print((0, roots));
            */
        }
    }
}
