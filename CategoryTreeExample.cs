using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp
{
    class CategoryTreeExample : Example
    {
        private readonly Channel channel;

        public CategoryTreeExample(Channel channel)
        {
            this.channel = channel;
        }

        public string Description()
        {
            return "Calling GetCategoryTree: Get the category tree for flows";
        }

        public void Run()
        {
            var service = new DataFetchService.DataFetchServiceClient(channel);
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
