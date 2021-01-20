using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;

using Index = System.Collections.Generic.Dictionary<string, (ProtoLCA.FlowProperty, double)>;

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
                            Console.WriteLine($"WARNING: there are " +
                                $"duplicate units in the database, e.g. {unit}");
                            unitErr = true;
                        }
                        continue;
                    }
                    idx.Add(unit.Name, (prop, unit.ConversionFactor));
                }
            }

            return new UnitIndex(idx);
        }

        public bool AreConvertible(string unit1, string unit2)
        {
            if (string.IsNullOrWhiteSpace(unit1)
                || string.IsNullOrWhiteSpace(unit2))
                return false;
            var (prop1, _) = index[unit1];
            var (prop2, _) = index[unit2];
            if (prop1 == null || prop2 == null)
                return false;
            return prop1 == prop2;
        }

        public FlowProperty PropertyOf(string unit)
        {
            var (prop, _) = index[unit];
            return prop;
        }

        public double FactorOf(string unit)
        {
            var (_, f) = index[unit];
            return f;
        }
    }

}
