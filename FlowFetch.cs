using System;
using System.Threading.Tasks;

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;

using FlowMapService = ProtoLCA.Services.FlowMapService.FlowMapServiceClient;
using DataFetchService = ProtoLCA.Services.DataFetchService.DataFetchServiceClient;
using DataUpdateService = ProtoLCA.Services.DataUpdateService.DataUpdateServiceClient;

using static DemoApp.Util;

namespace DemoApp
{

    /// <summary>
    /// Demonstrates the mapping, search, and creation of flows.
    /// </summary>
    class FlowFetch
    {
        private readonly FlowMap flowMap;
        private readonly DataFetchService dataFetch;
        private readonly DataUpdateService dataUpdate;
        private readonly FlowMapService mappings;
        private readonly UnitIndex units;

        private FlowFetch(Channel chan, FlowMap flowMap, UnitIndex units)
        {
            dataFetch = new DataFetchService(chan);
            dataUpdate = new DataUpdateService(chan);
            mappings = new FlowMapService(chan);
            this.flowMap = flowMap;
            this.units = units;
        }

        async public static Task<FlowFetch> Create(Channel chan, string mapping)
        {
            var flowMap = await GetFlowMap(mapping, chan);
            var units = await UnitIndex.Build(chan);
            return new FlowFetch(chan, flowMap, units);
        }

        /// <summary>
        /// Get the flow mapping with the given name from the server or create a
        /// new one if it does not exist.
        /// </summary>
        private static async Task<FlowMap> GetFlowMap(string name, Channel chan)
        {
            var service = new FlowMapService(chan);

            // check if the flow map exists and load it if this is the case
            var infos = service.GetAll(new Empty()).ResponseStream;
            while (await infos.MoveNext())
            {
                var info = infos.Current;
                if (name.EqualsIgnoreCase(info.Name))
                {
                    return service.Get(info);
                }
            }

            var map = new FlowMap
            {
                Name = name,
                Id = Guid.NewGuid().ToString()
            };
            return map;
        }

        /// <summary>
        /// Get a mapping entry for the qiven query. First it checks
        /// the mapping file in openLCA for a matching entry. If this
        /// does not exist, it searches for a matching existing flow
        /// and creates it if no matching flow can be found. Finally,
        /// it updates the mapping and returns he corresponding flow
        /// mapping.
        /// </summary>
        public async Task<FlowMapEntry> Get(FlowQuery query)
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
            var flow = await FlowSearch(query);
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
                dataUpdate.Put(new DataSet { Flow = flow });
                Log($"  Created new flow");
            }
            var flowRef = ToRef(flow);

            // if it is a product or waste, also search for an provider
            Ref provider = null;
            if (query.Type != FlowType.ElementaryFlow)
            {
                provider = await ProviderSearch(flowRef, query);
            }

            // finally, update the mapping entry
            var mapping = new FlowMapEntry
            {
                ConversionFactor = unitEntry.Factor,
                From = query.ToFlowMapRef(),
                To = ToMapRef(flow),
            };
            if (provider != null)
            {
                mapping.To.Provider = provider;
            }

            flowMap.Mappings.Add(mapping);
            mappings.Put(flowMap);
            Log($"  Updated flow mapping {flowMap.Name}");

            return mapping;
        }

        private async Task<Flow> FlowSearch(FlowQuery query)
        {
            var search = dataFetch.Search(new SearchRequest
            {
                ModelType = ModelType.Flow,
                Query = query.Name,
            }).ResponseStream;
            Ref candiate = null;
            while (await search.MoveNext())
            {
                var next = search.Current;
                if (next.FlowType != query.Type)
                    continue;
                if (!IsBetterFlow(candiate, next, query))
                    continue;
                // the units have to be convertible
                if (!units.AreConvertible(query.Unit, next.RefUnit))
                    continue;
                candiate = next;
            }

            // load the flow from the database
            if (candiate == null)
                return null;
            var dataSet = dataFetch.Get(new GetRequest
            {
                ModelType = ModelType.Flow,
                Id = candiate.Id
            });
            return dataSet.Flow;
        }

        private async Task<Ref> ProviderSearch(Ref flow, FlowQuery query)
        {
            var search = dataFetch.GetProvidersFor(flow).ResponseStream;
            Ref provider = null;
            Func<Ref, bool> matchesLocation = candidate =>
            {
                return query.Location.IsEmpty()
                        || query.Location.EqualsIgnoreCase(provider.Location);
            };
            while (await search.MoveNext())
            {
                var next = search.Current;
                if (provider == null)
                {
                    provider = next;
                    if (matchesLocation(provider))
                        break;
                    continue;
                }
                if (matchesLocation(next))
                {
                    provider = next;
                    break;
                }
            }
            return provider;
        }

        // Try to determine if the given candidate is a better match than
        // the current flow regarding the name and category path.
        private bool IsBetterFlow(Ref current, Ref candidate, FlowQuery query)
        {
            if (current == null)
                return true;

            // compare the names
            var words = query.Name.Split(' ');
            int currentScore = current.Name.MatchLengthOf(words);
            int candidateScore = candidate.Name.MatchLengthOf(words);
            if (candidateScore != currentScore)
                return candidateScore > currentScore;

            // compare locations
            if (query.Location.IsNotEmpty())
            {
                var isCu = query.Location
                    .EqualsIgnoreCase(current.Location);
                var isCa = query.Location
                    .EqualsIgnoreCase(candidate.Location);
                if (isCu != isCa)
                    return isCa;
            }

            // compare the categories
            if (query.Category.IsNotEmpty())
            {
                words = query.Category.Split('/');
                currentScore = current
                    .CategoryPath.Join("/")
                    .MatchLengthOf(words);
                candidateScore = candidate
                    .CategoryPath.Join("/")
                    .MatchLengthOf(words);
                return candidateScore > currentScore;
            }

            return false;
        }

        private FlowMapRef ToMapRef(Flow flow)
        {
            var flowRef = ToRef(flow);

            // flow property
            var prop = units.ReferenceQuantityOf(flow);
            var propRef = prop != null
                ? new Ref { Id = prop.Id, Name = prop.Name }
                : null;

            // unit
            var unit = units.ReferenceUnitOf(flow);
            var unitRef = unit != null
                ? new Ref { Id = unit.Id, Name = unit.Name }
                : null;

            return new FlowMapRef
            {
                Flow = flowRef,
                FlowProperty = propRef,
                Unit = unitRef,
            };
        }

        private Ref ToRef(Flow flow)
        {
            var r = new Ref
            {
                EntityType = EntityType.Flow,
                Id = flow.Id,
                Name = flow.Name,
                Description = flow.Description,
                Version = flow.Version,
                LastChange = flow.LastChange,
                Library = flow.Library,
                FlowType = flow.FlowType
            };

            if (flow.Category != null)
            {
                var category = flow.Category.CategoryPath;
                if (category.Count != 0)
                {
                    foreach (var c in category)
                    {
                        r.CategoryPath.Add(c);
                    }
                }
            }

            if (flow.Location != null && flow.Location.Name.IsNotEmpty())
            {
                r.Location = flow.Location.Name;
            }

            var unit = units.ReferenceUnitOf(flow);
            if (unit != null)
            {
                r.RefUnit = unit.Name;
            }
            return r;
        }

    }
}
