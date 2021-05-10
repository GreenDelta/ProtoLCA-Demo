using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp {

    /// <summary>
    /// This example shows how to use GetAll requests. By default these requests
    /// return a paged response.For small objects with a small number of data
    /// sets you may want to skip the paging.
    /// </summary>
    class GetAllExample : Example {
        private readonly Channel channel;

        public GetAllExample(Channel channel) {
            this.channel = channel;
        }

        public string Description() {
            return "Get the first page of all flows and all unit groups";
        }

        public void Run() {
            var service = new DataFetchService.DataFetchServiceClient(channel);

            // first page of flows
            Log("  .. get the first page of all flows");
            var flows = service.GetAll(new GetAllRequest {
                ModelType = ModelType.Flow,
            });
            Log($"  .. loaded first {flows.PageSize} of {flows.TotalCount} flows");
            int i = 0;
            foreach (var dataSet in flows.DataSet) {
                i++;
                Log($"  .. {i}. {dataSet.Flow.Name}");
                if (i >= 5)
                    break;
            }
            Log($"  .. {flows.PageSize - i} more");

            // all unit groups
            Log("\n  .. get all unit groups from the database");
            var groups = service.GetAll(new GetAllRequest {
                ModelType = ModelType.UnitGroup,
                SkipPaging = true
            });
            foreach (var dataSet in groups.DataSet) {
                var unitGroup = dataSet.UnitGroup;
                Unit refUnit = null;
                foreach (var unit in unitGroup.Units) {
                    if (unit.ReferenceUnit) {
                        refUnit = unit;
                        break;
                    }
                }
                var ru = refUnit != null ? refUnit.Name : "?";
                Log($"  .. {unitGroup.Name}; reference unit = {ru}");
            }
        }
    }
}
