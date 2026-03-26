using System.IO;
using System.Linq;

namespace HealthcheckDashboard.ResourceNS
{
    class LatestFileResource : Resource
    {
        public string FileSearchPath { get; }
        public string FilePath
        {
            get
            {
                if (Directory.GetFiles(Path.GetDirectoryName(FileSearchPath), Path.GetFileName(FileSearchPath))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault() is FileInfo latestFile)
                {
                    return latestFile.FullName;
                }
                else
                {
                    return null;
                }
            }
        }

        public LatestFileResource(string fileSearchPath) : base(ResourceType.LatestFile)
        {
            FileSearchPath = fileSearchPath;
        }

        public override string ToString()
        {
            return nameof(LatestFileResource) + ": " + FilePath;
        }
    }
}
