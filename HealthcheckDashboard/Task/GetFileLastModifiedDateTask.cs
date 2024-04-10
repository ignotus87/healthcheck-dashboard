using System;
using System.IO;
using HealthcheckDashboard.ResourceNS;

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
        }

        public override string ToString()
        {
            return nameof(GetFileLastModifiedDateTask);
        }
    }
}
