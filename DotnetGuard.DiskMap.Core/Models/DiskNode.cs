using System.Collections.Generic;

namespace DotnetGuard.DiskMap.Core.Models
{
    public class DiskNode
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public string Extension { get; set; } = "";
        public List<DiskNode> Children { get; set; } = new List<DiskNode>();

        public string DisplaySize => FormatSize(Size);

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.#} {units[unitIndex]}";
        }
    }
}
