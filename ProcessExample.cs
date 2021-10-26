using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;

using static DemoApp.Util;
using DataFetchService = ProtoLCA.Services.DataFetchService.DataFetchServiceClient;
using DataUpdateService = ProtoLCA.Services.DataUpdateService.DataUpdateServiceClient;

namespace DemoApp {

    /// <summary>
    /// This example creates an example process. It uses the mapping file that
    /// was created in the mapping example if this is available. Otherwise it
    /// creates new flows.
    /// </summary>
    class ProcessExample : Example {

        private readonly Channel channel;
        private readonly DataFetchService fetchService;
        private readonly DataUpdateService updateService;
        private List<(ProtoUnit, ProtoUnitGroup, ProtoFlowProperty)> unitTriples;

        public ProcessExample(Channel channel) {
            this.channel = channel;
            this.fetchService = new DataFetchService(channel);
            this.updateService = new DataUpdateService(channel);
        }

        public string Description() {
            return "Create an example process using flow mappings if possible";
        }

        public void Run() {
            // we could load this in the constructor but we do it here to
            // avoid log outputs when the example is created
            this.unitTriples = Examples.GetUnitTriples(channel);
            Exec().Wait();
        }

        private async Task<bool> Exec() {
            var mapping = await Examples.GetExampleFlowMap(channel);

            // the inputs and outputs of our example
            var p = ProtoFlowType.ProductFlow;
            var w = ProtoFlowType.WasteFlow;
            var e = ProtoFlowType.ElementaryFlow;
            var i = true; // is input
            var o = false; // is output
            var ioList = new List<(bool, ProtoFlowType, string, string, double, string)> {
                (i, p, "Air Blast", "", 245.8751543969349, "t"),
                (i, w, "Combustion Air", "", 59.764430236449158, "t"),
                (i, p, "Hematite Pellets", "", 200, "t"),
                (i, p, "Coke", "", 50, "t"),
                (i, p, "Limestone", "", 30.422441963816247, "t"),
                (i, w, "Steel Scrap", "", 1.8853256607049331, "t"),
                (i, p, "Reductant", "", 16, "t"),
                (i, p, "Washing Solution", "", 75, "t"),

                (o, w, "Slag", "", 33.573534216580185, "t"),
                (o, e, "Carbon dioxide", "emission/air/unspecified", 140.44236409682583, "t"),
                (o, e, "Water vapour", "emission/air/unspecified", 30.591043638569072, "t"),
                (o, e, "Sulfur dioxide", "emission/air/unspecified", 0.01134867565288134, "t"),
                (o, e, "Air", "emission/air/unspecified", 158.58576460676247, "t"),
                (o, p, "Pig Iron", "", 138.2370620852756, "t"),
                (o, w, "Heat Loss", "", 32727.272727272728, "kWh"),
                (o, e, "Coarse Dust", "emission/air/unspecified", 1.4340290871696806, "kg"),
                (o, w, "Scrubber Sludge", "", 56.261517810249792, "kg"),
                (o, e, "Fine Dust", "emission/air/unspecified", 0.18398927491951844, "kg"),
            };

            var process = InitProcess();
            foreach (var (isInput, type, name, category, amount, unit) in ioList) {
                var exchange = new ProtoExchange {
                    InternalId = process.Exchanges.Count + 1,
                    Input = isInput,
                    QuantitativeReference = !isInput && type == ProtoFlowType.ProductFlow,
                };

                var flowId = $"{type}/{name}/{unit}/{category}";
                var mapEntry = FindMapEntry(flowId, mapping);
                if (mapEntry != null) {

                    // create the exchange from a mapped flow
                    exchange.Flow = mapEntry.To.Flow;
                    exchange.FlowProperty = mapEntry.To.FlowProperty;
                    exchange.Unit = mapEntry.To.Unit;
                    exchange.Amount = mapEntry.ConversionFactor * amount;

                    if (mapEntry.To.Provider != null
                        && ((isInput && type == ProtoFlowType.ProductFlow)
                        || (!isInput && type == ProtoFlowType.WasteFlow))) {
                        exchange.DefaultProvider = mapEntry.To.Provider;
                    }
                } else {

                    var (flowRef, unitObj, propObj) = GetOrCreateFlow(type, name, unit);
                    if (flowRef == null)
                        continue;
                    exchange.Flow = flowRef;
                    exchange.FlowProperty = new ProtoRef {
                        Type = ProtoType.FlowProperty,
                        Id = propObj.Id,
                        Name = propObj.Name,
                    };
                    exchange.Unit = new ProtoRef {
                        Type = ProtoType.Unit,
                        Id = unitObj.Id,
                        Name = unitObj.Name,
                    };
                    exchange.Amount = amount;
                }

                if (exchange.Flow != null) {
                    process.Exchanges.Add(exchange);
                }
            }

            var processRef = updateService.Put(new ProtoDataSet { Process = process });
            Log($"  .. created process {processRef.Name}");
            return true;
        }

