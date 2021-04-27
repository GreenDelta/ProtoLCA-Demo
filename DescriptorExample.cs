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
    // This example shows how to load descriptors from a database.
    // Descriptors are small objects that describe a data set.
    // When you do not need the complete data set but e.g. just
    // want to search for data it is generally better to use the
    // data set descriptors instead of the full data sets for this.
    class DescriptorExample
    {
        private readonly Channel channel;

        public DescriptorExample(Channel channel)
        {
            this.channel = channel;
        }

        public async void Run()
        {
            var service = new DataFetchService.DataFetchServiceClient(channel);

            // get all flow descriptors
            Log("Get all flow descriptors");
            var start = DateTime.Now;
            var response = service.GetDescriptors(new GetDescriptorsRequest
            {
                ModelType = ModelType.Flow,
            }).ResponseStream;
            var flows = new List<Ref>();
            while (await response.MoveNext())
            {
                flows.Add(response.Current);
            }

            // print some results
            var time = DateTime.Now.Subtract(start).TotalSeconds;
            Log($"  .. collected {flows.Count} in {time} seconds");
            int i = 0;
            foreach (var flow in flows)
            {
                i++;
                var category = flow.CategoryPath != null
                    ? flow.CategoryPath.Join("/")
                    : "/";
                if (category.Length > 40)
                {
                    category = category.Substring(0, 37) + "...";
                }
                Log($"  .. {i}. {flow.Name} ({flow.RefUnit}; {category})");
                if (i >= 10)
                    break;
            }
            Log($"  .. {flows.Count - i} more");

        }


    }
}
