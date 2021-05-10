using System;
using System.Collections.Generic;

using Grpc.Core;
using ProtoLCA;
using static DemoApp.Util;

namespace DemoApp {

    /// <summary>
    /// This example writes the unit groups, their default flow properties, and
    /// units to the console output.
    /// </summary>
    class UnitExample : Example {

        private readonly Channel channel;

        public UnitExample(Channel channel) {
            this.channel = channel;
        }

        public string Description() {
            return "List the available units of measurement";
        }

        public void Run() {
            var triples = Examples.GetUnitTriples(channel);
            foreach (var group in UnitGroupsOf(triples)) {
                var property = DefaultPropertyOf(group, triples);
                if (property == null) {
                    Log($"  .. ERROR: unit group {group.Name} " +
                        "has no default flow property");
                    continue;
                }
                Log($"\n  .. + unit group '{group.Name}' with default flow" +
                    $" property '{property.Name}': ");
                foreach (var unit in UnitsOf(group, triples)) {
                    Log($"  ..   - {unit.Name}; {unit.ConversionFactor}");
                }
            }
        }

        private List<UnitGroup> UnitGroupsOf(
            List<(Unit, UnitGroup, FlowProperty)> triples) {
            var groups = new List<UnitGroup>();
            var handled = new HashSet<string>();
            foreach (var (_, group, _) in triples) {
                if (handled.Contains(group.Id))
                    continue;
                groups.Add(group);
                handled.Add(group.Id);
            }
            groups.Sort((g1, g2) => String.Compare(g1.Name, g2.Name));
            return groups;
        }

        private FlowProperty DefaultPropertyOf(UnitGroup group,
            List<(Unit, UnitGroup, FlowProperty)> triples) {
            foreach (var (_, otherGroup, property) in triples) {
                if (String.Equals(group.Id, otherGroup.Id))
                    return property;
            }
            return null;
        }

        private List<Unit> UnitsOf(UnitGroup group,
            List<(Unit, UnitGroup, FlowProperty)> triples) {
            var units = new List<Unit>();
            foreach (var (unit, otherGroup, _) in triples) {
                if (String.Equals(group.Id, otherGroup.Id)) {
                    units.Add(unit);
                }
            }
            units.Sort((u1, u2) => String.Compare(u1.Name, u2.Name));
            return units;
        }
    }
}
