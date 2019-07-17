using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

            DocumentClient = new DocumentClient(cosmosAccountEndpoint, cosmosAccountKey, GetConnectionPolicy(settings));
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

        private static ConnectionPolicy GetConnectionPolicy(CosmosCacheFactorySettings settings)
        {
            if (settings.MultiRegionReadWrite)
            {
                var connectionPolicy = new ConnectionPolicy
                {
                    UseMultipleWriteLocations = true,
                    EnableEndpointDiscovery = true
                };
                try
                {
                    connectionPolicy.SetCurrentLocation(settings.CurrentRegion);
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"unable to set current location to {settings.CurrentRegion}", e);
                }                

                return connectionPolicy;
            }

            return null;
        }

        private IndexingPolicy BuildIndexingPolicy()
        {
            if ((_settings.IndexingSettings?.ExcludedPaths?.Length ?? 0) == 0 && (_settings.IndexingSettings?.IncludedPaths?.Length ?? 0) == 0)
            {
                return new IndexingPolicy();
            }

            return new IndexingPolicy
            {
                Automatic = true,
                IndexingMode = IndexingMode.Consistent,

                ExcludedPaths = new Collection<ExcludedPath>(
                    _settings.IndexingSettings?.ExcludedPaths?.Select(path => new ExcludedPath { Path = path }).ToList() ?? new List<ExcludedPath>()),

                IncludedPaths = new Collection<IncludedPath>(
                    _settings.IndexingSettings?.IncludedPaths?.Select(path => new IncludedPath { Path = path }).ToList() ?? new List<IncludedPath>())
            };
        }

        private Uri TryCreateCollection(string name)
        {
            var db = DocumentClient.CreateDatabaseIfNotExistsAsync(new Database() {Id = _dbName}).ConfigureAwait(false).GetAwaiter().GetResult();

            var docCol = new DocumentCollection()
            {
                Id = name,
                DefaultTimeToLive = _settings.DefaultTimeToLive,
                IndexingPolicy = BuildIndexingPolicy()
            };

            if (_settings.UseKeyAsPartitionKey)
            {
                docCol.PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string>() {"/id"} };
            }

            var requestOptions = _settings.UseDatabaseSharedThroughput ? null : new RequestOptions {OfferThroughput = _settings.NewCollectionDefaultDTU};

            var dc = DocumentClient.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(_dbName), docCol, requestOptions)
                                   .ConfigureAwait(false)
                                   .GetAwaiter()
                                   .GetResult();

            return new Uri(dc.Resource.AltLink, UriKind.Relative);
        }

        public void Dispose() => DocumentClient.Dispose();
    }
}
