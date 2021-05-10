﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using Google.Protobuf.WellKnownTypes;
using static DemoApp.Util;
using DataService = ProtoLCA.Services.DataFetchService.DataFetchServiceClient;
using MappingService = ProtoLCA.Services.FlowMapService.FlowMapServiceClient;

namespace DemoApp {
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
            var p = FlowType.ProductFlow;
            var w = FlowType.WasteFlow;
            var e = FlowType.ElementaryFlow;
            var flowList = new List<(FlowType, string, string, string)> {
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
                var mapEntry = new FlowMapEntry {
                    From = new FlowMapRef {
                        Flow = new Ref {
                            Id = $"{type}/{name}/{unit}/{category}",
                            Name = name,
                            RefUnit = unit,
                        },
                        Unit = new Ref { Name = unit }
                    },
                    To = new FlowMapRef {
                        Flow = flow,
                        Unit = new Ref { Name = refUnit },
                    },
                    ConversionFactor = factor
                };
                mapping.Mappings.Add(mapEntry);
                if (type != FlowType.ElementaryFlow) {
                    var provider = await findProvider(flow);
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

        private async Task<Ref> findProvider(Ref flow) {
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
