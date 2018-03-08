﻿using System;
using Eshopworld.Caching.Core;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;

namespace Eshopworld.Caching.Cosmos
{
    public class CosmosCacheFactory : ICacheFactory, IDisposable
    {
        private readonly string _dbName;
        private readonly DocumentClient _documentClient;

        public int NewCollectionDefaultDTU { get; set; } = 400;

        public CosmosCacheFactory(Uri cosmosAccountEndpoint, string cosmosAccountKey, string dbName)
        {
            _dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));

            _documentClient = new DocumentClient(cosmosAccountEndpoint, cosmosAccountKey);
        }

        public ICache<T> CreateDefault<T>() => Create<T>(typeof(T).Name);

        public ICache<T> Create<T>(string name)
        {
            // todo: need to handle partition key, size etc
            var dc = _documentClient.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(_dbName), new DocumentCollection() { Id = name }, new RequestOptions() { OfferThroughput = NewCollectionDefaultDTU }).GetAwaiter().GetResult();
            
            return new CosmosCache<T>(new Uri(dc.Resource.AltLink,UriKind.Relative), _documentClient);
        }

        public void Dispose() => _documentClient.Dispose();
    }
}