using System;
using System.Threading.Tasks;

using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using ProtoLCA;
using ProtoLCA.Services;

using static DemoApp.Util;

namespace DemoApp {

    /// <summary>
    /// This is the entry point of the example application. 
    /// </summary>
    class Program {
        static void Main() {
            var chan = new Channel("localhost:8080", ChannelCredentials.Insecure);
            var examples = new Example[] {
                new CategoryTreeExample(chan),
                new CategoryContentExample(chan),
                new GetAllExample(chan),
                new GetFlowDescriptorsExample(chan),
                new FlowSearchExample(chan),
                new ProductProviderExample(chan),
                new GetImpactMethodsExample(chan),
                new UnitExample(chan),
                new FlowMappingExample(chan),
                new ProcessExample(chan),
                new TotalResultExample(chan),
                new ContributionResultExample(chan),
                new ResultImpactFactorExample(chan),
            };

            while (true) {

                // print the examples
                Log("\nEnter the number of the example that you want to execute:");
                for (int i = 0; i < examples.Length; i++) {
                    Log($"  {i + 1} - {examples[i].GetType().Name}");
                    Log($"      {examples[i].Description()}\n");
                }
                Log("  or type exit (e) or quit (q) to exit\n");

                // select an example
                var line = Console.ReadLine().Trim().ToLower();
                if (line.StartsWith("e") || line.StartsWith("q")) break;
                Example example = null;
                try {
                    var i = int.Parse(line);
                    if (i >= 1 && i <= examples.Length) {
                        example = examples[i - 1];
                    }
                } catch (Exception) { }
                if (example == null) {
                    Log("invalid number; try again\n");
                    continue;
                }

                ExecuteExample(example);

                // ask to run again
                Log("\nRun again? yes (y)?");
                line = Console.ReadLine().Trim().ToLower();
                if (line.StartsWith("y")) {
                    continue;
                } else {
                    break;
                }
            }
        }

        private static void ExecuteExample(Example example) {
            Log($"\nExecuting example: {example.GetType().Name}...");
            var start = DateTime.Now;
            try {
                example.Run();
            } catch (Exception e) {
                Log("  .. ERROR: example failed with exception:");
                Log(e.ToString());
                return;
            }
            var time = (int) DateTime.Now.Subtract(start).TotalMilliseconds;
            if (time > 1000) {
                Log($"\n  .. finished in {time / 1000} s");
            } else {
                Log($"\n  .. finished in {time} ms");
            }
        }
    }
}
