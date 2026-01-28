using HealthcheckDashboard.ResourceNS;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HealthcheckDashboard.TaskNS
{
    class GetFileLastModifiedDateTask : ITask
    {
        public string Name { get; }
        private GeneralFileResource GeneralFileResource { get; }

        public DateTime LastModifiedDate { get; private set; }

        public GetFileLastModifiedDateTask(string name, GeneralFileResource generalFileResource)
        {
            Name = name;
            GeneralFileResource = generalFileResource;
        }

        public async Task PerformAsync()
        {
            LastModifiedDate = File.GetLastWriteTime(GeneralFileResource.FilePath);
            await Console.Out.WriteLineAsync($"[{nameof(GetFileLastModifiedDateTask)}] LastModifiedDate is {LastModifiedDate}");
        }

        public override string ToString()
        {
            return nameof(GetFileLastModifiedDateTask) + " " + Name + $": {LastModifiedDate}";
        }
    }
}
