using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using static DemoApp.Util;
using DataService = ProtoLCA.Services.DataFetchService.DataFetchServiceClient;
using MappingService = ProtoLCA.Services.FlowMapService.FlowMapServiceClient;

namespace DemoApp {

    /// <summary>
    /// This example shows how to create and update flow mappings.
    /// </summary>
    class FlowMappingExample : Example {

        private readonly Channel channel;
        private readonly DataService dataService;
        private readonly MappingService mappingService;

        public FlowMappingExample(Channel channel) {
            this.channel = channel;
            this.dataService = new DataService(channel);
            this.mappingService = new MappingService(channel);
        }

        public string Description() {
            return "Create flow mappings";
        }

        public void Run() {
            Exec().Wait();
        }

        private async Task<bool> Exec() {

            var mapping = await Examples.GetExampleFlowMap(channel);
            mapping.Mappings.Clear();

            // define the flows for which we want to create mappings
            var p = ProtoFlowType.ProductFlow;
            var w = ProtoFlowType.WasteFlow;
            var e = ProtoFlowType.ElementaryFlow;
            var flowList = new List<(ProtoFlowType, string, string, string)> {
                (p, "Air Blast", "t", ""),
                (w, "Combustion Air", "t", ""),
                (p, "Hematite Pellets", "t", ""),
                (p, "Coke", "t", ""),
                (p, "Limestone", "t", ""),
                (w, "Steel Scrap", "t", ""),
                (p, "Reductant", "t", ""),
                (p, "Washing Solution", "t", ""),

                (w, "Slag", "t", ""),
                (e, "Carbon dioxide", "t", "emission/air/unspecified"),
                (e, "Water vapour", "t", "emission/air/unspecified"),
                (e, "Sulfur dioxide", "t", "emission/air/unspecified"),
                (e, "Air", "t", "emission/air/unspecified"),
                (p, "Pig Iron", "t", ""),
                (w, "Heat Loss", "kWh", ""),
                (e, "Coarse Dust", "kg", "emission/air/unspecified"),
                (w, "Scrubber Sludge", "kg", ""),
                (e, "Fine Dust", "kg", "emission/air/unspecified"),
            };

            int count = 0;
            foreach (var (type, name, unit, category) in flowList) {
                var (refUnit, factor) = RefUnitOf(unit);
                var flow = await Examples.FindFlow(channel, type, name, refUnit, category);
                if (flow == null)
                    continue;
                count++;
                var mapEntry = new ProtoFlowMapEntry {
                    From = new ProtoFlowMapRef {
                        Flow = new ProtoRef {
                            Id = $"{type}/{name}/{unit}/{category}",
                            Name = name,
                            RefUnit = unit,
                        },
                        Unit = new ProtoRef { Name = unit }
                    },
                    To = new ProtoFlowMapRef {
                        Flow = flow,
                        Unit = new ProtoRef { Name = refUnit },
                    },
                    ConversionFactor = factor
                };
                mapping.Mappings.Add(mapEntry);
                if (type != ProtoFlowType.ElementaryFlow) {
                    var provider = await FindProvider(flow);
                    if (provider != null) {
                        mapEntry.To.Provider = provider;
                    }
                }
            }
            Log($"  .. created {count} mappings");
            mappingService.Put(mapping);
            Log($"  .. saved mapping {mapping.Name}");
            return true;
        }

        private (string, double) RefUnitOf(string unit) {
            switch (unit) {
                case "t": return ("kg", 1000);
                case "kWh": return ("MJ", 3.6);
                default: return (unit, 1.0);
            }
        }

        private async Task<ProtoRef> FindProvider(ProtoRef flow) {
            Log($"  .. search providers for {flow.Name}");
            var providers = dataService.GetProvidersFor(flow).ResponseStream;
            if (await providers.MoveNext()) {
                var provider = providers.Current;
                Log($"  .. found provider {provider.Name}");
                return provider;
            }
            return null;
        }
    }
}
