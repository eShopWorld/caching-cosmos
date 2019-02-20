namespace Eshopworld.Caching.Cosmos
{
    public class CosmosCacheFactorySettings
    {
        public CosmosCache.InsertMode InsertMode { get; set; } = CosmosCache.InsertMode.JSON;
        public int NewCollectionDefaultDTU { get; set; } = 400;
        public int DefaultTimeToLive { get; set; } = -1;  //never expire by default
        public bool UseKeyAsPartitionKey {get;set;} = false;
        public CosmosCacheFactoryIndexingSettings IndexingSettings { get; set; } = new CosmosCacheFactoryIndexingSettings();

        public static readonly CosmosCacheFactorySettings Default = new CosmosCacheFactorySettings();
    }
}