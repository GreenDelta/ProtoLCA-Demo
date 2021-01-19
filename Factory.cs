using System;
using ProtoLCA;

namespace DemoApp
{
    public class Build
    {
        private Build() { }

        public static Ref RefOf(string id = "", string name = "")
        {
            return new Ref
            {
                Id = id,
                Name = name
            };
        }

        public static Unit UnitOf(string name = "", double conversionFactor = 1.0)
        {
            var unit = new Unit
            {
                Id = Guid.NewGuid().ToString(),
                Type = "Unit",
                Name = name,
                ConversionFactor = conversionFactor,
            };
            return unit;
        }

        public static UnitGroup UnitGroupOf(string name = "", Unit unit = null)
        {
            var group = new UnitGroup();
            SetBaseAttributes(group, name);
            if (unit != null)
            {
                unit.ReferenceUnit = true;
                group.Units.Add(unit);
            }
            return group;
        }

        public static FlowProperty FlowPropertyOf(string name = "", UnitGroup unitGroup = null)
        {
            var property = new FlowProperty();
            SetBaseAttributes(property, name);
            if (unitGroup != null)
            {
                property.UnitGroup = new Ref
                {
                    Id = unitGroup.Id,
                    Name = unitGroup.Name,
                };
            }
            return property;
        }

        public static Flow FlowOf(
            string name = "",
            FlowType flowType = FlowType.UndefinedFlowType,
            FlowProperty property = null)
        {
            var flow = new Flow();
            SetBaseAttributes(flow, name);
            flow.FlowType = flowType;
            if (property != null)
            {
                var prop = new FlowPropertyFactor
                {
                    ConversionFactor = 1.0,
                    ReferenceFlowProperty = true,
                    FlowProperty = new Ref
                    {
                        Id = property.Id,
                        Name = property.Name
                    }
                };
                flow.FlowProperties.Add(prop);
            }
            return flow;
        }

        public static Flow ProductFlowOf(string name = "", FlowProperty property = null)
        {
            return FlowOf(name, FlowType.ProductFlow, property);
        }

        public static Flow WasteFlowOf(string name = "", FlowProperty property = null)
        {
            return FlowOf(name, FlowType.WasteFlow, property);
        }

        public static Flow ElementaryFlowOf(string name = "", FlowProperty property = null)
        {
            return FlowOf(name, FlowType.ElementaryFlow, property);
        }

        public static Exchange ExchangeOf(Process process, Flow flow, double amount = 1.0)
        {
            process.LastInternalId++;
            return new Exchange
            {
                InternalId = process.LastInternalId,
                Amount = amount,
                Flow = new FlowRef
                {
                    Id = flow.Id,
                    Name = flow.Name
                }
            };
        }

        public static Exchange InputOf(Process process, Flow flow, double amount = 1.0)
        {
            var exchange = ExchangeOf(process, flow, amount);
            exchange.Input = true;
            return exchange;
        }

        public static Exchange OutputOf(Process process, Flow flow, double amount = 1.0)
        {
            var exchange = ExchangeOf(process, flow, amount);
            exchange.Input = false;
            return exchange;
        }

        public static Process ProcessOf(string name = "")
        {
            var process = new Process();
            SetBaseAttributes(process, name);
            process.ProcessType = ProcessType.UnitProcess;
            return process;
        }

        public static Location LocationOf(string name = "", String code = null)
        {
            var location = new Location();
            SetBaseAttributes(location, name);
            location.Code = code ?? name;
            return location;
        }

        private static void SetBaseAttributes(dynamic entity, string name)
        {
            entity.Id = Guid.NewGuid().ToString();
            entity.Name = name;
            entity.Type = entity.GetType().Name;
            entity.Version = "00.00.000";
            entity.LastChange = DateTime.UtcNow.ToString(
                "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
        }

    }
}