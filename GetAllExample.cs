using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp
{

    // This class contains examples for GetAll requests. By default
    // these requests return a paged response. For small objects with
    // a small number of data sets you may want to skip the paging.     
    class GetAllExample
    {
        private readonly Channel channel;

        public GetAllExample(Channel channel)
        {
            this.channel = channel;
        }

        public async void Run()
        {
            var service = new DataFetchService.DataFetchServiceClient(channel);

            Log("Get the first page of all flows");
            var flows = service.GetAll(new GetAllRequest
            {
                ModelType = ModelType.Flow,
            });
            Log($"  .. loaded first {flows.PageSize} of {flows.TotalCount} flows");
            int i = 0;
            foreach (var dataSet in flows.DataSet)
            {
                i++;
                Log($" .. {i}. {dataSet.Flow.Name}");
                if (i >= 5)
                    break;
            }
            Log($"  .. {flows.PageSize - i} more");

            Log("Get all unit groups from the database");


        }

    }
}
