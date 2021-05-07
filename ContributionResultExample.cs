using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;
using Service = ProtoLCA.Services.ResultService.ResultServiceClient;

namespace DemoApp
{
    class ContributionResultExample : Example
    {
        private readonly Channel channel;
        private readonly Service results;

        public ContributionResultExample(Channel channel)
        {
            this.channel = channel;
            this.results = new Service(channel);
        }

        public string Description()
        {
            return "Calling Get*Contribution: calculate process contribution results";
        }

        public void Run()
        {
            Exec().Wait();
        }

        private async Task<bool> Exec()
        {
            var result = await Examples.CalculateSomeProcessResult(channel);
            if (result == null)
                return false;

            // select a random tech-flow and impact category from the result
            // for which we calculate the contributions
            var techFlow = await SelectTechFlow(result);
            var impact = await SelectImpact(result);
            if (techFlow == null || impact == null)
                return false;

            Log("  .. get contributions of "
                + $"{techFlow.Process.Name} to {impact.Name}");

            var req = new TechFlowContributionRequest
            {
                Result = result,
                TechFlow = techFlow,
                Impact = impact,
            };
            var direct = results.GetDirectContribution(req);
            Log($"  .. direct: {direct.Value} {impact.RefUnit}");
            var total = results.GetTotalContribution(req);
            Log($"  .. total: {total.Value} {impact.RefUnit}");
            var totalOfOne = results.GetTotalContributionOfOne(req);
            Log($"  .. total of 1: {totalOfOne.Value} {impact.RefUnit}");

            results.Dispose(result);
            return true;
        }

        private async Task<TechFlow> SelectTechFlow(Result result)
        {
            Log("  .. select a tech-flow from the supply chain");
            var techFlows = results.GetTechFlows(result).ResponseStream;
            var collected = new List<TechFlow>();
            while (await techFlows.MoveNext())
            {
                collected.Add(techFlows.Current);
            }

            if (collected.Count == 0)
            {
                Log("=> no tech flow found in result");
                return null;
            }

            Log($"  .. the system has {collected.Count} tech-flows");
            var idx = new Random().Next(0, collected.Count);
            var selected = collected[idx];
            Log($"  .. selected {selected.Process.Name}");
            return selected;
        }

        private async Task<Ref> SelectImpact(Result result)
        {
            Log("  .. select an impact category from the result");
            var impacts = results.GetImpactCategories(result).ResponseStream;
            var collected = new List<Ref>();
            while (await impacts.MoveNext())
            {
                collected.Add(impacts.Current);
            }

            if (collected.Count == 0)
            {
                Log("=> no impact category found in result");
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
