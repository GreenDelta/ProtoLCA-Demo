using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ProtoLCA;

using System.Security.Cryptography;

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
                var hash = MD5.Create().ComputeHash(
               Encoding.UTF8.GetBytes(this.ToString()));
                _id = new Guid(hash).ToString();
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
            if (!String.IsNullOrEmpty(Category))
            {
                var path = Category.Split('/');
                foreach (var p in path)
                {
                    if (!String.IsNullOrEmpty(p))
                    {
                        mapRef.Flow.CategoryPath.Add(p);
                    }
                }
            }
            return mapRef;
        }

        public FlowMapEntry FindEntryIn(FlowMap flowMap)
        {
            if (flowMap == null)
                return null;
            foreach (var entry in flowMap.Mappings)
            {
                if (FlowID.Equals(entry.From.Flow.Id))
                    return entry;
            }
            return null;
        }

    }
}
