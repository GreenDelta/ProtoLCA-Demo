using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;
using Service = ProtoLCA.Services.DataFetchService.DataFetchServiceClient;

namespace DemoApp {
    class FlowSearchExample : Example {

        private readonly Service service;

        public FlowSearchExample(Channel channel) {
            this.service = new Service(channel);
        }

        public string Description() {
            return "Calling Search: search for flows with a `carbon dio` query";
        }

        public void Run() {
            Exec().Wait();
        }

        private async Task<bool> Exec() {
            Log("  .. search flows for 'carbon dio'");
            var response = service.Search(new SearchRequest {
                ModelType = ModelType.Flow,
                Query = "carbon dio",
            }).ResponseStream;
            var flows = new List<Ref>();
            while (await response.MoveNext()) {
                flows.Add(response.Current);
            }
            Log($"  .. found {flows.Count} flows");
            int i = 0;
            foreach (var flow in flows) {
                if (i >= 10)
                    break;
                i++;
                var category = flow.CategoryPath != null
                    ? flow.CategoryPath.Join("/")
                    : "/";
                Log($"  .. {flow.Name}; {category}");
            }
            if (i < flows.Count) {
                Log($"  .. {flows.Count - i} more");
            }
            return true;
        }
    }
}
