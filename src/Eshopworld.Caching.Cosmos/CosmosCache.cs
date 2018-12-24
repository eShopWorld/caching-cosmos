using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Eshopworld.Caching.Core;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Eshopworld.Caching.Cosmos
{
    public class CosmosCache
    {
        public enum InsertMode
        {
            /// <summary>
            /// Persist data as Envelop object with JSON model represented as a string property.
            /// </summary>
            JSON,

            /// <summary>
            /// Persists data as pure JSON model.
            /// </summary>
            Document,

            /// <summary>
            /// Use 'Document' mode for writing data. Auto-detect mode for reading data.
            /// </summary>
            Autodetect
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>Warning: As the cosmos SDK doesn't provide sync methods, all non async methods call their async counterparts and just GetResult().</remarks>
    public class CosmosCache<T> : IDistributedCache<T>
    {
        private static readonly FieldInfo PropertyBagField = typeof(Document).GetField("propertyBag", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly Uri _documentCollectionUri;
        private readonly bool _usePartitionKey;

        private readonly CosmosCache.InsertMode _insertMode;
        private readonly JsonSerializer _jsonSerializer;

        public DocumentClient Database { get; set; }

        public CosmosCache(Uri documentCollectionUri, DocumentClient documentClient)
            : this(documentCollectionUri, documentClient, CosmosCache.InsertMode.JSON, false, JsonSerializer.CreateDefault()) { }

        public CosmosCache(Uri documentCollectionUri, DocumentClient documentClient, CosmosCache.InsertMode insertMode, bool usePartitionKey, JsonSerializer jsonSerializer)
        {
            _documentCollectionUri = documentCollectionUri ?? throw new ArgumentNullException(nameof(documentCollectionUri));
            Database = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
            _insertMode = insertMode;
            _usePartitionKey = usePartitionKey;
            _jsonSerializer = jsonSerializer;
        }

        public T Add(CacheItem<T> item) => AddAsync(item).ConfigureAwait(false).GetAwaiter().GetResult();

        public async Task<T> AddAsync(CacheItem<T> item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var requestOptions = _usePartitionKey ? new RequestOptions { PartitionKey = new PartitionKey(item.Key) } : null;
            var docResponse = await Database.UpsertDocumentAsync(_documentCollectionUri, CreateDocument(item), requestOptions).ConfigureAwait(false);

            if (!(docResponse.StatusCode == HttpStatusCode.Created || docResponse.StatusCode == HttpStatusCode.OK))
            {
                throw new InvalidOperationException($"Unable to save document. Status:{docResponse.StatusCode}");
            }

            return item.Value;
        }

        private object CreateDocument(CacheItem<T> item)
        {
            var doc = new Document();
            var ttl = item.Duration != TimeSpan.MaxValue ? (int?)item.Duration.TotalSeconds : null;

            switch (_insertMode)
            {
                case CosmosCache.InsertMode.Document:
                    {
                        PropertyBagField.SetValue(doc, JObject.FromObject(item.Value)); // todo: there has to be a better way to do this. its either this, or call the internal 'FromObject' method..
                        doc.Id = item.Key;
                        doc.TimeToLive = ttl;

                        return doc;
                    }
                case CosmosCache.InsertMode.JSON:
                    {
                        return new Envelope(item.Key, JsonConvert.SerializeObject(item.Value), ttl);
                    }
                case CosmosCache.InsertMode.Autodetect:
                    {
                        PropertyBagField.SetValue(doc, JObject.FromObject(item.Value, _jsonSerializer));
                        doc.Id = item.Key;
                        doc.TimeToLive = ttl;

                        return doc;
                    }
                default:
                    throw new NotSupportedException($"InsertMode '{_insertMode}' is not supported!");
            }
        }

        public void Set(CacheItem<T> item) => Add(item);
        public Task SetAsync(CacheItem<T> item) => AddAsync(item);


        public bool Exists(string key) => GetResult(key).HasValue;
        public async Task<bool> ExistsAsync(string key) => (await GetResultAsync(key)).HasValue;

        public bool KeyExpire(string key, TimeSpan? expiry) => false;
        public Task<bool> KeyExpireAsync(string key, TimeSpan? expiry) => Task.FromResult(false);

        public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();
        public Task RemoveAsync(string key) => Database.DeleteDocumentAsync(CreateDocumentURI(key));

        public async Task<T> GetAsync(string key) => (await GetResultAsync(key)).Value;
        public T Get(string key) => GetResult(key).Value;

        public CacheResult<T> GetResult(string key) => GetResultAsync(key).GetAwaiter().GetResult();

        public async Task<CacheResult<T>> GetResultAsync(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            try
            {
                var pair = await GetDocument(key);

                return pair.statusCode != HttpStatusCode.OK
                    ? CacheResult<T>.Miss()
                    : new CacheResult<T>(true, pair.body);
            }
            catch (DocumentClientException ex) when (ex.StatusCode.HasValue && ex.StatusCode.Value == HttpStatusCode.NotFound)
            {
                return CacheResult<T>.Miss();
            }
        }

        private async Task<(HttpStatusCode statusCode, T body)> GetDocument(string key)
        {
            var documentUri = CreateDocumentURI(key);
            var requestOptions = _usePartitionKey ? new RequestOptions { PartitionKey = new PartitionKey(key) } : null;

            switch (_insertMode)
            {
                case CosmosCache.InsertMode.Document:
                    {
                        var documentResponse = await Database.ReadDocumentAsync<T>(documentUri, requestOptions).ConfigureAwait(false);
                        return (documentResponse.StatusCode, documentResponse.Document);
                    }
                case CosmosCache.InsertMode.JSON:
                    {
                        var documentResponse = await Database.ReadDocumentAsync<Envelope>(documentUri, requestOptions).ConfigureAwait(false);
                        return (documentResponse.StatusCode, JsonConvert.DeserializeObject<T>(documentResponse.Document.Blob));
                    }
                case CosmosCache.InsertMode.Autodetect:
                    {
                        var document = await Database.ReadDocumentAsync(documentUri, requestOptions).ConfigureAwait(false);
                        var resource = (dynamic)document.Resource;

                        var envelopeValue = ConverDynamicToValue<Envelope>(resource);
                        if (envelopeValue != null && !string.IsNullOrEmpty(envelopeValue.Blob))
                        {
                            var documentResponse = JsonConvert.DeserializeObject<T>(envelopeValue.Blob);
                            return (document.StatusCode, documentResponse);
                        }

                        var dynamicValue = ConverDocumentToValue<T>(resource);
                        if (!dynamicValue.Equals(default(T)))
                        {
                            return (document.StatusCode, dynamicValue);
                        }

                        throw new InvalidCastException("Cannot convert dynamic value to target type!");
                    }
                default:
                    throw new NotSupportedException($"InsertMode '{_insertMode}' is not supported!");
            }
        }

        public IEnumerable<KeyValuePair<string, T>> Get(IEnumerable<string> keys) => GetAsync(keys).ConfigureAwait(false).GetAwaiter().GetResult();
        public async Task<IEnumerable<KeyValuePair<string, T>>> GetAsync(IEnumerable<string> keys)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            using (var queryable = Database.CreateDocumentQuery<Envelope>(_documentCollectionUri)
                .Where(e => keys.Contains(e.id))
                .AsDocumentQuery())
            {
                var items = new Dictionary<string, T>();

                while (queryable.HasMoreResults)
                {
                    foreach (var e in await queryable.ExecuteNextAsync<Envelope>())
                    {
                        items[e.id] = (T)(object)e;
                    }
                }

                return items;
            }
        }

        public Uri CreateDocumentURI(string key) => new Uri($"{_documentCollectionUri}/docs/{Uri.EscapeUriString(key)}", UriKind.Relative);

        class Envelope
        {
            public string id { get; } // do not uppercase this, ddb requires it lower so the document id matches the preorder code
            public string Blob { get; }

            // used to set expiration policy
            [JsonProperty(PropertyName = "ttl", NullValueHandling = NullValueHandling.Ignore)]
            public int? TimeToLive { get; set; }

            public Envelope(string id, string blob, int? expiry = null)
            {
                this.id = id;
                Blob = blob;
                TimeToLive = expiry;
            }
        }

        private static T ConverDynamicToValue<T>(dynamic resource)
        {
            try
            {
                return (T)resource;
            }
            catch
            {
                ;
            }

            return default(T);
        }
        private T ConverDocumentToValue<T>(Document resource)
        {
            try
            {
                return JObject.Load(new JsonTextReader(new StringReader(resource.ToString())), new JsonLoadSettings()).ToObject<T>(_jsonSerializer);
            }
            catch
            {
                ;
            }

            return default(T);
        }
    }
}