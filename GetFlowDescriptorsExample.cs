using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp {
    /// <summary>
    /// This example shows how to load descriptors from a database. Descriptors
    /// are small objects that describe a data set. When you do not need the
    /// complete data set but e.g. just want to search for data it is generally
    /// better to use the data set descriptors instead of the full data sets for
    /// this.
    /// </summary>
    class GetFlowDescriptorsExample : Example {
        private readonly Channel channel;

        public GetFlowDescriptorsExample(Channel channel) {
            this.channel = channel;
        }

        public string Description() {
            return "Get all flow descriptors";
        }

        public void Run() {
            Exec().Wait();
        }


        public async Task<bool> Exec() {
            var service = new DataFetchService.DataFetchServiceClient(channel);

            // get all flow descriptors
            var response = service.GetDescriptors(new GetDescriptorsRequest {
                ModelType = ModelType.Flow,
            }).ResponseStream;
            var flows = new List<Ref>();
            while (await response.MoveNext()) {
                flows.Add(response.Current);
            }

            // print some results
            Log($"  .. collected {flows.Count} flow descriptors");
            int i = 0;
            foreach (var flow in flows) {
                i++;
                var category = flow.CategoryPath != null
                    ? flow.CategoryPath.Join("/")
                    : "/";
                if (category.Length > 40) {
                    category = category.Substring(0, 37) + "...";
                }
                Log($"  .. {i}. {flow.Name} ({flow.RefUnit}; {category})");
                if (i >= 10)
                    break;
            }
            Log($"  .. {flows.Count - i} more");
            return true;
        }
    }
}
