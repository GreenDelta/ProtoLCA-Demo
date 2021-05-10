using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;
using Service = ProtoLCA.Services.ResultService.ResultServiceClient;

namespace DemoApp {

    /// <summary>
    /// In this example some random result is calculated and then the impact
    /// factors of that result are queried.
    /// </summary>
    class ResultImpactFactorExample : Example {
        private readonly Channel channel;
        private readonly Service results;

        public ResultImpactFactorExample(Channel channel) {
            this.channel = channel;
            this.results = new Service(channel);
        }

        public string Description() {
            return "Get the applied impact factors of a result";
        }

        public void Run() {
            Exec().Wait();
        }

        private async Task<bool> Exec() {
            var result = await Examples.CalculateSomeProcessResult(channel);
            if (result == null)
                return false;

            // select an indicator
            var impact = await SelectImpact(result);
            if (impact == null)
                return false;

            // collect the factors from the result
            var factors = results.GetImpactFactors(new ImpactFactorRequest {
                Result = result,
                Indicator = impact
            }).ResponseStream;
            var nonZeros = new List<ImpactFactorResponse>();
            while (await factors.MoveNext()) {
                var factor = factors.Current;
                if (factor.Value == 0 || factor.Flow == null)
                    continue;
                nonZeros.Add(factor);
            }
            Log($"  .. {nonZeros.Count} non-zero factors");

            // print the first ten factors
            int i = 0;
            foreach (var factor in nonZeros) {
                i++;
                if (i > 10)
                    break;
                var flow = factor.Flow.Flow;
                var name = flow.Name;
                var unit = $"{impact.RefUnit} / {flow.RefUnit}";
                var category = "/";
                if (flow.CategoryPath != null) {
                    category = flow.CategoryPath.Join("/");
                }
                Log($"  .. {name} ({category}): {factor.Value} {unit}");
            }
            if (i < nonZeros.Count) {
                Log($"  .. {nonZeros.Count - i} more");
            }

            results.Dispose(result);
            return true;
        }

        private async Task<Ref> SelectImpact(Result result) {
            Log("  .. select an impact category from the result");
            var impacts = results.GetImpactCategories(result).ResponseStream;
            var collected = new List<Ref>();
            while (await impacts.MoveNext()) {
                collected.Add(impacts.Current);
            }

            if (collected.Count == 0) {
                Log("  .. => no impact category found in result");
                return null;
            }

            Log($"  .. the result has {collected.Count} impact categories");
            var idx = new Random().Next(0, collected.Count);
            var selected = collected[idx];
            Log($"  .. selected {selected.Name}");
            return selected;
        }

    }
}
