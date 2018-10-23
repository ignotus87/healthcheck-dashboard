using System;
using System.IO;

namespace HealthcheckDashboard.Task
{
    class GetFileLastModifiedDateTask : ITask
    {
        private string _filePath;

        public DateTime LastModifiedDate { get; private set; }

        public GetFileLastModifiedDateTask(string filePath)
        {
            _filePath = filePath;
        }

        public void Perform()
        {
            LastModifiedDate = File.GetLastWriteTime(_filePath);
        }
    }
}
