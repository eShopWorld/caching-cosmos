namespace Eshopworld.Caching.Cosmos
{
    public class CosmosCacheFactoryIndexingSettings
    {
        public string[] IncludedPaths { get; set; }
        public string[] ExcludedPaths { get; set; }
        public bool IsKeyValueStore { get; set; }
    }
}
