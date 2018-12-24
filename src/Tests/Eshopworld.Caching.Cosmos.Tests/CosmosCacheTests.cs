using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eshopworld.Caching.Core;
using Eshopworld.Tests.Core;
using Newtonsoft.Json;
using Xunit;

namespace Eshopworld.Caching.Cosmos.Tests
{
    public class CosmosCacheTests
    {
        private const string CacheKey = "item";
        private CosmosCacheFactory cacheFactory;
        private CosmosCacheFactory docDirectCacheFactory;
        private CosmosCacheFactory autodetectDirectCacheFactory;
        private CosmosCache<string> stringCache;
        public CosmosCacheTests()
        {
            cacheFactory = new CosmosCacheFactory(LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName);
            docDirectCacheFactory = new CosmosCacheFactory(LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName, new CosmosCacheFactorySettings() { InsertMode = CosmosCache.InsertMode.Document }, JsonSerializer.CreateDefault());
            autodetectDirectCacheFactory = new CosmosCacheFactory(LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName, new CosmosCacheFactorySettings() { InsertMode = CosmosCache.InsertMode.Autodetect }, JsonSerializer.CreateDefault());

            stringCache = (CosmosCache<string>)cacheFactory.Create<string>("string-collection");

        }

        public static IEnumerable<object[]> GetInsertModes()
        {
            yield return new object[] { CosmosCache.InsertMode.JSON };
            yield return new object[] { CosmosCache.InsertMode.Document };
        }

        [Fact, IsIntegration]
        public void SetString_DoesNotThrow()
        {
            // Arrange
            // Act
            stringCache.Set(new CacheItem<string>(CacheKey, "Test", TimeSpan.FromSeconds(5)));
            // Assert
        }

        [Fact, IsIntegration]
        public async Task SetAsyncString_DoesNotThrow()
        {
            // Arrange
            // Act
            await stringCache.SetAsync(new CacheItem<string>(CacheKey, "Test", TimeSpan.FromSeconds(5)));
            // Assert
        }


        [Fact, IsIntegration]
        public void SetStringWithTimeout_ItemDoesNotExistAfterTimeout()
        {
            // Arrange
            // Act
            var duration = TimeSpan.FromSeconds(3);
            stringCache.Set(new CacheItem<string>(CacheKey, "Test", duration));

            // Assert
            Assert.True(stringCache.Exists(CacheKey));
            System.Threading.Thread.Sleep(duration.Add(TimeSpan.FromSeconds(1)));
            Assert.False(stringCache.Exists(CacheKey));
        }

