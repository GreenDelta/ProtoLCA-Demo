using System;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;

using FlowMapService = ProtoLCA.Services.FlowMapService.FlowMapServiceClient;
using DataService = ProtoLCA.Services.DataService.DataServiceClient;

using static DemoApp.Util;

namespace DemoApp
{

    public class FlowQuery
    {
        public string Name { get; private set; }
        public string Unit { get; private set; }
        public string Category { get; private set; }
        public FlowType Type { get; private set; }
        public string Location { get; private set; }

        private string _id;
        public string FlowID
        {
            get
            {
                if (_id != null)
                    return _id;
                _id = MakeID(this.ToString());
                return _id;
            }
        }

        private FlowQuery(FlowType type)
        {
            this.Type = type;
            this.Name = "";
            this.Unit = "";
            this.Category = "";
            this.Location = "";
        }

        public static FlowQuery ForElementary()
        {
            return new FlowQuery(FlowType.ElementaryFlow);
        }

        public static FlowQuery ForProduct()
        {
            return new FlowQuery(FlowType.ProductFlow);
        }

        public static FlowQuery ForWaste()
        {
            return new FlowQuery(FlowType.WasteFlow);
        }

        public FlowQuery WithName(string name)
        {
            this.Name = name == null
                ? ""
                : name.Trim();
            return this;
        }

        public FlowQuery WithCategory(string category)
        {
            this.Category = category == null
                ? ""
                : category.Trim();
            return this;
        }

        public FlowQuery WithUnit(string unit)
        {
            this.Unit = unit == null
                ? ""
                : unit.Trim();
            return this;
        }

        public FlowQuery WithLocation(string location)
        {
            this.Location = location == null
                ? ""
                : location.Trim();
            return this;
        }

        public override string ToString()
        {
            var s = new StringBuilder();
            var parts = new string[] {
                Type.ToString(),
                Name,
                Unit,
                Location,
                Category };
            for (int i = 0; i < parts.Length; i++)
            {
                if (String.IsNullOrEmpty(parts[i]))
                    continue;
                if (i != 0)
                {
                    s.Append(" - ");
                }
                s.Append(parts[i]);
            }
            return s.ToString();
        }

        public FlowMapRef ToFlowMapRef()
        {
            var mapRef = new FlowMapRef
            {
                Flow = new Ref
                {
                    Id = FlowID,
                    Name = Name,
                    FlowType = Type,
                    RefUnit = Unit,
                },
                Unit = new Ref { Id = Unit, Name = Unit },
            };

            if (!String.IsNullOrEmpty(Location))
            {
                mapRef.Flow.Location = Location;
            }
            return mapRef;
        }
    }

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

        public async Task<Tuple<Ref, double>> ElementaryFlow(FlowQuery query)
        {

            // first try to find a flow from the mapping
            foreach (var m in flowMap.Mappings)
            {
                if (query.FlowID.Equals(m.From.Flow.Id))
                {
                    Log($"Found mapping for {query}");
                    return Tuple.Create(m.To.Flow, m.ConversionFactor);
                }
            }

            // if there is no mapping for the flow, search if we
            // can find a matching flow in the database
            // first check if the unit is known
            var unitEntry = units.EntryOf(query.Unit);
            if (unitEntry == null)
            {
                Log($"ERROR: Unknown unit {query.Unit}; unmapped flow {query}");
                return null;
            }

            // run the search for the flow name and try to find
            // the best candidate
            var search = data.Search(new SearchRequest
            {
                Type = ModelType.Flow,
                Query = query.Name,
            }).ResponseStream;
            Ref candiate = null;
            while (await search.MoveNext())
            {
                var next = search.Current;
                if (next.FlowType != FlowType.ElementaryFlow)
                    continue;
                if (!IsBetterMatch(candiate, next, query))
                    continue;
                // the units have to be convertible
                if (!units.AreConvertible(query.Unit, next.RefUnit))
                    continue;
                candiate = next;
            }

            // if we found a matching flow, we add it to the mapping
            if (candiate != null)
            {
                Log($"Found matching flow {candiate.Id} for {query}");
                var mapEntry = new FlowMapEntry
                {
                    ConversionFactor = unitEntry.Factor,
                    From = query.ToFlowMapRef(),
                    To = new FlowMapRef { Flow = candiate }
                };
                flowMap.Mappings.Add(mapEntry);
                mappings.Put(flowMap);
                Log($"Updated flow mapping {flowMap.Name}");

                return Tuple.Create(candiate, unitEntry.Factor);
            }

            // finally, if we cannot find a corresponding flow,
            // we create a new one, and add it to the flow mapping
            var flow = Build.ElementaryFlowOf(query.Name, unitEntry.FlowProperty);
            Log($"Created new flow for {query}");
            var flowRef = Build.RefOf(flow.Id, flow.Name);
            flowRef.FlowType = FlowType.ElementaryFlow;
            var newEntry = new FlowMapEntry
            {
                ConversionFactor = unitEntry.Factor,
                From = query.ToFlowMapRef(),
                To = new FlowMapRef { Flow = flowRef }
            };
            flowMap.Mappings.Add(newEntry);
            mappings.Put(flowMap);
            Log($"Updated flow mapping {flowMap.Name}");

            return Tuple.Create(flowRef, unitEntry.Factor);
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
