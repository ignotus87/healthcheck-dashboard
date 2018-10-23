namespace HealthcheckDashboard.Resource
{
    class GeneralFileResource : Resource
    {
        private string _filePath;

        public GeneralFileResource(string filePath) : base(ResourceType.GeneralFile)
        {
            _filePath = filePath;
        }
    }
}
