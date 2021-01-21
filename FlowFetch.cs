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
        private readonly UnitIndex units;

        private FlowFetch(Channel chan, FlowMap flowMap, UnitIndex units)
        {
            data = new DataService(chan);
            mappings = new FlowMapService(chan);
            this.flowMap = flowMap;
            this.units = units;
        }

        async public static Task<FlowFetch> Create(Channel chan, string mapping)
        {
            var flowMap = GetFlowMap(mapping, chan);
            var units = await UnitIndex.Build(chan);
            return new FlowFetch(chan, flowMap, units);
        }

        private static FlowMap GetFlowMap(string name, Channel chan)
        {
            var service = new FlowMapService(chan);
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

        public async Task<FlowMapEntry> GetFlow(FlowQuery query)
        {
            Log($"Handle a flow query: {query}");

            // first try to find a flow from the mapping
            var entry = query.FindEntryIn(flowMap);
            if (entry != null)
            {
                Log($"  Found mapping entry {query.FlowID}");
                return entry;
            }

            // check if the unit is known
            var unitEntry = units.EntryOf(query.Unit);
            if (unitEntry == null)
            {
                Log($"  ERROR: Unknown unit {query.Unit} => no flow");
                return null;
            }

            // search in the database for a matching flow or create a
            // new one if no matching flow can be found
            var flow = await Search(query);
            if (flow != null)
            {
                Log($"  Found matching flow {flow.Name}:{flow.Id}");
            }
            else
            {
                flow = Build.FlowOf(
                    query.Name,
                    query.Type,
                    unitEntry.FlowProperty);
                Log($"  Created new flow");
            }

            // finally, update the mapping entry
            // TODO: improve the mapping entry
            var mapping = new FlowMapEntry
            {
                ConversionFactor = unitEntry.Factor,
                From = query.ToFlowMapRef(),
                To = new FlowMapRef
                {
                    Flow = Build.RefOf(flow.Id, flow.Name),
                }
            };
            flowMap.Mappings.Add(mapping);
            mappings.Put(flowMap);
            Log($"  Updated flow mapping {flowMap.Name}");

            // TODO: search for providers in case of
            // technosphere flows

            return mapping;
        }

        private async Task<Flow> Search(FlowQuery query)
        {
            var search = data.Search(new SearchRequest
            {
                Type = ModelType.Flow,
                Query = query.Name,
            }).ResponseStream;
            Ref candiate = null;
            while (await search.MoveNext())
            {
                var next = search.Current;
                if (next.FlowType != query.Type)
                    continue;
                if (!IsBetterMatch(candiate, next, query))
                    continue;
                // the units have to be convertible
                if (!units.AreConvertible(query.Unit, next.RefUnit))
                    continue;
                candiate = next;
            }

            // load the flow from the database
            if (candiate == null)
                return null;
            var status = data.GetFlow(candiate);
            return status.Ok ? status.Flow : null;
        }

        // Try to determine if the given candidate is a better match than
        // the current flow regarding the name and category path.
        private bool IsBetterMatch(
            Ref current, Ref candidate, FlowQuery query)
        {
            if (current == null)
                return true;

            // compare the names
            var words = query.Name.Split(' ');
            int currentScore = current.Name.MatchLengthOf(words);
            int candidateScore = candidate.Name.MatchLengthOf(words);
            if (candidateScore != currentScore
                || string.IsNullOrWhiteSpace(query.Category))
                return candidateScore > currentScore;

            // compare the categories
            words = query.Category.Split('/');
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
