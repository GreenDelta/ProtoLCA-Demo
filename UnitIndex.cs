using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;

using Index = System.Collections.Generic.Dictionary<string, DemoApp.UnitEntry>;
using static DemoApp.Util;

namespace DemoApp
{
    /// <summary>
    /// We index units and their corresponding groups and flow properties by the
    /// respective unit names. In openLCA convertible units are organized in
    /// unit groups and amounts of flows are given in a quantity (flow property
    /// in openLCA) and unit. Multiple flow properties can link to the same
    /// unit group in openLCA. In order to select a flow property for a unit,
    /// a unit group can have a link to a default flow property. This index
    /// builds a mapping from a unit name to its corresponding default flow
    /// property.
    /// </summary>
    public class UnitIndex
    {
        private readonly Dictionary<string, FlowProperty> flowProps;
        private readonly Dictionary<string, UnitGroup> unitGroups;
        private readonly Index index;

        /// <summary>
        /// Builds a new unit index.
        /// </summary>
        public static async Task<UnitIndex> Build(Channel chan)
        {
            var data = new DataService.DataServiceClient(chan);

            // fetch the unit groups
            var groupStream = data.GetUnitGroups(new Empty())
                .ResponseStream;
            var groups = new Dictionary<string, UnitGroup>();
            while (await groupStream.MoveNext())
            {
                var group = groupStream.Current;
                groups.Add(group.Id, group);
            }

            // fetch the flow properties
            var propStream = data.GetFlowProperties(new Empty())
                .ResponseStream;
            var props = new Dictionary<string, FlowProperty>();
            while (await propStream.MoveNext())
            {
                var prop = propStream.Current;
                props.Add(prop.Id, prop);
            }
            return new UnitIndex(props, groups);
        }

        private UnitIndex(
            Dictionary<string, FlowProperty> flowProps,
            Dictionary<string, UnitGroup> unitGroups)
        {
            this.flowProps = flowProps;
            this.unitGroups = unitGroups;

            // build the reverse unit index that maps unit symbols to
            // their corresponding unit, group, and flow property objects
            this.index = new Index();
            var handled = new HashSet<string>();
            Func<(string, Unit, UnitGroup, FlowProperty), bool> add = tup =>
            {
                var (symbol, unit, group, prop) = tup;
                if (handled.Contains(symbol))
                {
                    Log($"WARNING: Duplicate unit {symbol} in database.");
                    return false;
                }
                index.Add(symbol, new UnitEntry(prop, group, unit));
                handled.Add(symbol);
                return false;
            };

            foreach (var group in unitGroups.Values)
            {
                if (!flowProps.TryGetValue(
                    group.DefaultFlowProperty.Id, out FlowProperty prop))
                    continue;
                foreach (var unit in group.Units)
                {
                    add((unit.Name, unit, group, prop));
                    foreach (var synonym in unit.Synonyms)
                    {
                        add((synonym, unit, group, prop));
                    }
                }
            }

        }

        /// <summary>
        /// Returns true when both units can be coverted into each other.
        /// This is true when the units belong to the same unit group.
        /// </summary>
        public bool AreConvertible(string unit1, string unit2)
        {
            if (string.IsNullOrWhiteSpace(unit1)
                || string.IsNullOrWhiteSpace(unit2))
                return false;


            var e1 = EntryOf(unit1);
            if (e1 == null || e1.UnitGroup == null)
                return false;

            var e2 = EntryOf(unit2);
            if (e2 == null || e2.UnitGroup == null)
                return false;

            return String.Equals(e1.UnitGroup.Id, e2.UnitGroup.Id);
        }

        public UnitEntry EntryOf(string unit)
        {
            return index.TryGetValue(unit, out UnitEntry entry)
                ? entry
                : null;
        }

        /// <summary>
        /// Returns the reference quantity (flow property) of the
        /// given flow. Results of that flow are always given in
        /// that quantity. Also, this is the quantity that should
        /// be used for the flow mappings.
        /// </summary>
        public FlowProperty ReferenceQuantityOf(Flow flow)
        {
            if (flow == null)
                return null;
            foreach (var f in flow.FlowProperties)
            {
                if (f.ReferenceFlowProperty)
                {
                    return flowProps.TryGetValue(
                        f.FlowProperty.Id, out FlowProperty prop)
                        ? prop
                        : null;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the reference unit of the given flow. Results
        /// of that flow are always given in that unit. Also, this
        /// is the unit that should be used for the flow mappings.
        /// </summary>
        public Unit ReferenceUnitOf(Flow flow)
        {
            var refProp = ReferenceQuantityOf(flow);
            if (refProp == null)
                return null;
            if (!unitGroups.TryGetValue(
                refProp.UnitGroup.Id, out UnitGroup group))
                return null;
            foreach (var unit in group.Units)
            {
                if (unit.ReferenceUnit)
                    return unit;
            }
            return null;
        }
    }

    public class UnitEntry
    {
        public readonly FlowProperty FlowProperty;
        public readonly UnitGroup UnitGroup;
        public readonly Unit Unit;

        public double Factor
        {
            get { return Unit.ConversionFactor; }
        }

        internal UnitEntry(
            FlowProperty prop,
            UnitGroup group,
            Unit unit)
        {
            this.FlowProperty = prop;
            this.UnitGroup = group;
            this.Unit = unit;
        }
    }

}
