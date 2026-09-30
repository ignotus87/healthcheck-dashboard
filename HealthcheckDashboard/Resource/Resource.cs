namespace HealthcheckDashboard.ResourceNS
{
    public enum ResourceType
    {
        GeneralFile,
        TextFile,
        ExcelFile,
        SqlQuery,
        WebServiceCall,
        Folder,
        Url,
        ConnectionStringWithQuery,
        LatestFile
    }
    public class Resource
    {
        private ResourceType _resourceType;

        public Resource(ResourceType resourceType)
        {
            _resourceType = resourceType;
        }
    }
}
