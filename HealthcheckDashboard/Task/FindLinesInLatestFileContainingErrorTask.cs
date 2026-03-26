using HealthcheckDashboard.ResourceNS;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HealthcheckDashboard.TaskNS
{
    class FindLinesInLatestFileContainingErrorTask : ITask
    {
        public string Name { get; }
        private LatestFileResource LatestFileResource { get; }
        public string LineWithError { get; private set; } = null;
        public string[] TextPartsIndicatingError { get; }
        public string[] TextPartsToExclude { get; }

        public FindLinesInLatestFileContainingErrorTask(string name, LatestFileResource latestFileResource, string[] textPartsIndicatingError, string[] textPartsToExclude)
        {
            Name = name;
            LatestFileResource = latestFileResource;
            TextPartsIndicatingError = textPartsIndicatingError ?? Array.Empty<string>();
            TextPartsToExclude = textPartsToExclude ?? Array.Empty<string>();
        }

        public async Task PerformAsync()
        {
            if (LatestFileResource.FilePath == null)
            {
                await Console.Out.WriteLineAsync($"No file found to read for {LatestFileResource.FileSearchPath}.");
                return;
            }

            var allLines = await File.ReadAllLinesAsync(LatestFileResource.FilePath);

            LineWithError = allLines.Where(line =>
                TextPartsIndicatingError.Any(part => line.Contains(part, StringComparison.OrdinalIgnoreCase)) &&
                !TextPartsToExclude.Any(part => line.Contains(part, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault();

            if (LineWithError == null)
            {
                await Console.Out.WriteLineAsync($"No line containing error found in file {Path.GetFileName(LatestFileResource.FilePath)}");
            }
            else
            {
                await Console.Out.WriteLineAsync($"[{nameof(FindLinesInLatestFileContainingErrorTask)}] LineWithError is {LineWithError}, in file {Path.GetFileName(LatestFileResource.FilePath)}");
            }
        }

        public override string ToString()
        {
            return nameof(FindLinesInLatestFileContainingErrorTask) + " " + LineWithError + $": {LineWithError} - in file {LatestFileResource.FilePath}";
        }
    }
}
