using HealthcheckDashboard.ResourceNS;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HealthcheckDashboard.TaskNS
{
    class MakeWebRequestTask : ITask
    {
        public string Name { get; }
        private UrlResource UrlResource { get; }
        public string LastResult { get; private set; }

        public MakeWebRequestTask(string name, UrlResource urlResource)
        {
            Name = name;
            UrlResource = urlResource;
        }

        public async Task PerformAsync()
        {
            try
            {
                using var client = new HttpClient();
                // await the async web request
                LastResult = await client.GetStringAsync(UrlResource.Url);
            }
            catch (Exception ex)
            {
                LastResult = null;
                throw new InvalidOperationException($"Failed to fetch URL {UrlResource.Url}: {ex.Message}", ex);
            }
        }

        public override string ToString()
        {
            return nameof(MakeWebRequestTask) + " " + Name;
        }
    }
}