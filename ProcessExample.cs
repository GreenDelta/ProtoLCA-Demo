using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            var data = new DataService.DataServiceClient(chan);

            // set the location
            var process = Build.ProcessOf("Iron Process - Gas cleaning");
            var location = GetLocation(data, "Global");
            process.Location = new Ref
            {
                Id = location.Id,
                Name = location.Name,
            };

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
            var insertStatus = data.PutProcess(process);
            if (!insertStatus.Ok)
                throw new Exception(insertStatus.Error);

            // calculation

            // select the first best LCIA method if it exsists
            Ref method = null;
            var methods = data.GetDescriptors(
                new DescriptorRequest { Type = ModelType.ImpactMethod })
                .ResponseStream;
            if (await methods.MoveNext())
            {
                method = methods.Current;
                Log($"Selected LCIA method: {method.Name}");
            }

            var setup = new CalculationSetup
            {
                Amount = qRef.Amount,
                ProductSystem = new Ref { Id = process.Id },
                ImpactMethod = method,
                FlowProperty = qRef.FlowProperty,
                Unit = qRef.Unit,
            };

            Log("Calculate results ...");
            var results = new ResultService.ResultServiceClient(chan);
            var resultStatus = results.Calculate(setup);
            if (!resultStatus.Ok)
            {
                Log($"ERROR: Failed to calculate " +
                    $"result: {resultStatus.Error}");
                return;
            }

            var impacts = results.GetImpacts(resultStatus.Result)
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

            results.Dispose(resultStatus.Result);
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

        private static Location GetLocation(
            DataService.DataServiceClient client,
            string name)
        {
            var status = client.GetLocation(Build.RefOf(name: name));
            if (status.Ok)
                return status.Location;
            var location = Build.LocationOf(name);
            var insertStatus = client.PutLocation(location);
            if (!insertStatus.Ok)
                throw new Exception(insertStatus.Error);
            return location;
        }
    }
}
