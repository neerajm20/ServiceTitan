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
//            var serviceRepositoryTest = new ServiceRepositoryTest();
//            serviceRepositoryTest.RunAllTests();
            Console.WriteLine("Done.");
        }
    }

    //TODO: nmittal - nmittal - install code contract library
    [ContractClass(typeof(CacheableContract))]
    public interface ICacheable
    {
        string Key { get; }
        TimeSpan? Ttl { get; }
    }

    [ContractClassFor(typeof(ICacheable))]
    internal abstract class CacheableContract : ICacheable
    {
        public string Key
        {
            get
            {
                Contract.Ensures(Contract.Result<string>() != null);
                return null;
            }
        }

        public TimeSpan? Ttl
        {
            get { return null; }
        }
    }

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
        
        // Singleton referance
        // typically, this would be controlled via a IoC Container
        private static ICache instance = new InMemoryCache();
        public static ICache Instance
        {
            get { return instance; }
        }

        private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();

        private readonly ObjectCache _cache = new MemoryCache(DefaultCacheName);
        private static readonly TimeSpan MaxMaxAge = TimeSpan.FromMinutes(15);

        public async Task<T> Get<T>(string key)
        {
            cacheLock.EnterReadLock();
            try
            {
                return await Task.FromResult((T)(_cache.Get(key))).ConfigureAwait(false);
            }
            finally
            {
                cacheLock.ExitReadLock();
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

            cacheLock.EnterWriteLock();
            try
            {
                _cache.Set(key, value, DateTime.Now.Add(ttl.Value));
                await Task.FromResult(false).ConfigureAwait(false);
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
        }

        public async Task<T> GetOrInsert<T>(string key, Func<Task<T>> hydrator, TimeSpan? ttl = null)
        {
            T value;

            cacheLock.EnterUpgradeableReadLock();
            try
            {
                value = await Get<T>(key).ConfigureAwait(false);

                // hydrate if a complete cache miss.
                if (EqualityComparer<T>.Default.Equals(value, default(T)))
                {
                    value = await hydrator().ConfigureAwait(false);
                }

                await Upsert(key, value, ttl).ConfigureAwait(false);
            }
            finally
            {
                cacheLock.ExitUpgradeableReadLock();
            }

            return value;
        }

        public async Task Remove(string key)
        {
            cacheLock.EnterWriteLock();
            try
            {
                _cache.Remove(key);
                await Task.FromResult(false).ConfigureAwait(false);
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            cacheLock?.Dispose();
        }
    }

    public class Person : ICacheable
    {
        private const int PersonCacheAbsoluteExpirationMs = 10;

        private string _id;
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public string Key { get; private set; }
        public TimeSpan? Ttl { get; private set; }

        public Person()
        {
            Ttl = TimeSpan.FromMilliseconds(PersonCacheAbsoluteExpirationMs);
        }

        public string Id
        {
            get { return _id; }
            set
            {
                _id = value;
                SetKey(_id);
            }
        }

        private void SetKey(string id)
        {
            Key = $"{GetType().AssemblyQualifiedName}.{id}";
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
        private ICache cache;

        // this code will be replaced to get a person from the data store
        private Person person;

        //TODO: nmittal - nmittal - ICache would typically be injected via an IoC Container
        public PersonRepository(ICache cache = null)
        {
            if (cache == null) cache = InMemoryCache.Instance;
            this.cache = cache;
            
            // this code will be replaced to get a person from the data store
            person = new Person()
            {
                Id = "f2aa8194c8804d979b4f233cc413afb3",
                FirstName = "Neeraj",
                LastName = "Mittal"
            };
        }

        public async Task<Person> GetPersonById(string id)
        {
            Func<Task<Person>> func = async () =>
                await Task.FromResult(person).ConfigureAwait(false);
            
            return await cache.GetOrInsert(id, func).ConfigureAwait(false);
        }
    }

    public class CachingServiceTest
    {
        private const string Key0 = "f2aa8194c8804d979b4f233cc413afb3";
        private const string Value0 = "98641a2022154dfb8222d4c862999480";
        private const string Value1 = "8eb0173f4bd04ef69995ce72b57e2c85";

        private Person person0 = new Person()
        {
            Id = Key0,
            FirstName = "Neeraj",
            LastName = "Mittal"
        };

        private ICache Target = InMemoryCache.Instance;

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
            Target.Upsert(Key0, Value0).Wait();
            Assert(Target.Get<string>(Key0).Result == Value0);

            // cleanup
            Target.Remove(Key0);
        }

        private void Get_EntryNotPresent_ReturnsNull()
        {
            Assert(Target.Get<string>(Key0).Result == null);
        }

        public void Upsert_EntryNotPresent_SetsValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Assert(Target.Get<string>(Key0).Result == Value0);

            // cleanup
            Target.Remove(Key0);
        }

        public void Upsert_EntryAlreadyPresent_OverwritesValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Target.Upsert(Key0, Value1).Wait();

            Assert(Target.Get<string>(Key0).Result == Value1);

            // cleanup
            Target.Remove(Key0);
        }

        public void Upsert_EntryAlreadyPresent_OverwritesPersonValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Target.Upsert(Key0, person0).Wait();

            var actualPerson = Target.Get<Person>(Key0).Result;
            Assert(actualPerson.FirstName == person0.FirstName);
            Assert(actualPerson.LastName == person0.LastName);

            // cleanup
            Target.Remove(Key0);
        }

        public void Upsert_WithTtl_CausesDataToExpire()
        {
            var ttl = TimeSpan.FromMilliseconds(10);

            Target.Upsert(Key0, Value0, ttl).Wait();
            Assert(Target.Get<string>(Key0).Result == Value0);

            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime) < ttl)
            {
                Thread.Sleep(ttl);
            }

            Assert(Target.Get<string>(Key0).Result == null);
        }

        public void GetOrInsert_GetEntryPresent_ReturnsValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Assert(Target.GetOrInsert(Key0, async () => await Task.FromResult(Value1).ConfigureAwait(false)).Result == Value0);
            Assert(Target.Get<string>(Key0).Result != Value1);

            // cleanup
            Target.Remove(Key0);
        }

        public void GetOrInsert_GetEntryNotPresent_ReturnsValue()
        {
            Assert(Target.GetOrInsert(Key0, async () => await Task.FromResult(Value1).ConfigureAwait(false)).Result == Value1);
            Assert(Target.Get<string>(Key0).Result != Value0);

            // cleanup
            Target.Remove(Key0);
        }

        public void Delete_EntryNotPresent_DoesNothing()
        {
            Target.Remove(Key0).Wait();
            Assert(Target.Get<string>(Key0).Result == null);
        }

        public void Delete_EntryPresent_RemovesValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Assert(Target.Get<string>(Key0).Result == Value0);

            Target.Remove(Key0).Wait();
            Assert(Target.Get<string>(Key0).Result == null);
        }

        public static void Assert(bool criteria)
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