using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly Index index;

        private UnitIndex(Index index)
        {
            this.index = index;
        }

        public static async Task<UnitIndex> Build(Channel chan)
        {
            var data = new DataService.DataServiceClient(chan);

            var groupStream = data.GetUnitGroups(new Empty())
                .ResponseStream;
            var groups = new Dictionary<string, UnitGroup>();
            while (await groupStream.MoveNext())
            {
                var group = groupStream.Current;
                groups.Add(group.Id, group);
            }

            var propStream = data.GetFlowProperties(new Empty())
                .ResponseStream;
            var idx = new Index();

            var unitErr = false;
            while (await propStream.MoveNext())
            {
                var prop = propStream.Current;
                var group = groups[prop.UnitGroup.Id];
                if (group == null
                    || !prop.Id.Equals(group.DefaultFlowProperty.Id))
                    continue;
                foreach (var unit in group.Units)
                {
                    // TODO: also index synonyms
                    if (idx.ContainsKey(unit.Name))
                    {
                        if (!unitErr)
                        {
                            Log("WARNING: there are " +
                                $"duplicate units in the database, e.g. {unit.Name}");
                            unitErr = true;
                        }
                        continue;
                    }
                    idx.Add(unit.Name, new UnitEntry(prop, unit));
                }
            }

            return new UnitIndex(idx);
        }

        public bool AreConvertible(string unit1, string unit2)
        {
            if (string.IsNullOrWhiteSpace(unit1)
                || string.IsNullOrWhiteSpace(unit2))
                return false;

            var e1 = index[unit1];
            if (e1 == null)
                return false;

            var e2 = index[unit2];
            if (e2 == null)
                return false;

            // checking by identity should be good here
            return e1.FlowProperty == e2.FlowProperty;
        }

        public UnitEntry EntryOf(string unit)
        {
            return index[unit];
        }

        public FlowProperty PropertyOf(string unit)
        {
            return index[unit]?.FlowProperty;
        }

        public double FactorOf(string unit)
        {
            var e = index[unit];
            return e == null ? 0 : e.Factor;
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
