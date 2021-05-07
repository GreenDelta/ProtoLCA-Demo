using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;
using Service = ProtoLCA.Services.DataFetchService.DataFetchServiceClient;

namespace DemoApp
{
    class GetImpactMethodsExample : Example
    {
        private readonly Service service;

        public GetImpactMethodsExample(Channel channel)
        {
            this.service = new Service(channel);
        }

        public string Description()
        {
            return "Calling GetAll: get all LCIA methods and list their indicators";
        }

        public void Run()
        {
            var response = service.GetAll(new GetAllRequest
            {
                ModelType = ModelType.ImpactMethod,
                SkipPaging = true
            });
            foreach (var ds in response.DataSet)
            {
                var method = ds.ImpactMethod;
                if (method == null)
                    continue;
                Log($"\n  .. + {method.Name}");
                foreach (var impact in method.ImpactCategories)
                {
                    Log($"  ..   - {impact.Name} [{impact.RefUnit}]");
                }
            }
        }
    }
}
