using System;
using Eshopworld.Tests.Core;
using Newtonsoft.Json;
using Xunit;

namespace Eshopworld.Caching.Cosmos.Tests
{
    public class CosmosCacheFactoryTests
    {
        [Fact, IsUnit]
        public void Create_WithDocumentDirectAndPrimitiveType_RaisesException()
        {
            // Arrange
            using (var factory = new CosmosCacheFactory(LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName,new CosmosCacheFactorySettings() { InsertMode = CosmosCache.InsertMode.Document}, JsonSerializer.CreateDefault()))
            {
                // Assert
                Assert.Throws<ArgumentOutOfRangeException>(() => factory.Create<string>(""));
            }
        }

        [Fact,IsIntegration]
        public void NewInstance_WithValidConnectionString_NoException()
        {
            // Arrange
            // Act
            using (new CosmosCacheFactory(LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName)) ;
            // Assert
        }

        [Fact,IsIntegration]
        public void Create_CosmosCache_NoException()
        {
            // Arrange
            using (var factory = new CosmosCacheFactory(LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName))
            {
                // Act
                var instance = factory.Create<SimpleObject>("testCache");

                // Assert
                Assert.IsType<CosmosCache<SimpleObject>>(instance);
            }
        }

        [Fact,IsIntegration]
        public void Create_CosmosCacheMultipleTimes_NoException()
        {
            // Arrange
            using (var factory = new CosmosCacheFactory(LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName))
            {
                // Act
                for (int i = 0; i < 10; i++)
                {
                    factory.Create<SimpleObject>("testCache");
                }

                // Assert
                // should not throw
            }
        }

        [Fact,IsIntegration]
        public void Create_WithNonExistingCollection_NewCollectionIsCreated()
        {
            var tempCollectionName = Guid.NewGuid().ToString();

            // Arrange
            using (var factory = new CosmosCacheFactory(LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName))
            {
                // Act
                factory.Create<SimpleObject>(tempCollectionName);

                Assert.Equal(System.Net.HttpStatusCode.OK, factory.DocumentClient.ReadDocumentCollectionAsync(new Uri($"dbs/test-db/colls/{tempCollectionName}",UriKind.Relative)).GetAwaiter().GetResult().StatusCode);
            }
        }
    }
 }