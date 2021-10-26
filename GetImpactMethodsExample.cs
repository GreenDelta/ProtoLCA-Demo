using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;
using Service = ProtoLCA.Services.DataFetchService.DataFetchServiceClient;

namespace DemoApp {

    /// <summary>
    /// In this example all available impact assessment methods and their
    /// indicators are printed to the console.
    /// </summary>
    class GetImpactMethodsExample : Example {
        private readonly Service service;

        public GetImpactMethodsExample(Channel channel) {
            this.service = new Service(channel);
        }

        public string Description() {
            return "Get all impact assessment methods and list their indicators";
        }

        public void Run() {
            var response = service.GetAll(new GetAllRequest {
                Type = ProtoType.ImpactMethod,
                SkipPaging = true
            });
            foreach (var ds in response.DataSet) {
                var method = ds.ImpactMethod;
                if (method == null)
                    continue;
                Log($"\n  .. + {method.Name}");
                foreach (var impact in method.ImpactCategories) {
                    Log($"  ..   - {impact.Name} [{impact.RefUnit}]");
                }
            }
        }
    }
}
