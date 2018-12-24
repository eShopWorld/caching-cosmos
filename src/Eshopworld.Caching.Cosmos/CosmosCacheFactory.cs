using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Eshopworld.Caching.Core;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;

namespace Eshopworld.Caching.Cosmos
{
    public class CosmosCacheFactory : ICacheFactory, IDisposable
    {
        private readonly string _dbName;
        private readonly CosmosCacheFactorySettings _settings;
        private readonly ConcurrentDictionary<string, Uri> _documentCollectionUriLookup = new ConcurrentDictionary<string, Uri>();
        private readonly JsonSerializer _jsonSerializer;

        public DocumentClient DocumentClient { get; }

        [Obsolete("Use CosmosCacheFactorySettings in ctor instead")]
        public int NewCollectionDefaultDTU
        {
            get => _settings.NewCollectionDefaultDTU;
            set => _settings.NewCollectionDefaultDTU = value;
        }

        public CosmosCacheFactory(Uri cosmosAccountEndpoint, string cosmosAccountKey, string dbName, CosmosCacheFactorySettings settings, JsonSerializer jsonSerializer)
        {
            _dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
            _settings = settings;
            _jsonSerializer = jsonSerializer;

            DocumentClient = new DocumentClient(cosmosAccountEndpoint, cosmosAccountKey);
        }

        public CosmosCacheFactory(Uri cosmosAccountEndpoint, string cosmosAccountKey, string dbName) 
            : this(cosmosAccountEndpoint, cosmosAccountKey, dbName, CosmosCacheFactorySettings.Default, JsonSerializer.CreateDefault()){}

        public ICache<T> CreateDefault<T>() => Create<T>(typeof(T).Name);

        public ICache<T> Create<T>(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if(_settings.InsertMode == CosmosCache.InsertMode.Document && Type.GetTypeCode(typeof(T)) != TypeCode.Object) 
                throw new ArgumentOutOfRangeException("T", $"Primitive type '{typeof(T)}' not supported. Non primitive types only (i.e. a class)");

            var documentCollectionUri = _documentCollectionUriLookup.GetOrAdd(name, TryCreateCollection);

            return new CosmosCache<T>(documentCollectionUri, DocumentClient, _settings.InsertMode,_settings.UseKeyAsPartitionKey, _jsonSerializer);
        }

        private Uri TryCreateCollection(string name)
        {
            DocumentClient.CreateDatabaseIfNotExistsAsync(new Database {Id = _dbName}).ConfigureAwait(false).GetAwaiter().GetResult();

            var docCol = new DocumentCollection
            {
                Id = name,
                DefaultTimeToLive = _settings.DefaultTimeToLive
            };

            if (_settings.UseKeyAsPartitionKey)
            {
                docCol.PartitionKey = new PartitionKeyDefinition {Paths = new Collection<string> {"/id"}};
            }

            var dc = DocumentClient.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(_dbName), docCol,new RequestOptions {OfferThroughput = _settings.NewCollectionDefaultDTU})
                                   .ConfigureAwait(false)
                                   .GetAwaiter()
                                   .GetResult();

            return new Uri(dc.Resource.AltLink, UriKind.Relative);
        }

        public void Dispose() => DocumentClient.Dispose();
    }
}