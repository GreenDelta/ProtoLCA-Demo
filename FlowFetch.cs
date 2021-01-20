using System;

using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;

using FlowMapService = ProtoLCA.Services.FlowMapService.FlowMapServiceClient;
using DataService = ProtoLCA.Services.DataService.DataServiceClient;

using static DemoApp.Util;

namespace DemoApp
{
    class FlowFetch
    {
        private readonly FlowMap flowMap;
        private readonly DataService data;
        private readonly FlowMapService mappings;

        public FlowFetch(Channel chan, string mappingName)
        {
            data = new DataService(chan);
            mappings = new FlowMapService(chan);
            flowMap = GetFlowMap(mappingName, mappings);
        }

        private FlowMap GetFlowMap(string name, FlowMapService service)
        {
            var status = service.Get(new FlowMapInfo
            {
                Name = name
            });

            if (status.Ok)
                return status.FlowMap;
            var map = new FlowMap
            {
                Name = name,
                Id = Guid.NewGuid().ToString()
            };
            return map;
        }

        public async Task<(Ref, double)> ElementaryFlow(
            string name, string unit, string category = "")
        {
            var info = $"{name}/{unit}/{category}";
            var flowID = MakeID(name, unit, category);

            // try to find a mapped flow
            foreach (var m in flowMap.Mappings)
            {
                if (flowID.Equals(m.From.Flow.Id))
                {
                    Console.WriteLine($"Found mapping for {info}");
                    return (m.To.Flow, m.ConversionFactor);
                }
            }

            // search for a flow
            var search = data.Search(new SearchRequest
            {
                Type = ModelType.Flow,
                Query = name,
            }).ResponseStream;
            Ref candidate = null;
            while (await search.MoveNext())
            {
                // check the unit and the compartment
                if (IsBetterMatch(candidate, search.Current, name, category))
                {
                    candidate = search.Current;
                }
            }

            if (candidate != null)
            {
                Console.WriteLine(
                    $"Found matching flow {candidate.Id} for {info}");
            }


            return (candidate, 1.0);

        }

        // Try to determine if the given candidate is a better match than
        // the current flow regarding the name and category path.
        private bool IsBetterMatch(
            Ref current, Ref candidate, string name, string category)
        {
            if (current == null)
                return true;

            // compare the names
            var words = name.Split(' ');
            int currentScore = current.Name.MatchLengthOf(words);
            int candidateScore = candidate.Name.MatchLengthOf(words);
            if (candidateScore != currentScore
                || string.IsNullOrWhiteSpace(category))
                return candidateScore > currentScore;

            // compare the categories
            words = category.Split('/');
            currentScore = current
                .CategoryPath.Join("/")
                .MatchLengthOf(words);
            candidateScore = candidate
                .CategoryPath.Join("/")
                .MatchLengthOf(words);
            return candidateScore > currentScore;
        }
    }
}