        private ProtoProcess InitProcess() {
            var timeStamp = DateTime.UtcNow.ToString(
                "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
            return new ProtoProcess {
                Type = ProtoType.Process,
                Id = Guid.NewGuid().ToString(),
                Name = "Proto-Example " + timeStamp,
                Version = "00.00.001",
                LastChange = timeStamp,
                ProcessType = ProtoProcessType.UnitProcess,
            };
        }

        private ProtoFlowMapEntry FindMapEntry(string flowId, ProtoFlowMap mapping) {
            if (mapping.Mappings == null)
                return null;
            foreach (var entry in mapping.Mappings) {
                if (entry.From == null
                    || entry.From.Flow == null
                    || entry.To == null
                    || entry.To.Flow == null)
                    continue;
                if (string.Equals(flowId, entry.From.Flow.Id))
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// Get or create a flow from the given attributes. Note that this is
        /// just an example. Typically you would also include the flow category
        /// and maybe other attributes.
        /// </summary>
        private (ProtoRef, ProtoUnit, ProtoFlowProperty) GetOrCreateFlow(
            ProtoFlowType type, String name, String unitName) {

            // find the unit and flow property
            ProtoFlowProperty flowProperty = null;
            ProtoUnit unit = null;
            foreach (var (tUnit, _, tProperty) in unitTriples) {
                if (string.Equals(unitName, tUnit.Name)) {
                    flowProperty = tProperty;
                    unit = tUnit;
                    break;
                }
            }

            if (unit == null || flowProperty == null) {
                Log("  .. WARNING could not find unit and flow property" +
                    $" for '{unitName}'");
                return (null, null, null);
            }

            // try to find an existing flow
            var bytes = MD5.Create().ComputeHash(
                Encoding.UTF8.GetBytes(
                    $"{type}/{name}/{unit.Id}{flowProperty.Id}"));
            var id = new Guid(bytes).ToString();
            var ds = fetchService.Find(new FindRequest {
                Type = ProtoType.Flow,
                Id = id,
            });
            if (ds.Flow != null) {
                var existing = new ProtoRef {
                    Type = ProtoType.Flow,
                    Id = ds.Flow.Id,
                    Name = ds.Flow.Name,
                };
                return (existing, unit, flowProperty);
            }

            // create a new flow
            var flow = new ProtoFlow {
                Type = ProtoType.Flow,
                Id = id,
                Name = name,
                Version = "00.00.001",
                FlowType = type
            };
            flow.FlowProperties.Add(new ProtoFlowPropertyFactor {
                ReferenceFlowProperty = true,
                ConversionFactor = 1.0,
                FlowProperty = new ProtoRef {
                    Type = ProtoType.FlowProperty,
                    Id = flowProperty.Id,
                    Name = flowProperty.Name,
                },
            });
            var flowRef = updateService.Put(new ProtoDataSet { Flow = flow });
            Log($"  .. created new flow {flowRef.Name}");
            return (flowRef, unit, flowProperty);
        }
    }
}
