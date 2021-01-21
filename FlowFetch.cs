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

        public async Task<Tuple<Ref, double>> ElementaryFlow(
            string name, string unit, string category = "")
        {
            var info = $"{name}/{unit}/{category}";
            var flowID = MakeID(name, unit, category);

            // first try to find a flow from the mapping
            foreach (var m in flowMap.Mappings)
            {
                if (flowID.Equals(m.From.Flow.Id))
                {
                    Log($"Found mapping for {info}");
                    return Tuple.Create(m.To.Flow, m.ConversionFactor);
                }
            }

            // if there is no mapping for the flow, search if we
            // can find a matching flow in the database
            // first check if the unit is known
            var unitEntry = units.EntryOf(unit);
            if (unitEntry == null)
            {
                Log($"ERROR: Unknown unit {unit}; unmapped flow {info}");
                return null;
            }

            // run the search for the flow name and try to find
            // the best candidate
            var search = data.Search(new SearchRequest
            {
                Type = ModelType.Flow,
                Query = name,
            }).ResponseStream;
            Ref candiate = null;
            while (await search.MoveNext())
            {
                var next = search.Current;
                if (next.FlowType != FlowType.ElementaryFlow)
                    continue;
                if (!IsBetterMatch(candiate, next, name, category))
                    continue;
                // the units have to be convertible
                if (!units.AreConvertible(unit, next.RefUnit))
                    continue;
                candiate = next;
            }

            // if we found a matching flow, we add it to the mapping
            if (candiate != null)
            {
                Log($"Found matching flow {candiate.Id} for {info}");
                var mapEntry = new FlowMapEntry
                {
                    ConversionFactor = unitEntry.Factor,
                    From = new FlowMapRef
                    {
                        Flow = new Ref
                        {
                            Id = flowID,
                            Name = name,
                            FlowType = FlowType.ElementaryFlow,
                            RefUnit = unit,
                        },
                        Unit = new Ref { Id = unit, Name = unit },
                    },
                    To = new FlowMapRef
                    {
                        Flow = candiate,
                    }
                };
                flowMap.Mappings.Add(mapEntry);
                mappings.Put(flowMap);
                Log($"Updated flow mapping {flowMap.Name}");

                return Tuple.Create(candiate, unitEntry.Factor);
            }

            // finally, if we cannot find a corresponding flow,
            // we create a new one, and add it to the flow mapping
            var flow = Build.ElementaryFlowOf(name, unitEntry.FlowProperty);
            Log($"Created new flow for {info}");
            var flowRef = Build.RefOf(flow.Id, flow.Name);
            flowRef.FlowType = FlowType.ElementaryFlow;
            var newEntry = new FlowMapEntry
            {
                ConversionFactor = unitEntry.Factor,
                From = new FlowMapRef
                {
                    Flow = new Ref
                    {
                        Id = flowID,
                        Name = name,
                        FlowType = FlowType.ElementaryFlow,
                        RefUnit = unit,
                    },
                    Unit = new Ref { Id = unit, Name = unit },
                },
                To = new FlowMapRef
                {
                    Flow = flowRef,
                },
            };
            flowMap.Mappings.Add(newEntry);
            mappings.Put(flowMap);
            Log($"Updated flow mapping {flowMap.Name}");

            return Tuple.Create(flowRef, unitEntry.Factor);
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
