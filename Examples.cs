using System;
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
using FlowMapService = ProtoLCA.Services.FlowMapService.FlowMapServiceClient;


namespace DemoApp {
    public static class Examples {

        // Selects a random descriptor of the given type from the database.
        public static async Task<Ref> GetSomeDescriptorOf(
            Channel channel, ModelType type) {
            Log($"  .. fetch all descriptors of type {type}");
            var service = new DataService(channel);
            var descriptors = service.GetDescriptors(new GetDescriptorsRequest {
                ModelType = type
            }).ResponseStream;

            var collected = new List<Ref>();
            while (await descriptors.MoveNext()) {
                collected.Add(descriptors.Current);
            }
            if (collected.Count == 0) {
                Log($"  .. no data set of type {type} found");
                return null;
            }

            var idx = new Random().Next(0, collected.Count);
            var selected = collected[idx];
            Log($"  .. selected {type} {selected.Name}");
            return selected;
        }

        public static async Task<FlowMap> GetExampleFlowMap(Channel channel) {
            var service = new FlowMapService(channel);
            var name = "ProtoLCA-Example-Mapping.csv";
            Log($"  .. try to find example flow map '{name}'");
            var infos = service.GetAll(new Empty()).ResponseStream;
            while (await infos.MoveNext()) {
                var info = infos.Current;
                if (name.EqualsIgnoreCase(info.Name)) {
                    Log("  .. found it");
                    return service.Get(info);
                }
            }
            Log("  .. does not exist; initialized new");
            var map = new FlowMap {
                Name = name,
                Id = Guid.NewGuid().ToString()
            };
            return map;
        }

        // Tries to calculate the result of some process in the database.
        public static async Task<Result> CalculateSomeProcessResult(
            Channel channel) {
            var process = await GetSomeDescriptorOf(channel, ModelType.Process);
            if (process == null) {
                Log("=> database has no processes; cannot calculate result");
                return null;
            }
            Log($"  .. try to calculate result of process {process.Name}");

            var method = await GetSomeDescriptorOf(
                channel, ModelType.ImpactMethod);
            if (method != null) {
                Log($"  .. calculate results with LCIA method {method.Name}");
            } else {
                Log("  .. calculate results without LCIA method");
            }
            var setup = new CalculationSetup {
                ProductSystem = process,
                ImpactMethod = method,
                // calculate it for 1 unit of the reference flow
                // of the process
                Amount = 1.0,
            };
            var results = new ResultService.ResultServiceClient(channel);
            return results.Calculate(setup);
        }

    }
}
