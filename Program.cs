using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Google.Protobuf;
using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;

using static DemoApp.Util;

namespace DemoApp
{
    class Program
    {
        static void Main()
        {
            var chan = new Channel("localhost:8080", ChannelCredentials.Insecure);
            Task.Run(() => CreateExampleProcess(chan)).Wait();
            // Task.Run(() => CreateExampleFlows(chan)).Wait();
            // Task.Run(() => PrintAllMappingFiles(chan)).Wait();
            Console.ReadKey();
        }

        private static async void CreateExampleFlows(Channel chan)
        {
            var flows = await FlowFetch.Create(chan, "ProtoLCA-Demo.csv");

            // this should find a matching flow in the reference data
            await flows.GetFlow(
                FlowQuery.ForElementary("Carbon dioxide")
                .WithUnit("g")
                .WithCategory("air/unspecified"));

            // this should create a new flow
            await flows.GetFlow(
                FlowQuery.ForElementary("SARS-CoV-2 viruses")
                .WithUnit("Item(s)")
                .WithCategory("air/urban"));

        }

        private static async void PrintAllMappingFiles(Channel chan)
        {
            var service = new FlowMapService.FlowMapServiceClient(chan);
            var mappings = service.GetAll(new Empty()).ResponseStream;
            while (await mappings.MoveNext())
            {
                var name = mappings.Current.Name;
                Log($"Found mapping: {name}");
            }
        }

        private static async void CreateExampleProcess(Channel chan)
        {
            var flows = await FlowFetch.Create(chan, "ProtoLCA-Demo.csv");
            var client = new DataService.DataServiceClient(chan);

            // get flow property mass
            var status = client.GetFlowProperty(Build.RefOf(name: "Mass"));
            if (!status.Ok)
                throw new Exception(status.Error);
            var mass = status.FlowProperty;

            // set the location
            var process = Build.ProcessOf("Iron Process - Gas cleaning");
            var location = GetLocation(client, "Global");
            process.Location = Build.RefOf(id: location.Id, name: location.Name);

            // add inputs
            var inputs = new List<(string, FlowType, double)> {
                ("Air Blast", FlowType.ProductFlow, 245.8751543969349),
                ("Combustion Air", FlowType.WasteFlow, 59.764430236449158),
                ("Hematite Pellets", FlowType.ProductFlow, 200),
                ("Coke", FlowType.ProductFlow, 50),
                ("Limestone", FlowType.ProductFlow, 30.422441963816247),
                ("Steel Scrap", FlowType.WasteFlow, 1.8853256607049331),
                ("Reductant", FlowType.ProductFlow, 16),
                ("Washing Solution", FlowType.ProductFlow, 75),
            };
            foreach (var (name, type, amount) in inputs)
            {
                var mapping = await flows.GetFlow(FlowQuery.For(type, name));
                if (mapping == null)
                    continue;
                var e = ToExchange(amount, mapping);
                process.LastInternalId += 1;
                e.InternalId = process.LastInternalId;
                e.Input = true;
                process.Exchanges.Add(e);
            }

            // add outputs
            var outputs = new List<(string, FlowType, double)>
            {
                ("Slag", FlowType.WasteFlow, 33.573534216580185),
                ("Carbon dioxide", FlowType.ElementaryFlow, 140.44236409682583),
                ("Water vapour", FlowType.ElementaryFlow, 30.591043638569072),
                ("Sulfur dioxide", FlowType.ElementaryFlow, 0.01134867565288134),
                ("Air", FlowType.ElementaryFlow, 158.58576460676247),
                ("Pig Iron", FlowType.ProductFlow, 138.2370620852756),
                ("Heat Loss", FlowType.WasteFlow, 32727.272727272728),
                ("Coarse Dust", FlowType.ElementaryFlow, 1.4340290871696806),
                ("Scrubber Sludge", FlowType.WasteFlow, 56.261517810249792),
                ("Fine Dust", FlowType.ElementaryFlow, 0.18398927491951844),
            };
            foreach (var (name, type, amount) in outputs)
            {
                var mapping = await flows.GetFlow(FlowQuery.For(type, name));
                if (mapping == null)
                    continue;
                var e = ToExchange(amount, mapping);
                process.LastInternalId += 1;
                e.InternalId = process.LastInternalId;
                e.Input = false;
                process.Exchanges.Add(e);
            }

            var insertStatus = client.PutProcess(process);
            if (!insertStatus.Ok)
                throw new Exception(insertStatus.Error);
        }

        private static Exchange ToExchange(double amount, FlowMapEntry mapping)
        {
            var target = mapping.To;
            var e = new Exchange
            {
                Amount = amount,
                Flow = target.Flow,
                FlowProperty = target.FlowProperty,
                Unit = target.Unit,
            };
            return e;
        }

        private static Flow GetFlow(
            DataService.DataServiceClient client,
            string name,
            FlowType flowType,
            FlowProperty quantity)
        {
            var status = client.GetFlow(Build.RefOf(name: name));
            if (status.Ok)
                return status.Flow;
            var flow = Build.FlowOf(name, flowType, quantity);
            var insertStatus = client.PutFlow(flow);
            if (!insertStatus.Ok)
                throw new Exception(insertStatus.Error);
            return flow;
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
