using System;
using System.Collections.Generic;
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
using Document = Microsoft.Azure.Documents.Document;

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
            Document
        }

        internal static readonly FieldInfo PropertyBagField = typeof(Document).GetField("propertyBag", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <remarks>Warning: As the cosmos SDK doesn't provide sync methods, all non async methods call their async counterparts and just GetResult().</remarks>
    public class CosmosCache<T> : IDistributedCache<T>
    {
        private readonly Uri _documentCollectionUri;
        private readonly bool _usePartitionKey;

        private readonly CosmosCache.InsertMode _insertMode;

        [Obsolete("Please use property DocumentClient instead Database.")]
        public DocumentClient Database => DocumentClient;

        public DocumentClient DocumentClient { get; }

        public CosmosCache(Uri documentCollectionUri, DocumentClient documentClient) : this(documentCollectionUri, documentClient, CosmosCache.InsertMode.JSON,false) { }

        public CosmosCache(Uri documentCollectionUri, DocumentClient documentClient, CosmosCache.InsertMode _insertMode, bool usePartitionKey)
        {
            _documentCollectionUri = documentCollectionUri ?? throw new ArgumentNullException(nameof(documentCollectionUri));
            DocumentClient = documentClient ?? throw new ArgumentNullException(nameof(documentClient));
            this._insertMode = _insertMode;
            this._usePartitionKey = usePartitionKey;
        }

        public T Add(CacheItem<T> item) => AddAsync(item).ConfigureAwait(false).GetAwaiter().GetResult();
        
        public async Task<T> AddAsync(CacheItem<T> item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var requestOptions = _usePartitionKey ? new RequestOptions() { PartitionKey = new PartitionKey(item.Key) } : null;
            var docResponse = await DocumentClient.UpsertDocumentAsync(_documentCollectionUri, CreateDocument(item), requestOptions).ConfigureAwait(false);

            if (!(docResponse.StatusCode == HttpStatusCode.Created || docResponse.StatusCode == HttpStatusCode.OK))
            {
                throw new InvalidOperationException($"Unable to save document. Status:{docResponse.StatusCode}");
            }

            return item.Value;
        }

        private object CreateDocument(CacheItem<T> item)
        {
            var ttl = item.Duration != TimeSpan.MaxValue ? (int?)item.Duration.TotalSeconds : null;

            if (_insertMode == CosmosCache.InsertMode.Document)
            {
                var doc = new Document();
                CosmosCache.PropertyBagField.SetValue(doc, JObject.FromObject(item.Value)); // todo: there has to be a better way to do this. its either this, or call the internal 'FromObject' method..
                doc.Id = item.Key;
                doc.TimeToLive = ttl;

                return doc;
            }
            else if (_insertMode == CosmosCache.InsertMode.JSON)
            {
                return new Envelope(item.Key, JsonConvert.SerializeObject(item.Value), ttl);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(_insertMode), _insertMode, "Condition not supported");
            }
        }

        public void Set(CacheItem<T> item) => Add(item);
        public Task SetAsync(CacheItem<T> item) => AddAsync(item);


        public bool Exists(string key) => GetResult(key).HasValue;
        public async Task<bool> ExistsAsync(string key) => (await GetResultAsync(key).ConfigureAwait(false)).HasValue;

        public bool KeyExpire(string key, TimeSpan? expiry) => false;
        public Task<bool> KeyExpireAsync(string key, TimeSpan? expiry) => Task.FromResult(false);

        public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();

        public async Task RemoveAsync(string key)
        {
            var requestOptions = _usePartitionKey ? new RequestOptions() { PartitionKey = new PartitionKey(key) } : null;
            await DocumentClient.DeleteDocumentAsync(CreateDocumentURI(key), requestOptions).ConfigureAwait(false);
        }

        public async Task<T> GetAsync(string key) => (await GetResultAsync(key).ConfigureAwait(false)).Value;
        public T Get(string key) => GetResult(key).Value;

        public CacheResult<T> GetResult(string key) => GetResultAsync(key).ConfigureAwait(false).GetAwaiter().GetResult();

        public async Task<CacheResult<T>> GetResultAsync(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            try
            {
                var pair = await GetDocument(key).ConfigureAwait(false);

                return pair.statusCode != HttpStatusCode.OK
                    ? CacheResult<T>.Miss()
                    : new CacheResult<T>(true, pair.body);
            }
            catch (DocumentClientException ex) when (ex.StatusCode.HasValue && ex.StatusCode.Value == HttpStatusCode.NotFound)
            {
                return CacheResult<T>.Miss();
            }
        }

        protected virtual async Task<(HttpStatusCode statusCode, T body)> GetDocument(string key)
        {
            var documentUri = CreateDocumentURI(key);
            var requestOptions = _usePartitionKey ? new RequestOptions() { PartitionKey = new PartitionKey(key) } : null;

            if (_insertMode == CosmosCache.InsertMode.JSON)
            {
                var documentResponse = await DocumentClient.ReadDocumentAsync<Envelope>(documentUri, requestOptions).ConfigureAwait(false);
                return (documentResponse.StatusCode, JsonConvert.DeserializeObject<T>(documentResponse.Document.Blob));
            }
            else if (_insertMode == CosmosCache.InsertMode.Document)
            {
                var documentResponse = await DocumentClient.ReadDocumentAsync<T>(documentUri, requestOptions).ConfigureAwait(false);
                return (documentResponse.StatusCode, documentResponse.Document);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(_insertMode), _insertMode, "Condition not supported");
            }
        }

        public IEnumerable<KeyValuePair<string, T>> Get(IEnumerable<string> keys) => GetAsync(keys).ConfigureAwait(false).GetAwaiter().GetResult();
        public async Task<IEnumerable<KeyValuePair<string, T>>> GetAsync(IEnumerable<string> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));

            using (var queryable = DocumentClient.CreateDocumentQuery<Document>(_documentCollectionUri)
                .Where(e => keys.Contains(e.Id))
                .AsDocumentQuery())
            {
                var items = new Dictionary<string, T>();

                while (queryable.HasMoreResults)
                {
                    foreach (var document in await queryable.ExecuteNextAsync<Document>().ConfigureAwait(false))
                    {
                        items[document.Id] = ChangeType<T>(document);
                    }
                }

                return items;
            }
        }

        public Uri CreateDocumentURI(string key) => new Uri($"{_documentCollectionUri}/docs/{Uri.EscapeUriString(key)}", UriKind.Relative);

        protected class Envelope
        {
            public string id { get; } // do not uppercase this, db requires it lower so the document id matches the preorder code
            public string Blob { get; }

            // used to set expiration policy
            [JsonProperty(PropertyName = "ttl", NullValueHandling = NullValueHandling.Ignore)]
            public int? TimeToLive { get; set; }

            public Envelope(string id, string blob, int? expiry = null)
            {
                this.id = id;
                this.Blob = blob;
                TimeToLive = expiry;
            }
        }

        protected TResult ChangeType<TResult>(Document document)
        {
            var jObject = (JObject)CosmosCache.PropertyBagField.GetValue(document);

            var result = jObject.ContainsKey(nameof(Envelope.Blob))
                ? JsonConvert.DeserializeObject<TResult>(jObject[nameof(Envelope.Blob)].ToString())
                : jObject.ToObject<TResult>();

            return result;
        }
    }
}