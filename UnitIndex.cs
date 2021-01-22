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
    public class UnitIndex
    {
        private readonly Dictionary<string, FlowProperty> flowProps;
        private readonly Dictionary<string, UnitGroup> unitGroups;
        private readonly Index index;

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
            // their corresponding unit and flow property objects
            this.index = new Index();
            var handled = new HashSet<string>();
            Func<(string, Unit, FlowProperty), bool> add = tup =>
            {
                var (symbol, unit, prop) = tup;
                if (handled.Contains(symbol))
                {
                    Log($"WARNING: Duplicate unit {symbol} in database.");
                    return false;
                }
                index.Add(symbol, new UnitEntry(prop, unit));
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
                    add((unit.Name, unit, prop));
                    foreach (var synonym in unit.Synonyms)
                    {
                        add((synonym, unit, prop));
                    }
                }
            }

        }

        /// <summary>
        /// Returns true when both units can be coverted into each other.
        /// </summary>
        public bool AreConvertible(string unit1, string unit2)
        {
            if (string.IsNullOrWhiteSpace(unit1)
                || string.IsNullOrWhiteSpace(unit2))
                return false;


            var e1 = EntryOf(unit1);
            if (e1 == null)
                return false;

            var e2 = EntryOf(unit2);
            if (e2 == null)
                return false;

            return e1.FlowProperty.Id.Equals(e2.FlowProperty.Id);
        }

        public UnitEntry EntryOf(string unit)
        {
            return index.TryGetValue(unit, out UnitEntry entry)
                ? entry
                : null;
        }

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
        public readonly Unit Unit;
        public double Factor
        {
            get { return Unit.ConversionFactor; }
        }

        internal UnitEntry(FlowProperty prop, Unit unit)
        {
            this.FlowProperty = prop;
            this.Unit = unit;
        }
    }

}
