using Otokoneko.DataType;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Otokoneko.Server.Converter
{
    class FileTreeNodeFormatter
    {
        private static readonly HashSet<string> ImageFileExtensions = new HashSet<string>
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".bmp",
            ".gif",
            ".webp"
        };

        public static bool IsImageFile(FileTreeNode node)
        {
            return !node.IsDirectory && ImageFileExtensions.Contains(node.Extension);
        }

        public static FileStructType ClassifyAndFormatDirectoryStruct(FileTreeNode node, List<FileTreeNode>[] nodes = null, bool isRoot=true)
        {
            // 图像文件节点
            if (IsImageFile(node))
            {
                nodes?[(int)node.StructType].Add(node);
                node.StructType = FileStructType.Image;
                return node.StructType;
            }

            // 若某节点无后代且其本身非图像文件节点，则该节点无意义
            if (node.Children == null || node.Children.Count == 0)
            {
                nodes?[(int)node.StructType].Add(node);
                node.StructType = FileStructType.None;
                return node.StructType;
            }

            // 各类型的子节点数量
            var number = Enum.GetValues(typeof(FileStructType)).Cast<FileStructType>().Max();
            var classes = new int[(int)(number + 1)];
            // 所有子节点中最高的层次
            var result = FileStructType.None;
            var newNodes = new List<FileTreeNode>[(int)(number + 1)];
            for (var i = 0; i < classes.Length; i++)
            {
                classes[i] = 0;
                newNodes[i] = new List<FileTreeNode>();
            }

            foreach (var child in node.Children)
            {
                result = (FileStructType)Math.Max((int)result, (int)ClassifyAndFormatDirectoryStruct(child, newNodes, false));
                classes[(int)child.StructType]++;
            }

            // 非根节点（根节点默认为 Manga 节点）
            if (!isRoot)
            {
                if (classes[(int)FileStructType.Series] != 0)
                {
                    // 子节点中有多于一个的 Series 节点，则自身为 Series 节点，且原 Series 子节点应修正为 None 节点
                    if (classes[(int)FileStructType.Series] > 1 || classes[(int)FileStructType.Chapter] != 0)
                    {
                        node.StructType = FileStructType.Series;
                        foreach (var series in newNodes[(int)FileStructType.Series])
                        {
                            series.StructType = FileStructType.None;
                        }
                        newNodes[(int)FileStructType.Series].Clear();
                    }
                    // 子节点中有且只有一个 Series 节点，则自身为 None 节点
                    else
                    {
                        node.StructType = FileStructType.None;
                    }
                }
                // 子节点中有多于一个的 Chapter 节点，则自身为 Series 节点
                else if (classes[(int)FileStructType.Chapter] > 1)
                {
                    node.StructType = FileStructType.Series;
                }
                // 子节点中存在 Image 节点，则自身为 Chapter 节点
                else if (classes[(int)FileStructType.Image] != 0)
                {
                    node.StructType = FileStructType.Chapter;
                }
                else
                {
                    node.StructType = FileStructType.None;
                }

            }

            if (nodes != null)
            {
                nodes[(int)node.StructType].Add(node);
                for (var i = 0; i < nodes.Length; i++)
                {
                    nodes[i].AddRange(newNodes[i]);
                }
            }

            // 返回以本节点为根的子树中最高的层次
            return (FileStructType)Math.Max((int)result, (int)node.StructType);
        }
    }
}
