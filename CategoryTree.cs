using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Grpc.Core;
using ProtoLCA;
using DataService = ProtoLCA.Services.DataFetchService.DataFetchServiceClient;

namespace DemoApp
{

    public class CategoryNode
    {

        public readonly Category Category;
        public readonly List<CategoryNode> Childs;
        public string Name
        {
            get { return Category.Name.OrEmpty(); }
        }
        public ModelType ModelType
        {
            get { return Category.ModelType; }
        }

        public CategoryNode(Category category)
        {
            this.Category = category;
            this.Childs = new List<CategoryNode>();
        }
    }

    class CategoryTree
    {
        private readonly Dictionary<ModelType, List<CategoryNode>> roots;

        private CategoryTree()
        {
            roots = new Dictionary<ModelType, List<CategoryNode>>();
        }

        public List<CategoryNode> RootsOf(ModelType type)
        {
            if (this.roots.TryGetValue(type, out List<CategoryNode> roots))
            {
                return roots;
            }
            return new List<CategoryNode>();
        }

        /// <summary>
        /// Add a new root category to the tree.
        /// </summary>
        private void AddRoot(CategoryNode node)
        {
            if (this.roots.TryGetValue(
                node.ModelType, out List<CategoryNode> roots))
            {
                roots.Add(node);
            }
            else
            {
                roots = new List<CategoryNode>();
                this.roots.Add(node.ModelType, roots);
                roots.Add(node);
            }
        }

        /// <summary>
        /// Sort the nodes of the tree recursively by name.
        /// </summary>
        private void Sort()
        {
            foreach (var nodes in roots.Values)
            {
                Sort(nodes);
            }
        }

        private static void Sort(List<CategoryNode> nodes)
        {
            foreach (var node in nodes)
            {
                Sort(node.Childs);
            }
            nodes.Sort((node1, node2) =>
            {
                return String.Compare(
                    node1.Name.ToLower(),
                    node2.Name.ToLower());
            });
        }

        /// <summary>
        /// Construct a new category tree from all categories that can be retrieved
        /// from the given data service.
        /// </summary>
        public static async Task<CategoryTree> Build(DataService data)
        {
            var nodes = new Dictionary<string, CategoryNode>();
            var categories = data.GetC(new Empty()).ResponseStream;
            while (await categories.MoveNext())
            {
                var next = categories.Current;
                nodes.Add(next.Id, new CategoryNode(next));
            }

            var tree = new CategoryTree();
            foreach (var node in nodes.Values)
            {
                var parentID = node.Category.Category_?.Id;
                if (String.IsNullOrEmpty(parentID))
                {
                    tree.AddRoot(node);
                }
                else
                {
                    if (nodes.TryGetValue(parentID, out CategoryNode parent))
                    {
                        parent.Childs.Add(node);
                    }
                }
            }
            tree.Sort();
            return tree;
        }
    }
}
