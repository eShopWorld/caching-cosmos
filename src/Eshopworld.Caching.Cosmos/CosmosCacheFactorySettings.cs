namespace Eshopworld.Caching.Cosmos
{
    public class CosmosCacheFactorySettings
    {
        public CosmosCache.InsertMode InsertMode { get; set; } = CosmosCache.InsertMode.JSON;
        public int NewCollectionDefaultDTU { get; set; } = 400;
        public int DefaultTimeToLive { get; set; } = -1;  //never expire by default
        public bool UseKeyAsPartitionKey { get; set; }
        public CosmosCacheFactoryIndexingSettings IndexingSettings { get; set; } = new CosmosCacheFactoryIndexingSettings();
        /// <summary>
        /// If the Database does NOT have a user-defined RU setting and this setting is set to true,
        /// any on-the-fly created Collection will still default to 400 RUs
        /// </summary>
        public bool DatabaseSharedRUs { get; set; }

        public static readonly CosmosCacheFactorySettings Default = new CosmosCacheFactorySettings();
    }
}
