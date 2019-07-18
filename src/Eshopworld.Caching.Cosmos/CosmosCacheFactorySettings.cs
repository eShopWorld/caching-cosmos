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
        /// Enables using shared throughput provisioned on a database.
        /// </summary>
        /// <remarks>
        /// When disabled, collections will be created with dedicated throughput defined by <see cref="NewCollectionDefaultDTU" />.
        /// When enabled new collections will share the throughput provisioned on the parent database.
        /// If the database has no provisioned throughput then new collections will have the dedicated default throughput (400 RUs).
        /// </remarks>
        public bool UseDatabaseSharedThroughput { get; set; }

        public static readonly CosmosCacheFactorySettings Default = new CosmosCacheFactorySettings();
    }
}
