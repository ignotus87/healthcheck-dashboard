namespace HealthcheckDashboard.ResourceNS
{
    class GeneralFileResource : Resource
    {
        public string FilePath { get; }

        public GeneralFileResource(string filePath) : base(ResourceType.GeneralFile)
        {
            FilePath = filePath;
        }

        public override string ToString()
        {
            return nameof(GeneralFileResource) + ": " + FilePath;
        }
    }
}
