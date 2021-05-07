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
    class CategoryContentExample : Example
    {
        private readonly Service service;

        public CategoryContentExample(Channel channel)
        {
            this.service = new Service(channel);
        }

        public string Description()
        {
            return "Calling GetCategoryContent: print the content of the first"
                + " path in the flow tree";
        }

        public void Run()
        {
            var tree = service.GetCategoryTree(new GetCategoryTreeRequest
            {
                ModelType = ModelType.Flow
            });
            PrintContent(tree, 0).Wait();
        }

        private async Task<bool> PrintContent(CategoryTree tree, int depth)
        {
            // print the category node
            var offset = new string(' ', 2 * depth);
            var label = depth == 0 ? "#root" : tree.Name;
            Log($"  .. {offset}+ {label}");

            // get and print the content
            var content = service.GetCategoryContent(new GetCategoryContentRequest
            {
                ModelType = ModelType.Flow,
                Category = tree.Id
            }).ResponseStream;
            var elements = new List<Ref>();
            while (await content.MoveNext())
            {
                elements.Add(content.Current);
            }
            int i = 0;
            foreach (var elem in elements)
            {
                i++;
                if (i > 10)
                    break;
                Log($"  .. {offset}- {elem.Name}");
            }
            if (i < elements.Count)
            {
                Log($"  .. {offset}  {elements.Count - i} more");
            }

            // expand the first child node
            if (tree.SubTree != null && tree.SubTree.Count > 0)
            {
                var first = tree.SubTree.First();
                await PrintContent(first, depth + 1);
            }

            return true;
        }
    }
}
