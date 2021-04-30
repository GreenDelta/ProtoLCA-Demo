using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp
{
    class CategoryTreeExample
    {
        private readonly Channel channel;

        public CategoryTreeExample(Channel channel)
        {
            this.channel = channel;
        }

        public void Run()
        {
            var service = new DataFetchService.DataFetchServiceClient(channel);

            Log("Get the flow category tree from the data service ...");
            var tree = service.GetCategoryTree(new GetCategoryTreeRequest
            {
                ModelType = ModelType.Flow
            });

            printTree(tree, 0);

        }

        private void printTree(ProtoLCA.Services.CategoryTree tree, int depth)
        {
            var offset = new string(' ', 2 * depth);
            var label = depth == 0 ? "#root" : tree.Name;
            Log($"  .. {offset}+ {label}");
            foreach (var subTree in tree.SubTree)
            {
                printTree(subTree, depth + 1);
            }
        }


    }
}
