# Caching Services

This project contains the interview answer for ServiceTitan's long hackerrank coding test. I chose the problem Caching Services.

## Question: Why did you not implement in HackerRank?
C# .Net Framework 4 contains an implementation of an in-memory cache under System.Runtime.Caching. Hackerrank did not provide this reference.

*Note: The only change that deviated finishing the entire exercise in HackerRank is adding reference System.Runtime.Caching*

## Installing and Running

```
1. Download master branch
2. Open ServiceTitan.sln in Visual Studio or any .Net IDE
3. Run (all output is written to stdout)
```

### Prerequisites
This solution is tested on Visual Studio 2017 Community Edition and Jetbrains Rider 2017

## Sample Output

```
I've solved this problem.

Running Cache implementation tests...
  Running Get_EntryPresent_ReturnsValue...Success
  Running Get_EntryNotPresent_ReturnsNull...Success
  Running Upsert_EntryNotPresent_SetsValue...Success
  Running Upsert_EntryAlreadyPresent_OverwritesValue...Success
  Running Upsert_EntryAlreadyPresent_OverwritesPersonValue...Success
  Running Upsert_WithTtl_CausesDataToExpire...Success
  Running GetOrInsert_GetEntryPresent_ReturnsValue...Success
  Running GetOrInsert_GetEntryNotPresent_ReturnsValue...Success
  Running Delete_EntryNotPresent_DoesNothing...Success
  Running Delete_EntryPresent_RemovesValue...Success
Done.

Running Service and repository tests...
  Running GetPersonById_EntryNotInCache_ReturnsValue...Success
Done.
```

## Explanation of Code

### Design
High-level, I am providing an ICache interface and a thread-safe in-memory cache implementation of it. This can be used by various code modules for local inmemory caching.

Per the exercise requirements, a sample service repository (PersonRepository) is provided, to show how it will be used.

Various test cases are provided.

### Classes
```
1. Solution

2. ICacheable, CacheableContract

3. CacheKey

4. ICache, CacheContract, InMemoryCache

5. Person : ICacheable

6. IPersonRepository, PersonRepositoryContract, PersonRepository

7. CachingServiceTest

8. PersonRepositoryTest

```

#### Solution
Main class that runs the program.

#### ICacheable, CacheableContract
Define the interface and validation contract for any Service that wants to be Cacheable.

#### CacheKey
Every key is unique in the ICache implementation. This helper class makes the key unique within the type of object you are storing in the cache.

#### ICache, CacheContract, InMemoryCache
Interface, validation and an in-memory implementation of the actual Cache.

#### Person : ICacheable
An example cacheable entity.

#### IPersonRepository, PersonRepositoryContract, PersonRepository
A sample repository implementation for CRUD on a Person entity. It uses ICache internally.

#### CachingServiceTest
Test cases for InMemoryCache implementation. This can be extended to support other cache types in the future.

#### PersonRepositoryTest
Test cases for the PersonRepository implementation, including making sure ICache is being used.

## Authors

* Neeraj Mittal

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details
