using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;
using Service = ProtoLCA.Services.DataFetchService.DataFetchServiceClient;

namespace DemoApp {

    /// <summary>
    /// This example shows how to search for something.
    /// </summary>
    class FlowSearchExample : Example {

        private readonly Service service;

        public FlowSearchExample(Channel channel) {
            this.service = new Service(channel);
        }

        public string Description() {
            return "Search for a flow";
        }

        public void Run() {
            Exec().Wait();
        }

        private async Task<bool> Exec() {
            Log("  .. search flows for 'carbon dio'");
            var response = service.Search(new SearchRequest {
                Type = ProtoType.Flow,
                Query = "carbon dio",
            }).ResponseStream;
            var flows = new List<ProtoRef>();
            while (await response.MoveNext()) {
                flows.Add(response.Current);
            }
            Log($"  .. found {flows.Count} flows");
            int i = 0;
            foreach (var flow in flows) {
                if (i >= 10)
                    break;
                i++;
                var category = flow.Category ?? "/";
                Log($"  .. {flow.Name}; {category}");
            }
            if (i < flows.Count) {
                Log($"  .. {flows.Count - i} more");
            }
            return true;
        }
    }
}
