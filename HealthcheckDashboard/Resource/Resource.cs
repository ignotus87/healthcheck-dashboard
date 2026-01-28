namespace HealthcheckDashboard.ResourceNS
{
    enum ResourceType
    {
        GeneralFile,
        TextFile,
        ExcelFile,
        SqlQuery,
        WebServiceCall,
        Folder,
        Url
    }
    class Resource
    {
        private ResourceType _resourceType;

        public Resource(ResourceType resourceType)
        {
            _resourceType = resourceType;
        }
    }
}
