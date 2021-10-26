using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp {
    class TechProcessExample : Example {

        private readonly Channel channel;

        public TechProcessExample(Channel channel) {
            this.channel = channel;
        }

        public string Description() {
            return "Creates a process from some available products in the database.";
        }

        public void Run() {
            Exec().Wait();
        }

        private async Task<bool> Exec() {

            var fetch = new DataFetchService.DataFetchServiceClient(channel);
            var update = new DataUpdateService.DataUpdateServiceClient(channel);

            Log("  ..  get providers / tech-flows from database");
            var techFlowStream = fetch.GetTechFlows(new Empty()).ResponseStream;
            var techFlows = new List<ProtoTechFlow>();
            while (await techFlowStream.MoveNext()) {
                var techFlow = techFlowStream.Current;
                if (techFlow != null) {
                    techFlows.Add(techFlow);
                }
            }
            Log($"  .. collected {techFlows.Count} tech-flows");
            if (techFlows.Count == 0) {
                Log("  no tech-flows in database; skip example");
                return false;
            }

            var timeStamp = DateTime.UtcNow.ToString(
               "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
            var process = new ProtoProcess {
                Type = ProtoType.Process,
                Id = Guid.NewGuid().ToString(),
                Name = "TechProcessExample " + timeStamp,
                Version = "00.00.001",
                LastChange = timeStamp,
                ProcessType = ProtoProcessType.UnitProcess,
            };
            Log($"  .. create process {process.Name}");

            var rand = new Random();
            var refIdx = rand.Next(0, techFlows.Count);
            var refFlow = techFlows[refIdx];

            var qRef = new ProtoExchange {
                InternalId = 1,
                Flow = refFlow.Product != null
                    ? refFlow.Product
                    : refFlow.Waste,
                Amount = 1.0,
                Input = refFlow.Waste != null,
                QuantitativeReference = true,
            };
            process.Exchanges.Add(qRef);

            for (int i = 0; i < 10; i++) {
                var nextIdx = rand.Next(0, techFlows.Count);
                var nextFlow = techFlows[nextIdx];
                var exchange = new ProtoExchange {
                    InternalId = 2 + i,
                    Flow = nextFlow.Product != null
                        ? nextFlow.Product
                        : nextFlow.Waste,
                    Amount = rand.NextDouble(),
                    Input = nextFlow.Product != null,
                    DefaultProvider = nextFlow.Process,
                };
                process.Exchanges.Add(exchange);
            }

            Log($"  .. insert process {process.Name}");
            update.Put(new ProtoDataSet { Process = process });

            return true;
        }
    }
}
