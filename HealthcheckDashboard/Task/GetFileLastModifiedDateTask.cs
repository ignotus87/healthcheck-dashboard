using HealthcheckDashboard.ConditionNS;
using HealthcheckDashboard.ResourceNS;
using System;
using System.IO;
using System.Threading.Tasks;

namespace HealthcheckDashboard.TaskNS
{
    class GetFileLastModifiedDateTask : ITask
    {
        private GeneralFileResource GeneralFileResource { get; }

        public DateTime LastModifiedDate { get; private set; }

        public GetFileLastModifiedDateTask(GeneralFileResource generalFileResource)
        {
            GeneralFileResource = generalFileResource;
        }

        public void Perform()
        {
            LastModifiedDate = File.GetLastWriteTime(GeneralFileResource.FilePath);
            Console.Out.WriteLineAsync($"[{nameof(GetFileLastModifiedDateTask)}] LastModifiedDate is {LastModifiedDate}");
        }

        public override string ToString()
        {
            return nameof(GetFileLastModifiedDateTask) + $": {LastModifiedDate}";
        }
    }
}
