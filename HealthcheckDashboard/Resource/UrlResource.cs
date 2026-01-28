namespace HealthcheckDashboard.ResourceNS
{
    class UrlResource : Resource
    {
        public string Url { get; }

        public UrlResource(string url) : base(ResourceType.Url)
        {
            Url = url;
        }

        public override string ToString()
        {
            return nameof(UrlResource) + ": " + Url;
        }
    }
}