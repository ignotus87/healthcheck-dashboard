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
        Url,
        ConnectionStringWithQuery
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
