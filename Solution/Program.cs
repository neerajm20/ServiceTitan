using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace Solution
{
    internal class Solution
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("I've solved this problem.");

            // test cache implementation
            Console.WriteLine("Running Cache implementation tests...");
            var cachingServiceTest = new CachingServiceTest();
            cachingServiceTest.RunAllTests();
            Console.WriteLine("Done.");

            Console.WriteLine("Running Service and repository tests...");
            // test service and repository implementation
            var personRepositoryTest = new PersonRepositoryTest();
            personRepositoryTest.RunAllTests();
            Console.WriteLine("Done.");
        }
    }

    public interface ICachePolicy
    {
        TimeSpan? Ttl { get; }
    }

    public class CacheKey<T>
    {
        public string Id { get; set; }

        public override string ToString()
        {
            return $"{typeof(T).FullName}.{Id}";
        }
    }

    //TODO: nmittal - nmittal - install code contract library
    //TODO: nmittal - nmittal - can be extended to support CacheKey and CachePolicy
    [ContractClass(typeof(CacheContract))]
    public interface ICache
    {
        Task<T> Get<T>(string key);
        Task Upsert<T>(string key, T value, TimeSpan? ttl = null);
        Task<T> GetOrInsert<T>(string key, Func<Task<T>> hydrator, TimeSpan? ttl = null);
        Task Remove(string key);
    }

    [ContractClassFor(typeof(ICache))]
    internal abstract class CacheContract : ICache
    {
        public Task<T> Get<T>(string key)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(key));
            Contract.Ensures(Contract.Result<Task<object>>() != null);

            return null;
        }

        public Task Upsert<T>(string key, T value, TimeSpan? ttl = null)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(key));
            Contract.Ensures(Contract.Result<Task>() != null);

            return null;
        }

        public Task<T> GetOrInsert<T>(string key, Func<Task<T>> hydrator, TimeSpan? ttl = null)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(key));
            Contract.Requires(hydrator != null);
            Contract.Ensures(Contract.Result<Task<object>>() != null);

            return null;
        }

        public Task Remove(string key)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(key));
            Contract.Ensures(Contract.Result<Task>() != null);

            return null;
        }
    }

    public class InMemoryCache : ICache, IDisposable
    {
        private const string DefaultCacheName = "InMemoryCache";
        private static readonly TimeSpan MaxMaxAge = TimeSpan.FromMinutes(15);

        // Singleton referance
        // typically, this would be controlled via a IoC Container
        public static readonly ICache Instance = new InMemoryCache();

        private readonly ObjectCache _cache = new MemoryCache(DefaultCacheName);
        private readonly ReaderWriterLockSlim _cacheLock = new ReaderWriterLockSlim();

        public async Task<T> Get<T>(string key)
        {
            _cacheLock.EnterReadLock();
            try
            {
                return await Task.FromResult((T) (_cache.Get(key))).ConfigureAwait(false);
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        }

        public async Task Upsert<T>(string key, T value, TimeSpan? ttl = null)
        {
            // Seems arbitrary, but caching in memory for > 15 minutes is of very low value 
            // with a high cost when things are out of sync
            if (!ttl.HasValue || ttl.Value > MaxMaxAge)
            {
                ttl = MaxMaxAge;
            }

            _cacheLock.EnterWriteLock();
            try
            {
                _cache.Set(key, value, DateTime.Now.Add(ttl.Value));
                await Task.FromResult(false).ConfigureAwait(false);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        public async Task<T> GetOrInsert<T>(string key, Func<Task<T>> hydrator, TimeSpan? ttl = null)
        {
            T value;

            _cacheLock.EnterUpgradeableReadLock();
            try
            {
                value = await Get<T>(key).ConfigureAwait(false);

                // hydrate if a complete cache miss.
                if (EqualityComparer<T>.Default.Equals(value, default(T)))
                {
                    value = await hydrator().ConfigureAwait(false);
                    if (typeof(ICachePolicy).IsAssignableFrom(typeof(T)))
                    {
                        ttl = ((ICachePolicy) value).Ttl;
                    }
                }

                await Upsert(key, value, ttl).ConfigureAwait(false);
            }
            finally
            {
                _cacheLock.ExitUpgradeableReadLock();
            }

            return value;
        }

        public async Task Remove(string key)
        {
            _cacheLock.EnterWriteLock();
            try
            {
                _cache.Remove(key);
                await Task.FromResult(false).ConfigureAwait(false);
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            _cacheLock?.Dispose();
        }
    }

    //TODO: nmittal - nmittal - dont need to inherit ICachePolicy, should be configuration based
    public class Person : ICachePolicy
    {
        private const int PersonCacheAbsoluteExpirationMs = 1000;

        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public TimeSpan? Ttl { get; }

        public Person()
        {
            Ttl = TimeSpan.FromMilliseconds(PersonCacheAbsoluteExpirationMs);
        }
    }

    [ContractClass(typeof(PersonRepositoryContract))]
    public interface IPersonRepository
    {
        Task<Person> GetPersonById(string id);
    }

    [ContractClassFor(typeof(IPersonRepository))]
    internal class PersonRepositoryContract : IPersonRepository
    {
        public Task<Person> GetPersonById(string id)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(id));
            return null;
        }
    }

    public class PersonRepository : IPersonRepository
    {
        private readonly ICache _cache;
        private readonly Person _person0;

        //TODO: nmittal - nmittal - ICache would typically be injected via an IoC Container
        public PersonRepository(ICache cache = null)
        {
            if (cache == null) cache = InMemoryCache.Instance;
            _cache = cache;

            // this code will be replaced to get a person from the data store
            _person0 = new Person()
            {
                FirstName = "Neeraj",
                LastName = "Mittal"
            };
        }

        public async Task<Person> GetPersonById(string id)
        {
            _person0.Id = id;
            async Task<Person> Func() => await Task.FromResult(_person0).ConfigureAwait(false);

            var cacheKey = new CacheKey<Person>()
            {
                Id = _person0.Id
            };
            return await _cache.GetOrInsert(cacheKey.ToString(), Func).ConfigureAwait(false);
        }
    }

    public class CachingServiceTest
    {
        private const string Key0 = "f2aa8194c8804d979b4f233cc413afb3";
        private const string Value0 = "98641a2022154dfb8222d4c862999480";
        private const string Value1 = "8eb0173f4bd04ef69995ce72b57e2c85";

        private readonly Person _person0 = new Person()
        {
            Id = Key0,
            FirstName = "Neeraj",
            LastName = "Mittal"
        };

        private readonly ICache _target = InMemoryCache.Instance;

        public Action[] AllTests => new Action[]
        {
            Get_EntryPresent_ReturnsValue,
            Get_EntryNotPresent_ReturnsNull,
            Upsert_EntryNotPresent_SetsValue,
            Upsert_EntryAlreadyPresent_OverwritesValue,
            Upsert_EntryAlreadyPresent_OverwritesPersonValue,
            Upsert_WithTtl_CausesDataToExpire,
            GetOrInsert_GetEntryPresent_ReturnsValue,
            GetOrInsert_GetEntryNotPresent_ReturnsValue,
            Delete_EntryNotPresent_DoesNothing,
            Delete_EntryPresent_RemovesValue
        };

        private void Get_EntryPresent_ReturnsValue()
        {
            _target.Upsert(Key0, Value0).Wait();
            Assert(_target.Get<string>(Key0).Result == Value0);

            // cleanup
            _target.Remove(Key0);
        }

        private void Get_EntryNotPresent_ReturnsNull()
        {
            Assert(_target.Get<string>(Key0).Result == null);
        }

        public void Upsert_EntryNotPresent_SetsValue()
        {
            _target.Upsert(Key0, Value0).Wait();
            Assert(_target.Get<string>(Key0).Result == Value0);

            // cleanup
            _target.Remove(Key0);
        }

        public void Upsert_EntryAlreadyPresent_OverwritesValue()
        {
            _target.Upsert(Key0, Value0).Wait();
            _target.Upsert(Key0, Value1).Wait();

            Assert(_target.Get<string>(Key0).Result == Value1);

            // cleanup
            _target.Remove(Key0);
        }

        public void Upsert_EntryAlreadyPresent_OverwritesPersonValue()
        {
            _target.Upsert(Key0, Value0).Wait();
            _target.Upsert(Key0, _person0).Wait();

            var actualPerson = _target.Get<Person>(Key0).Result;
            Assert(actualPerson.FirstName == _person0.FirstName);
            Assert(actualPerson.LastName == _person0.LastName);

            // cleanup
            _target.Remove(Key0);
        }

        public void Upsert_WithTtl_CausesDataToExpire()
        {
            var ttl = TimeSpan.FromMilliseconds(10);

            _target.Upsert(Key0, Value0, ttl).Wait();
            Assert(_target.Get<string>(Key0).Result == Value0);

            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime) < ttl)
            {
                Thread.Sleep(ttl);
            }

            Assert(_target.Get<string>(Key0).Result == null);
        }

        public void GetOrInsert_GetEntryPresent_ReturnsValue()
        {
            _target.Upsert(Key0, Value0).Wait();
            Assert(_target.GetOrInsert(Key0, async () => await Task.FromResult(Value1).ConfigureAwait(false)).Result ==
                   Value0);
            Assert(_target.Get<string>(Key0).Result != Value1);

            // cleanup
            _target.Remove(Key0);
        }

        public void GetOrInsert_GetEntryNotPresent_ReturnsValue()
        {
            Assert(_target.GetOrInsert(Key0, async () => await Task.FromResult(Value1).ConfigureAwait(false)).Result ==
                   Value1);
            Assert(_target.Get<string>(Key0).Result != Value0);

            // cleanup
            _target.Remove(Key0);
        }

        public void Delete_EntryNotPresent_DoesNothing()
        {
            _target.Remove(Key0).Wait();
            Assert(_target.Get<string>(Key0).Result == null);
        }

        public void Delete_EntryPresent_RemovesValue()
        {
            _target.Upsert(Key0, Value0).Wait();
            Assert(_target.Get<string>(Key0).Result == Value0);

            _target.Remove(Key0).Wait();
            Assert(_target.Get<string>(Key0).Result == null);
        }

        private void Assert(bool criteria)
        {
            if (!criteria)
                throw new InvalidOperationException("Assertion failed.");
        }

        public void RunAllTests()
        {
            foreach (var test in AllTests)
            {
                Console.Write($"Running {test.GetMethodInfo().Name}...");
                try
                {
                    test.Invoke();
                    Console.WriteLine("Success");
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed");
                }
            }
        }
    }

    public class PersonRepositoryTest
    {
        private const string Key0 = "f2aa8194c8804d979b4f233cc413afb3";
        private readonly CacheKey<Person> _cacheKey = new CacheKey<Person>()
        {
            Id = Key0
        };

        private readonly ICache _target = InMemoryCache.Instance;
        private readonly IPersonRepository _personRepository = new PersonRepository();

        public Action[] AllTests => new Action[]
        {
            GetPersonById_EntryNotInCache_ReturnsValue
        };

        public void GetPersonById_EntryNotInCache_ReturnsValue()
        {
            var key = _cacheKey.ToString();
            Assert(_target.Get<Person>(key).Result == null);
            Assert(_personRepository.GetPersonById(Key0).Result.Id == Key0);
            Assert(_target.Get<Person>(key).Result.Id == Key0);

            // cleanup
            _target.Remove(key);
        }

        private void Assert(bool criteria)
        {
            if (!criteria)
                throw new InvalidOperationException("Assertion failed.");
        }

        public void RunAllTests()
        {
            foreach (var test in AllTests)
            {
                Console.Write($"Running {test.GetMethodInfo().Name}...");
                try
                {
                    test.Invoke();
                    Console.WriteLine("Success");
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed");
                }
            }
        }
    }
}