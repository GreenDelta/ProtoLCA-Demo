using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;

using static DemoApp.Util;

namespace DemoApp
{
    class ProcessExample
    {
        internal static async void Run(Channel chan)
        {
            var flows = await FlowFetch.Create(chan, "ProtoLCA-Demo.csv");
            var fetch = new DataFetchService.DataFetchServiceClient(chan);
            var update = new DataUpdateService.DataUpdateServiceClient(chan);

            // set the location
            var process = Build.ProcessOf("Iron Process - Gas cleaning");
            process.Location = await GetLocation(chan, "Global");

            // add the exchanges
            Exchange qRef = null;
            foreach (var e in GetExampleExchanges())
            {
                var (isInput, type, name, amount, unit) = e;

                // find and map the flow
                var flowQuery = FlowQuery.For(type, name).WithUnit(unit);
                if (type == FlowType.ElementaryFlow)
                {
                    flowQuery.WithCategory("air/unspecified");
                }
                else
                {
                    flowQuery.WithLocation("FI");
                }
                var mapping = await flows.Get(flowQuery);
                if (mapping == null)
                    continue;

                // create the input or output
                process.LastInternalId += 1;
                var target = mapping.To;
                var exchange = new Exchange
                {
                    InternalId = process.LastInternalId,
                    Amount = amount * mapping.ConversionFactor,
                    Input = isInput,
                    Flow = target.Flow,
                    FlowProperty = target.FlowProperty,
                    Unit = target.Unit,
                };

                // set the provider for product inputs or waste outputs
                if (((isInput && type == FlowType.ProductFlow)
                    || (!isInput && type == FlowType.WasteFlow))
                    && target.Provider != null)
                {
                    exchange.DefaultProvider = target.Provider;
                }

                // set the quantitative reference
                if (!isInput && type == FlowType.ProductFlow)
                {
                    exchange.QuantitativeReference = true;
                    qRef = exchange;
                }

                process.Exchanges.Add(exchange);
            }

            // insert the process
            var processRef = update.Put(new DataSet { Process = process });

            // calculation

            // select the first best LCIA method if it exsists
            Ref method = null;
            var methods = fetch.GetDescriptors(
                new GetDescriptorsRequest { ModelType = ModelType.ImpactMethod })
                .ResponseStream;
            if (await methods.MoveNext())
            {
                method = methods.Current;
                Log($"Selected LCIA method: {method.Name}");
            }

            var setup = new CalculationSetup
            {
                Amount = qRef.Amount,
                ProductSystem = processRef,
                ImpactMethod = method,
                FlowProperty = qRef.FlowProperty,
                Unit = qRef.Unit,
            };

            Log("Calculate results ...");
            var results = new ResultService.ResultServiceClient(chan);
            var result = results.Calculate(setup);

            var impacts = results.GetImpacts(result)
                .ResponseStream;
            var hasImpacts = false;
            while (await impacts.MoveNext())
            {
                var impact = impacts.Current;
                Log($"{impact.ImpactCategory.Name}: {impact.Value}" +
                    $" {impact.ImpactCategory.RefUnit}");
                hasImpacts = true;
            }

            if (!hasImpacts)
            {
                // TODO: show inventory results

            }

            results.Dispose(result);
        }

        private static List<(bool, FlowType, string, double, string)>
            GetExampleExchanges()
        {
            var p = FlowType.ProductFlow;
            var w = FlowType.WasteFlow;
            var e = FlowType.ElementaryFlow;
            var i = true; // is input
            var o = false; // is output

            return new List<(bool, FlowType, string, double, string)> {
                (i, p, "Air Blast", 245.8751543969349, "t"),
                (i, w, "Combustion Air", 59.764430236449158, "t"),
                (i, p, "Hematite Pellets", 200, "t"),
                (i, p, "Coke", 50, "t"),
                (i, p, "Limestone", 30.422441963816247, "t"),
                (i, w, "Steel Scrap", 1.8853256607049331, "t"),
                (i, p, "Reductant", 16, "t"),
                (i, p, "Washing Solution", 75, "t"),

                (o, w, "Slag", 33.573534216580185, "t"),
                (o, e, "Carbon dioxide", 140.44236409682583, "t"),
                (o, e, "Water vapour", 30.591043638569072, "t"),
                (o, e, "Sulfur dioxide", 0.01134867565288134, "t"),
                (o, e, "Air", 158.58576460676247, "t"),
                (o, p, "Pig Iron", 138.2370620852756, "t"),
                (o, w, "Heat Loss", 32727.272727272728, "kWh"),
                (o, e, "Coarse Dust", 1.4340290871696806, "kg"),
                (o, w, "Scrubber Sludge", 56.261517810249792, "kg"),
                (o, e, "Fine Dust", 0.18398927491951844, "kg"),
            };
        }

        private static async Task<Ref> GetLocation(Channel chan, string name)
        {
            // first try to get the location by name
            var fetch = new DataFetchService.DataFetchServiceClient(chan);
            var descriptors = fetch.GetDescriptors(new GetDescriptorsRequest
            {
                ModelType = ModelType.Location,
                Attributes = new GetDescriptorsRequest.Types.Attributes
                {
                    Name = name
                }
            }).ResponseStream;
            if (await descriptors.MoveNext())
            {
                return descriptors.Current;
            }

            // create a new location and insert it
            var update = new DataUpdateService.DataUpdateServiceClient(chan);
            var location = Build.LocationOf(name);
            return update.Put(new DataSet { Location = location });
        }
    }
}