        [Fact, IsIntegration]
        public void GetString_ItemExistsInCache()
        {
            // Arrange
            var cacheValue = "Test";


            stringCache.Set(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            var result = stringCache.Get(CacheKey);

            // Assert
            Assert.Equal(result, cacheValue);
        }

        [Fact, IsIntegration]
        public async Task GetAsyncString_ItemExistsInCache()
        {
            // Arrange
            var cacheValue = "Test";

            stringCache.Set(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            var result = await stringCache.GetAsync(CacheKey);

            // Assert
            Assert.Equal(result, cacheValue);
        }




        [Fact, IsIntegration]
        public void Remove_AfterRemove_GetReturnsNull()
        {
            // Arrange
            var cacheValue = "Test";


            stringCache.Set(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            stringCache.Remove(CacheKey);

            // Assert
            var result = stringCache.Get(CacheKey);
            Assert.Null(result);
        }

        [Fact, IsIntegration]
        public async Task RemoveAsync_AfterRemove_GetReturnsNull()
        {
            // Arrange
            var cacheValue = "Test";


            await stringCache.SetAsync(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            await stringCache.RemoveAsync(CacheKey);

            // Assert
            var result = await stringCache.GetAsync(CacheKey);
            Assert.Null(result);
        }



        [Fact, IsIntegration]
        public void Exists_AfterAdding_ReturnsTrue()
        {
            // Arrange
            var cacheValue = "Test";


            stringCache.Set(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            // Assert
            Assert.True(stringCache.Exists(CacheKey));
        }

        [Fact, IsIntegration]
        public async Task ExistsAsync_AfterAdding_ReturnsTrue()
        {
            // Arrange
            var cacheValue = "Test";


            stringCache.Set(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            // Assert
            Assert.True(await stringCache.ExistsAsync(CacheKey));
        }

        [Fact, IsIntegration]
        public void Exists_AfterAddingAndRemoving_ReturnsFalse()
        {
            // Arrange
            var cacheValue = "Test";


            stringCache.Set(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));
            stringCache.Remove(CacheKey);
            // Act
            // Assert
            Assert.False(stringCache.Exists(CacheKey));
        }

        [Fact, IsIntegration]
        public async Task Exists_AfterAddingAndRemoving_GetReturnsNull()
        {
            // Arrange
            var cacheValue = "Test";


            stringCache.Set(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            // Assert
            Assert.True(await stringCache.ExistsAsync(CacheKey));
        }


        [Fact(Skip = "Not supported by cosmos implementation (yet)")]
        public void Expire_AfterSettingExpireAndWaiting_ItemDoesntExistInCache()
        {
            // Arrange
            var cacheValue = "Test";
            var expireIn = new TimeSpan(0, 0, 2);

            stringCache.Set(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            stringCache.KeyExpire(CacheKey, expireIn);
            System.Threading.Thread.Sleep(expireIn.Add(TimeSpan.FromSeconds(1)));

            // Assert
            Assert.False(stringCache.Exists(CacheKey));
        }

        [Fact(Skip = "Not supported by cosmos implementation (yet)")]
        public async Task ExpireAsync_AfterSettingExpireAndWaiting_ItemDoesntExistInCache()
        {
            // Arrange
            var cacheValue = "Test";
            var expireIn = new TimeSpan(0, 0, 2);

            await stringCache.SetAsync(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            await stringCache.KeyExpireAsync(CacheKey, expireIn);
            await Task.Delay(expireIn.Add(TimeSpan.FromSeconds(1)));


            // Assert
            Assert.False(await stringCache.ExistsAsync(CacheKey));
        }

        [Theory, IsIntegration]
        [MemberData(nameof(GetInsertModes))]
        public void Get_SimpleObject_ReturnedObjectIsIdentical(CosmosCache.InsertMode mode)
        {
            // Arrange
            var cache = CreateCache<SimpleObject>(mode);
            var value = SimpleObject.Create();
            cache.Set(new CacheItem<SimpleObject>(CacheKey, value, TimeSpan.FromSeconds(5)));

            // Act
            var result = cache.Get(CacheKey);

            // Assert
            Assert.False(object.ReferenceEquals(result, value));
            Assert.Equal(result, value);
        }

        [Fact, IsUnit]
        public void Get_SimpleObjectWhenAutodetectUsed_ReturnedObjectIsIdentical()
        {
            // Arrange
            var cacheJson = CreateCache<SimpleObject>(CosmosCache.InsertMode.JSON);
            var value = SimpleObject.Create();
            cacheJson.Set(new CacheItem<SimpleObject>(CacheKey, value, TimeSpan.FromSeconds(5)));

            var cacheAutodetect = CreateCache<SimpleObject>(CosmosCache.InsertMode.Autodetect);

            // Act
            var result = cacheAutodetect.Get(CacheKey);

            // Assert
            Assert.False(ReferenceEquals(result, value));
            Assert.Equal(result, value);
        }

        [Theory, IsIntegration]
        [MemberData(nameof(GetInsertModes))]
        public void Get_ComplexObject_ReturnedObjectIsIdentical(CosmosCache.InsertMode mode)
        {
            // Arrange
            var cache = CreateCache<ComplexObject>(mode);

            var value = ComplexObject.Create();
            cache.Set(new CacheItem<ComplexObject>(CacheKey, value, TimeSpan.FromSeconds(5)));

            // Act
            var result = cache.Get(CacheKey);

            // Assert
            Assert.False(object.ReferenceEquals(result, value));
            Assert.Equal(result, value);
        }


        [Fact, IsIntegration]
        public void GetResult_SimpleObject_ReturnedObjectIsIdentical()
        {
            // Arrange
            var cache = cacheFactory.CreateDefault<SimpleObject>();
            var value = SimpleObject.Create();
            cache.Set(new CacheItem<SimpleObject>(CacheKey, value, TimeSpan.FromSeconds(5)));

            // Act
            var result = cache.GetResult(CacheKey);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasValue);
            Assert.False(object.ReferenceEquals(result.Value, value));
            Assert.Equal(result.Value, value);
        }

        [Fact, IsIntegration]
        public async Task GetResultAsync_SimpleObject_ReturnedObjectIsIdentical()
        {
            // Arrange
            var cache = cacheFactory.CreateDefault<SimpleObject>();
            var value = SimpleObject.Create();
            await cache.SetAsync(new CacheItem<SimpleObject>(CacheKey, value, TimeSpan.FromSeconds(5)));

            // Act
            var result = await cache.GetResultAsync(CacheKey);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.HasValue);
            Assert.False(object.ReferenceEquals(result.Value, value));
            Assert.Equal(result.Value, value);
        }

        [Fact, IsIntegration]
        public void GetResult_MissingObject_ResultDoesNotHaveValue()
        {
            // Arrange
            var cache = cacheFactory.CreateDefault<SimpleObject>();

            // Act
            var result = cache.GetResult("doesntExist");

            // Assert
            Assert.NotNull(result);
            Assert.False(result.HasValue);
            Assert.Null(result.Value);
        }


        [Fact, IsIntegration]
        public void Set_MultipeTasks_NoExceptions()
        {
            // Arrange
            const int numberOfItems = 100;
            var items = Enumerable.Range(0, numberOfItems).Select(i => SimpleObject.Create()).ToArray();
            var cache = cacheFactory.CreateDefault<SimpleObject>();

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var loopResult = Parallel.For(0, 20, i =>
            {
                var index = i % numberOfItems;
                var item = items[index];
                cache.Set(new CacheItem<SimpleObject>("item-" + index, item, TimeSpan.FromSeconds(5)));
            });
            stopwatch.Stop();

            // Assert
            Console.WriteLine("Duration:" + stopwatch.ElapsedMilliseconds);
            Assert.True(loopResult.IsCompleted);
        }

        [Fact, IsIntegration]
        public void Get_MultipeTasks_NoExceptions()
        {
            // Arrange
            const int numberOfItems = 100;
            var cache = cacheFactory.CreateDefault<SimpleObject>();

            Enumerable.Range(0, numberOfItems)
                .Select(i => Tuple.Create("item-" + i, SimpleObject.Create()))
                .All(_ => { cache.Set(new CacheItem<SimpleObject>(_.Item1, _.Item2, TimeSpan.FromSeconds(5))); return true; });

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var loopResult = Parallel.For(0, 20, i =>
            {
                var index = i % numberOfItems;
                var result = cache.Get("item-" + index);

            });

            stopwatch.Stop();

            // Assert
            Console.WriteLine("Duration:" + stopwatch.ElapsedMilliseconds);
            Assert.True(loopResult.IsCompleted);
        }

        [Fact, IsIntegration]
        public async Task AddAsync_Set_ExpiryTimeAtDocumentLevel_ItemDoesntExistInCache()
        {
            // Arrange
            var cacheValue = "Test";

            await stringCache.AddAsync(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Act
            await Task.Delay(TimeSpan.FromSeconds(7));


            // Assert
            Assert.False(await stringCache.ExistsAsync(CacheKey));
        }

        [Fact, IsIntegration]
        public async Task AddAsync_Set_ExpiryTimeAtDocumentLevel_ItemExistInCache()
        {
            // Arrange
            var cacheValue = "Test";

            await stringCache.AddAsync(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(5)));

            // Assert
            Assert.True(await stringCache.ExistsAsync(CacheKey));
        }

        [Fact, IsIntegration]
        public async Task AddAsync_No_ExpiryTimeSet()
        {
            // Arrange
            var cacheValue = "Test";

            await stringCache.AddAsync(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.MaxValue));

            // Assert
            Assert.True(await stringCache.ExistsAsync(CacheKey));
        }

        [Fact, IsIntegration]
        public async Task AddAsync_WithCacheThatHasDefaultTTL_DocumentAutoExpires()
        {
            // Arrange
            var autoExpireCache = CreateTTLCache(TimeSpan.FromSeconds(2));
            var cacheValue = "Test";

            // Act
            await autoExpireCache.AddAsync(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.MaxValue));
            await Task.Delay(TimeSpan.FromSeconds(4));

            // Assert
            Assert.False(await autoExpireCache.ExistsAsync(CacheKey));
        }

        [Fact, IsIntegration]
        public async Task AddAsync_WithCacheThatHasDefaultTTLAndDocumentWithExplicitTTL_DocumentExpiresWithDocumentTTL()
        {
            // Arrange
            var autoExpireCache = CreateTTLCache(TimeSpan.FromSeconds(2));
            var cacheValue = "Test";

            // Act
            await autoExpireCache.AddAsync(new CacheItem<string>(CacheKey, cacheValue, TimeSpan.FromSeconds(6)));
            await Task.Delay(TimeSpan.FromSeconds(4));

            // Assert
            Assert.True(await autoExpireCache.ExistsAsync(CacheKey));

            await Task.Delay(TimeSpan.FromSeconds(4));
            Assert.False(await autoExpireCache.ExistsAsync(CacheKey));
        }

        [Fact, IsIntegration]
        public async Task AddAsync_DocumentDirectInsertMode_RecordIsNotJsonBlob()
        {
            // Arrange
            var ddCache = CreateDocumentDirectCache<SimpleObject>();
            var value = SimpleObject.Create();

            await ddCache.AddAsync(new CacheItem<SimpleObject>(CacheKey, value, TimeSpan.MaxValue));

            // Assert
            var result = await ddCache.Database.ReadDocumentAsync(ddCache.CreateDocumentURI(CacheKey));

            Assert.NotNull(result);
            Assert.Equal(value.Foo, result.Resource.GetPropertyValue<string>(nameof(SimpleObject.Foo)));
        }

        [Fact, IsIntegration]
        public async Task AddAsync_WithPartitionKeySet_CanReadAndWriteWithPartitionKey()
        {
            // Arrange
            var cacheFactory = new CosmosCacheFactory(
                LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName,
                new CosmosCacheFactorySettings() { InsertMode = CosmosCache.InsertMode.Document, UseKeyAsPartitionKey = true },
                JsonSerializer.CreateDefault());

            var partCache = cacheFactory.Create<SimpleObject>($"partition-{typeof(SimpleObject).Name}");
            var value = SimpleObject.Create();

            await partCache.AddAsync(new CacheItem<SimpleObject>(CacheKey, value, TimeSpan.MaxValue));

            // Assert
            var result = await partCache.GetAsync(CacheKey);

            Assert.NotNull(result);
        }

        private static CosmosCache<string> CreateTTLCache(TimeSpan defaultTTL)
        {
            var cFactory = new CosmosCacheFactory(
                LocalClusterCosmosDb.ConnectionURI, LocalClusterCosmosDb.AccessKey, LocalClusterCosmosDb.DbName,
                new CosmosCacheFactorySettings() { DefaultTimeToLive = (int)defaultTTL.TotalSeconds },
                JsonSerializer.CreateDefault());
            return (CosmosCache<string>)cFactory.Create<string>("ttl-string-collection");
        }

        private CosmosCache<T> CreateCache<T>(CosmosCache.InsertMode mode)
        {
            switch (mode)
            {
                case CosmosCache.InsertMode.Autodetect:
                    return (CosmosCache<T>)autodetectDirectCacheFactory.Create<T>(typeof(T).Name);
                case CosmosCache.InsertMode.Document:
                    return (CosmosCache<T>)docDirectCacheFactory.Create<T>(typeof(T).Name);
                case CosmosCache.InsertMode.JSON:
                    return (CosmosCache<T>)cacheFactory.CreateDefault<T>();
                default:
                    throw new NotImplementedException($"Cache creator for InsertMode='{mode}' is not implemented!");
            }
        }

        private CosmosCache<T> CreateDocumentDirectCache<T>() => 
            (CosmosCache<T>)docDirectCacheFactory.Create<T>($"documentDirect-{typeof(T).Name}");
    }
}
