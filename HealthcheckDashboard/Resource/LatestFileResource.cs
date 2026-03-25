using System.IO;
using System.Linq;

namespace HealthcheckDashboard.ResourceNS
{
    class LatestFileResource : Resource
    {
        public string FileSearchPath { get; }
        public string FilePath { get; }

        public LatestFileResource(string fileSearchPath) : base(ResourceType.LatestFile)
        {
            FileSearchPath = fileSearchPath;

            if (Directory.GetFiles(Path.GetDirectoryName(fileSearchPath), Path.GetFileName(fileSearchPath))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault() is FileInfo latestFile)
            {
                FilePath = latestFile.FullName;
            }
            else
            {
                throw new FileNotFoundException($"No files found in directory: {fileSearchPath}");
            }
        }

        public override string ToString()
        {
            return nameof(LatestFileResource) + ": " + FilePath;
        }
    }
}
