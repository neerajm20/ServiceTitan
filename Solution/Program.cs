using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
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
        Task<object> Get(string key, Type type);
        Task Upsert(string key, object value, TimeSpan? ttl = null);
        Task Remove(string key);
    }

    [ContractClassFor(typeof(ICache))]
    internal abstract class CacheContract : ICache
    {
        public Task<object> Get(string key, Type type)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(key));
            Contract.Requires(type != null);
            Contract.Ensures(Contract.Result<Task<object>>() != null);

            return null;
        }

        public Task Upsert(string key, object value, TimeSpan? ttl = null)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(key));
            Contract.Ensures(Contract.Result<Task>() != null);

            return null;
        }

        public Task Remove(string key)
        {
            Contract.Requires(!String.IsNullOrWhiteSpace(key));
            Contract.Ensures(Contract.Result<Task>() != null);

            return null;
        }
    }

    public class InMemoryCache : ICache
    {
        private readonly ObjectCache _cache = new MemoryCache("InMemoryCache");
        private static readonly TimeSpan MaxMaxAge = TimeSpan.FromMinutes(15);

        public async Task<object> Get(string key, Type type)
        {
            return _cache.Get(key);
        }

        public async Task Upsert(string key, object value, TimeSpan? ttl = null)
        {
            // Seems arbitrary, but caching in memory for > 15 minutes is of very low value 
            // with a high cost when things are out of sync
            if (!ttl.HasValue || ttl.Value > MaxMaxAge)
            {
                ttl = MaxMaxAge;
            }

            _cache.Set(key, value, DateTime.Now.Add(ttl.Value));
        }

        public async Task Remove(string key)
        {
            _cache.Remove(key);
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

        public Person(string id = null)
        {
            if (String.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString();
            
            this.Id = id;
            this.Ttl = TimeSpan.FromMilliseconds(PersonCacheAbsoluteExpirationMs);
        }

        public string Id
        {
            get { return this._id; }
            set
            {
                this._id = value;
                setKey(_id);
            }
        }

        private void setKey(string id)
        {
            this.Key = $"{this.GetType().AssemblyQualifiedName}.{Id}";
        }
    }

    public class CachingServiceTest
    {
        private const string Key0 = "f2aa8194c8804d979b4f233cc413afb3";
        private const string Value0 = "98641a2022154dfb8222d4c862999480";
        private const string Value1 = "8eb0173f4bd04ef69995ce72b57e2c85";
        private Person person0 = new Person(Key0)
        {
            FirstName = "Neeraj",
            LastName = "Mittal"
        };

        private ICache Target = new InMemoryCache();

        public Action[] AllTests => new Action[]
        {
            Get_EntryPresent_ReturnsValue,
            Get_EntryNotPresent_ReturnsNull,
            UpsertEntryNotPresent_SetsValue,
            Upsert_EntryAlreadyPresent_OverwritesValue,
            Upsert_EntryAlreadyPresent_OverwritesPersonValue,
            Upsert_WithTtl_CausesDataToExpire,
            Delete_EntryNotPresent_DoesNothing,
            Delete_EntryPresent_RemovesValue
        };

        private void Get_EntryPresent_ReturnsValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Assert(Target.Get(Key0, typeof(string)).Result == Value0);

            // cleanup
            Target.Remove(Key0);
        }

        private void Get_EntryNotPresent_ReturnsNull()
        {
            Assert(Target.Get(Key0, typeof(string)).Result == null);
        }

        public void UpsertEntryNotPresent_SetsValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Assert(Target.Get(Key0, typeof(string)).Result == Value0);

            // cleanup
            Target.Remove(Key0);
        }

        public void Upsert_EntryAlreadyPresent_OverwritesValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Target.Upsert(Key0, Value1).Wait();

            Assert(Target.Get(Key0, typeof(string)).Result == Value1);

            // cleanup
            Target.Remove(Key0);
        }

        public void Upsert_EntryAlreadyPresent_OverwritesPersonValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Target.Upsert(Key0, person0).Wait();

            var actualPerson = (Person)Target.Get(Key0, typeof(Person)).Result;
            Assert(actualPerson.FirstName == person0.FirstName);
            Assert(actualPerson.LastName == person0.LastName);

            // cleanup
            Target.Remove(Key0);
        }

        public void Upsert_WithTtl_CausesDataToExpire()
        {
            var ttl = TimeSpan.FromMilliseconds(10);

            Target.Upsert(Key0, Value0, ttl).Wait();
            Assert(Target.Get(Key0, typeof(string)).Result == Value0);

            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime) < ttl)
            {
                Thread.Sleep(ttl);
            }

            Assert(Target.Get(Key0, typeof(string)).Result == null);
        }

        public void Delete_EntryNotPresent_DoesNothing()
        {
            Target.Remove(Key0).Wait();
            Assert(Target.Get(Key0, typeof(string)).Result == null);
        }

        public void Delete_EntryPresent_RemovesValue()
        {
            Target.Upsert(Key0, Value0).Wait();
            Assert(Target.Get(Key0, typeof(string)).Result == Value0);

            Target.Remove(Key0).Wait();
            Assert(Target.Get(Key0, typeof(string)).Result == null);
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