using HealthcheckDashboard.ResourceNS;
using System;
using System.Net.Http;

namespace HealthcheckDashboard.TaskNS
{
    class MakeWebRequestTask : ITask
    {
        private UrlResource UrlResource { get; }
        public string LastResult { get; private set; }

        public MakeWebRequestTask(UrlResource urlResource)
        {
            UrlResource = urlResource;
        }

        public void Perform()
        {
            try
            {
                using var client = new HttpClient();
                // synchronous wait is OK here because Perform() runs on a background thread
                LastResult = client.GetStringAsync(UrlResource.Url).GetAwaiter().GetResult();
                Console.Out.WriteLineAsync($"[{nameof(MakeWebRequestTask)}] Fetched {UrlResource.Url} (length: {LastResult?.Length ?? 0})");
            }
            catch (Exception ex)
            {
                LastResult = null;
                throw new InvalidOperationException($"Failed to fetch URL {UrlResource.Url}: {ex.Message}", ex);
            }
        }

        public override string ToString()
        {
            return nameof(MakeWebRequestTask);
        }
    }
}