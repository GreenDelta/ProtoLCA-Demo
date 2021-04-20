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
    // In this example, we search for the providers of a product
    // flow which are processes that have this product on the
    // output side. For waste flows this is similar but a "provider"
    // of a waste flow is a waste treatment process that has this
    // flow as an input.
    class ProductProviderExample
    {
        internal static async void Run(Channel channel)
        {
            Log("Search for providers of a product flow ...");

            var service = new DataFetchService.DataFetchServiceClient(channel);

            // get the flow descriptors
            Log("  .. get all flow descriptors");
            var descriptors = service.GetDescriptors(new GetDescriptorsRequest
            {
                ModelType = ModelType.Flow
            }).ResponseStream;

            // search for product flows and providers of them
            int flowCount = 0;
            bool foundSomething = false;
            int trials = 0;
            while (await descriptors.MoveNext())
            {
                flowCount++;
                if (flowCount % 5000 == 0)
                {
                    Log($"  .. checked {flowCount} flows");
                }

                // check for product flows
                var productRef = descriptors.Current;
                if (productRef.FlowType != FlowType.ProductFlow)
                    continue;
                trials++;

                // try to get providers of the product
                Log($"  .. check product {productRef.Name}");
                var providers = service.GetProvidersFor(productRef).ResponseStream;
                while (await providers.MoveNext())
                {
                    foundSomething = true;
                    var provider = providers.Current;
                    Log($"  .. => found provider {provider.Name}");
                }
                if (foundSomething || trials >= 10)
                    break;
            }

            if (!foundSomething)
            {
                Log("Could not find product providers;" +
                    $" tried with ${trials} products");
            }
        }
    }
}
