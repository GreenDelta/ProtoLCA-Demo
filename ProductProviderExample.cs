using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp
{
    // In this example, we search for the providers of a product flow. Such
    // providers are processes that produce this product; thus, have it on the
    // the output side. For waste flows this is similar but a "provider" of a
    // waste flow is a waste treatment process that has this flow on the input
    // side.
    class ProductProviderExample
    {
        internal static async void Run(Channel channel)
        {
            Log("Search for providers of a product flow ...");
            var service = new DataFetchService.DataFetchServiceClient(channel);

            // first, we fetch the descriptors of all flows in the database
            Log("  .. get all flow descriptors");
            var descriptors = service.GetDescriptors(new GetDescriptorsRequest
            {
                ModelType = ModelType.Flow
            }).ResponseStream;

            // now we search for product flows and providers of them
            // we try this ten times until we give up
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

                // check if it is a product flow
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
