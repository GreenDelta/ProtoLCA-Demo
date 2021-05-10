using Grpc.Core;
using ProtoLCA;
using ProtoLCA.Services;
using static DemoApp.Util;

namespace DemoApp {

    /// <summary>
    /// This example loads the full flow category tree and prints it
    /// recursively on the console.
    /// </summary>
    class CategoryTreeExample : Example {
        private readonly Channel channel;

        public CategoryTreeExample(Channel channel) {
            this.channel = channel;
        }

        public string Description() {
            return "Get the full flow category tree";
        }

        public void Run() {
            var service = new DataFetchService.DataFetchServiceClient(channel);
            var tree = service.GetCategoryTree(new GetCategoryTreeRequest {
                ModelType = ModelType.Flow
            });
            PrintTree(tree, 0);

        }

        private void PrintTree(CategoryTree tree, int depth) {
            var offset = new string(' ', 2 * depth);
            var label = depth == 0 ? "#root" : tree.Name;
            Log($"  .. {offset}+ {label}");
            foreach (var subTree in tree.SubTree) {
                PrintTree(subTree, depth + 1);
            }
        }
    }
}
