using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using DotnetGuard.DiskMap.Core.Models;

namespace DotnetGuard.DiskMap.Data
{
    public class DiskScanner
    {
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(150);

        public DiskNode Scan(string rootPath, Action<DiskNode> onProgress, CancellationToken token)
        {
            DiskNode root = new DiskNode
            {
                Name = rootPath,
                FullPath = rootPath,
                IsDirectory = true
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            ScanDirectory(root, root, onProgress, stopwatch, token);
            onProgress?.Invoke(root);

            return root;
        }

        private void ScanDirectory(DiskNode root, DiskNode node, Action<DiskNode> onProgress, Stopwatch stopwatch, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                // Single pass: FileSystemInfo tells us file vs. directory without a second API call.
                foreach (FileSystemInfo entry in new DirectoryInfo(node.FullPath).EnumerateFileSystemInfos())
                {
                    token.ThrowIfCancellationRequested();

                    if (entry is DirectoryInfo)
                    {
                        DiskNode childNode = new DiskNode
                        {
                            Name = entry.Name,
                            FullPath = entry.FullName,
                            IsDirectory = true
                        };

                        // Add before recursing so it's visible (and grows) in the live view immediately,
                        // instead of only appearing once its whole subtree finishes scanning.
                        node.Children.Add(childNode);
                        ScanDirectory(root, childNode, onProgress, stopwatch, token);
                        node.Size += childNode.Size;
                    }
                    else if (entry is FileInfo fileInfo)
                    {
                        try
                        {
                            DiskNode fileNode = new DiskNode
                            {
                                Name = fileInfo.Name,
                                FullPath = fileInfo.FullName,
                                IsDirectory = false,
                                Size = fileInfo.Length,
                                Extension = string.IsNullOrEmpty(fileInfo.Extension) ? "(none)" : fileInfo.Extension.ToLowerInvariant()
                            };

                            node.Children.Add(fileNode);
                            node.Size += fileNode.Size;
                        }
                        catch (IOException)
                        {
                        }
                        catch (UnauthorizedAccessException)
                        {
                        }
                    }

                    if (stopwatch.Elapsed >= ProgressInterval)
                    {
                        onProgress?.Invoke(root);
                        stopwatch.Restart();
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        public static DiskNode CollapseSmallItems(DiskNode parent, int maxVisibleChildren)
        {
            if (parent.Children.Count <= maxVisibleChildren)
            {
                return parent;
            }

            var ordered = parent.Children.OrderByDescending(c => c.Size).ToList();
            var visible = ordered.Take(maxVisibleChildren - 1).ToList();
            var rest = ordered.Skip(maxVisibleChildren - 1).ToList();

            DiskNode other = new DiskNode
            {
                Name = $"Other ({rest.Count} items)",
                FullPath = parent.FullPath,
                IsDirectory = true,
                Size = rest.Sum(r => r.Size),
                Children = rest
            };

            DiskNode collapsed = new DiskNode
            {
                Name = parent.Name,
                FullPath = parent.FullPath,
                IsDirectory = parent.IsDirectory,
                Size = parent.Size,
                Extension = parent.Extension,
                Children = visible.Concat(new[] { other }).ToList()
            };

            return collapsed;
        }
    }
}
