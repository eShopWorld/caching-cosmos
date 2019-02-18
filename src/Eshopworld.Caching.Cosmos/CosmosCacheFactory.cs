using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Eshopworld.Caching.Core;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Eshopworld.Caching.Cosmos
{
    public class CosmosCacheFactory : ICacheFactory, IDisposable
    {
        private readonly string _dbName;
        private readonly CosmosCacheFactorySettings _settings;
        private readonly ConcurrentDictionary<string, Uri> documentCollectionURILookup = new ConcurrentDictionary<string, Uri>();
        
        private static readonly IndexingPolicy DefaultIndexingPolicy = new IndexingPolicy();

        public DocumentClient DocumentClient { get; }

        [Obsolete("Use CosmosCacheFactorySettings in ctor instead")]
        public int NewCollectionDefaultDTU
        {
            get => _settings.NewCollectionDefaultDTU;
            set => _settings.NewCollectionDefaultDTU = value;
        }

        public CosmosCacheFactory(Uri cosmosAccountEndpoint, string cosmosAccountKey, string dbName, CosmosCacheFactorySettings settings)
        {
            _dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
            _settings = settings;

            DocumentClient = new DocumentClient(cosmosAccountEndpoint, cosmosAccountKey);
        }
        public CosmosCacheFactory(Uri cosmosAccountEndpoint, string cosmosAccountKey, string dbName) : this(cosmosAccountEndpoint, cosmosAccountKey, dbName, CosmosCacheFactorySettings.Default){}



        public ICache<T> CreateDefault<T>() => Create<T>(typeof(T).Name);

        public ICache<T> Create<T>(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if(_settings.InsertMode == CosmosCache.InsertMode.Document && Type.GetTypeCode(typeof(T)) != TypeCode.Object) throw new ArgumentOutOfRangeException("T",$"Primitive type '{typeof(T)}' not supported. Non primitive types only (i.e. a class)");

            var documentCollectionURI = documentCollectionURILookup.GetOrAdd(name, TryCreateCollection);

            return BuildCacheInstance<T>(documentCollectionURI);
        }

        protected virtual ICache<T> BuildCacheInstance<T>(Uri documentCollectionUri)
        {
            return new CosmosCache<T>(documentCollectionUri, DocumentClient, _settings.InsertMode, _settings.UseKeyAsPartitionKey);
        }

        protected virtual IndexingPolicy GetCollectionIndexingPolicy(string dbName, string collectionName) => DefaultIndexingPolicy;

        private Uri TryCreateCollection(string name)
        {
            var db = DocumentClient.CreateDatabaseIfNotExistsAsync(new Database() {Id = _dbName}).ConfigureAwait(false).GetAwaiter().GetResult();

            var docCol = new DocumentCollection()
            {
                Id = name,
                DefaultTimeToLive = _settings.DefaultTimeToLive,
                IndexingPolicy = GetCollectionIndexingPolicy(_dbName, name) ?? DefaultIndexingPolicy
            };

            if (_settings.UseKeyAsPartitionKey)
            {
                docCol.PartitionKey = new PartitionKeyDefinition() {Paths = new Collection<string>() {"/id"}};
            }

            var dc = DocumentClient.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(_dbName), docCol,new RequestOptions() {OfferThroughput = _settings.NewCollectionDefaultDTU})
                                   .ConfigureAwait(false)
                                   .GetAwaiter()
                                   .GetResult();

            return new Uri(dc.Resource.AltLink, UriKind.Relative);
        }

        public void Dispose() => DocumentClient.Dispose();
    }
}