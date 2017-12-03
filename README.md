# Caching Services

This project contains the interview answer for ServiceTitan's long hackerrank coding test. I chose the problem Caching Services.

## Question: Why did you not implement in HackerRank?
C# .Net Framework 4 contains a preimplemented Cache under System.Runtime.Caching. Hackerrank did not provide this reference.

## Installing and Running

```
1. Download master branch
2. Open ServiceTitan.sln in Visual Studio or any .Net IDE
3. Run (all output is written to stdout)
```

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

###List of Classes
```

```



## Authors

* Neeraj Mittal

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details
