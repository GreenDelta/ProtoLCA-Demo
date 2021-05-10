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

        /// <summary>
        /// Searches for a flow with matching attributes in the database. It
        /// first searches for a flow with a matching name. Then, it filters the
        /// search result by the other attributes.
        /// </summary>
        /// <param name="channel">the gRPC channel</param>
        /// <param name="type">the flow type</param>
        /// <param name="name">the flow name</param>
        /// <param name="unit">the reference unit of the flow</param>
        /// <param name="category">
        /// the flow category; this can be fragments of category path segments
        /// (e.g. `emmissions/air`)
        /// </param>
        /// <returns>
        /// a matching flow or null if no such flow could be found
        /// </returns>
        public static async Task<Ref> FindFlow(Channel channel, FlowType type,
            string name, string unit, string category = "") {
            Log($"  .. search flow {type}; {name}; {unit}");
            var service = new DataService(channel);
            var flows = service.Search(new SearchRequest {
                ModelType = ModelType.Flow,
                Query = name
            }).ResponseStream;
            Ref match = null;
            while (await flows.MoveNext()) {
                var candidate = flows.Current;
                if (candidate.FlowType != type)
                    continue;
                if (!unit.Equals(candidate.RefUnit))
                    continue;
                if (category.IsEmpty()) {
                    match = candidate;
                    break;
                }
                if (candidate.CategoryPath == null)
                    continue;
                var flowCategory = candidate.CategoryPath.Join("/").ToLower();
                var terms = category.ToLower().Split('/');
                var categoryMatches = true;
                foreach (var term in terms) {
                    if (!flowCategory.Contains(term.Trim().ToLower())) {
                        categoryMatches = false;
                        break;
                    }
                }
                if (categoryMatches) {
                    match = candidate;
                    break;
                }
            }

            if (match != null) {
                Log($"  .. found matching flow {match.Id}");
                return match;
            }
            Log("  .. found no matching flow");
            return null;
        }

        /// <summary>
        /// Returns the list of unit triples from the database. A unit triple
        /// contains a unit, the corresponding unit group, and the default
        /// flow property of that unit group. In openLCA it is a bit specific
        /// that you link a flow amount to a flow property (e.g. dry volume) and
        /// a unit (e.g. m3).
        /// </summary>
        public static List<(Unit, UnitGroup, FlowProperty)> GetUnitTriples(
            Channel channel) {
            Log("  .. load unit, unit group, flow property triples");
            var service = new DataService(channel);

            // collect all flow properties
            var flowProperties = new List<FlowProperty>();
            var propResponse = service.GetAll(new GetAllRequest {
                ModelType = ModelType.FlowProperty,
                SkipPaging = true,
            });
            foreach (var ds in propResponse.DataSet) {
                if (ds.FlowProperty != null) {
                    flowProperties.Add(ds.FlowProperty);
                }
            }

            // collect the triples
            var triples = new List<(Unit, UnitGroup, FlowProperty)>();
            var groupResponse = service.GetAll(new GetAllRequest {
                ModelType = ModelType.UnitGroup,
                SkipPaging = true,
            });
            foreach (var ds in groupResponse.DataSet) {
                var group = ds.UnitGroup;
                if (group == null)
                    continue;
                // find the matching flow property of that unit group
                FlowProperty property = null;
                var defaultPropId = group.DefaultFlowProperty?.Id;
                foreach (var candidate in flowProperties) {
                    if (defaultPropId != null
                        && defaultPropId.Equals(candidate.Id)) {
                        property = candidate;
                        break;
                    }
                    var groupId = candidate.UnitGroup?.Id;
                    if (groupId == null || !groupId.Equals(group.Id))
                        continue;
                    if (property != null) {
                        Log($"  .. WARNING unit group '{group.Name}' can " +
                            $"have multiple flow properties, e.g. " +
                            $"{property.Name} or {candidate.Name}");
                        continue;
                    }
                    property = candidate;
                }

                if (property == null) {
                    Log("  .. no default flow property found for unit" +
                        $" group {group.Name}");
                    continue;
                }
                foreach (var unit in group.Units) {
                    triples.Add((unit, group, property));
                }
            }
            Log($"  .. loaded {triples.Count} unit triples");
            return triples;
        }
    }
}
