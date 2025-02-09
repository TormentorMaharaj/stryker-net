For .NET Core projects Stryker.NET can be run without any configuration. On .NET Framework projects the solution path is required.

The full list of Stryker.NET configuration options are:

<!-- TOC -->
- [Solution path (required .NET Framework)](#solution-path)
- [Project file (required on some projects)](#project-file)
- [Test runner](#test-runner)
- [Timeout time](#timeout-time)
- [Reporters](#reporters)
- [Logging to console](#logging-to-console)
- [Excluding mutations](#excluding-mutations)
- [Excluding files (deprecated)](#excluding-files)
- [Mutate](#mutate)
- [Ignore methods](#ignore-methods)
- [Custom tresholds](#unary-operators)
- [Coverage analysis](#coverage-analysis)
- [Abort testrun on test failure](#abort-test-on-fail)
<!-- /TOC -->

## Solution path
On .NET Framework projects Stryker needs your `.sln` file path.

```
dotnet stryker --solution-path "..\\ExampleProject.sln"
dotnet stryker -s "..\\ExampleProject.sln"
```

Stryker.NET needs the path to execute:

```
nuget restore "*.sln"
```
and 
```
MSBuild.exe "*.sln"
```

## Project file
When Stryker finds two or more project references inside your test project, it needs to know which project should be mutated. Pass the name of this project using:

```
dotnet stryker --project-file SomeProjectName.csproj
dotnet stryker -p SomeProjectName.csproj
```

## Test Project file
When Stryker finds two or more project files in the working directory, it needs to which is your test project. Pass the name of this project using:

```
dotnet stryker --test-project-file ExampleTestProject.csproj
dotnet stryker -tp ExampleTestProject.csproj
```

## Specify testrunner
Stryker supports `dotnet test`, the commandline testrunner and `VsTest`, the visual studio testrunner. 
VsTest is the default because it offers tight integration with all test frameworks (MsTest, xUnit, NUnit etc).
Dotnet test can be used if VsTest causes issues for some reason. Please also submit an issue with us if you experience difficulties with VsTest.


```
dotnet stryker --test-runner dotnettest
dotnet stryker -tr dotnettest
```

Default: `"vstest"`

## Timeout time
Some mutations can create endless loops inside your code. To detect and stop these loops, Stryker cancels a unit test run after a set time.
Using this parameter you can increase or decrease the time before a test will time out.

```
dotnet stryker --timeout-ms 1000
dotnet stryker -t 1000
```

Default: `"5000"`

## Reporters
During a mutation testrun one or more reporters can be enabled. A reporter will produce some kind of output during or after the mutation testrun.

```
dotnet stryker --reporters "['html', 'progress']"
dotnet stryker -r "['html', 'progress']"
```

You can find a list of all available reporters and what output they produce in the [reporter docs](/docs/Reporters.md)

Default: `"['cleartext', 'progress']"`

## Logging to console
To gain more insight in what Stryker does during a mutation testrun you can lower your loglevel.
```
dotnet stryker --log-level "debug"
dotnet stryker -l "debug"
```

All available loglevels are:
* error
* warning
* info
* debug
* trace

Default: `"info"`

## Logging to a file
When creating an issue for Stryker.NET on github you can include a logfile. File logging always uses loglevel `trace`.

```
dotnet stryker --log-file
dotnet stryker -f

```

Default: `off`

## Maximum concurrent test runners  
By default Stryker.NET will use as much CPU power as you allow it to use during a mutation testrun. You can lower this setting to lower your CPU usage.

```
dotnet stryker --max-concurrent-test-runners 4
dotnet stryker -c 4
```

This setting can also be used to disable parallel testing. This can be useful if your test project cannot handle parallel testruns.
```
dotnet stryker -c 1
```

Default: `your number of logical processors / 2`*

\* This usually equals your physical processor count

## Custom thresholds
If you want to decide on your own mutation score thresholds, you can configure this with extra parameters.

```
dotnet stryker --threshold-high 90 --threshold-low 75 --threshold-break 50
dotnet stryker -th 90 -tl 75 -tb 50
```

- `mutation score > threshold-high`: 
    - Awesome! Your reporters will color this green and happy.
- `threshold-high > mutation score > threshold-low`:
    - Warning! Your reporters will display yellow/orange colors, watch out!
- `threshold-low < mutation score > threshold-break`:
    - Danger! Your reporters will display red colors, you're in the danger zone now.
- `threshold-break > mutation score`:
    - Error! The application will exit with exit code 1.

Default: `80`, `60`, `0`

## Excluding mutations
If you deem some mutations unwanted for your project you can disable mutations. 

```
dotnet stryker --excluded-mutations "['string', 'logical']"
dotnet stryker -em "['string', 'logical']"
```

The mutations of these kinds will be skipped and not be shown in your reports. This can also speed up your performance on large projects. But don't get too exited, skipping mutations doesn't improve your mutation score ;)

## Excluding files
> ⚠ This parameter is deprecated. Use [Mutate](#mutate) instead.

If you decide to exclude files for unit testing, you can configure this with the following command:

```
dotnet stryker --files-to-exclude "['./ExampleClass.cs', './ExampleDirectory', './ExampleDirectory/ExampleClass2.cs']"
dotnet stryker -fte "['./ExampleClass.cs', './ExampleDirectory', './ExampleDirectory/ExampleClass2.cs']"
```

We recommend to use relative paths. Relative paths are automatically resolved. Absolute paths break easily on different devices. However it is also possible to use absolute paths.

When you want to exclude a large set of files, it is advised to use the stryker configuration file because it is easier to handle multiple files.

Default: `[]`

## Mutate
To specify which files should be mutated you can use file pattern to in- or excluded files or even only parts of a files. By default all files are included.

```
dotnet stryker --mutate "['C:/Repos/MyProject/MyFile.cs']"
dotnet stryker -m "['C:/Repos/MyProject/MyFile.cs']"
```

The patterns support [globbing syntax](https://en.wikipedia.org/wiki/Glob_(programming)) to allow wildcards.

```
dotnet stryker --mutate "['**/*Services.cs']"
dotnet stryker -m "['**/*Services.cs']"
```

You can add an `!` in front of the pattern to exclude all files that match the pattern.

```
dotnet stryker --mutate "['!**/*Factory.cs']"
dotnet stryker -m "['!**/*Factory.cs']"
```
When only exclude patterns are provided, all files will be included that do not match any exclude pattern. If both, include and exclude patterns, are provided, only the files that match an include pattern but not also an exclude pattern will be included. The order of the patterns is irrelevant.

### Example:

| Patterns  | File                      | Will be mutated   |
| ----------| ------------------------- | ----------------- |
| []            | MyFolder/MyFactory.cs    | Yes               |
| []            | MyFolder/MyService.cs | Yes               |
| ['\*\*/\*.\*']   | MyFolder/MyFactory.cs    | Yes               |
| ['\*\*/\*.\*']   | MyFolder/MyService.cs | Yes               |
| ['!\*\*/MyFactory.cs']   | MyFolder/MyFactory.cs    | No        |
| ['!\*\*/MyFactory.cs']   | MyFolder/MyService.cs | Yes       |
| ['!\*\*/MyFactory.cs', '\*\*/My\*.cs']   | MyFolder/MyFactory.cs    | No        |
| ['!\*\*/MyFactory.cs', '\*\*/My\*.cs']   | MyFolder/MyService.cs | Yes       |

To allow more fine grained filtering you can also specify the span of text that should be in- or excluded. A span is defined by the indices of the first character and the last character.

```
dotnet stryker --mutate "['MyFolder/MyService.cs{10..100}']"
dotnet stryker -m "['MyFolder/MyService.cs{10..100}']"
```

## Ignore methods
If you would like to ignore some mutations that are passed as method parameters, you can do so by specifying which methods to ignore:

```
dotnet stryker --ignore-methods "['ToString', 'ConfigureAwait', '*Exception.ctor']"
dotnet stryker -im "['ToString', 'ConfigureAwait', '*Exception.ctor']"
```

Ignore methods will only affect mutations in directly passed parameters.

``` csharp
// This mutation will be skipped;
ConfigureAwait(true); 

// This mutation won't
var t = true;
ConfigureAwait(t); 
```

You can also ignore constructors by specifying the type and adding the `.ctor` suffix.

`dotnet stryker -im "['NotImplementedException.ctor']"`

Both, method names and constructor names, support wildcards.

`dotnet stryker -im "['*Log']" // Ignores all methods ending with log`
`dotnet stryker -im "['*Exception.ctor']" // Ignores all exception constructors`

Default: `[]`

## Use a config file
There is also the option to use a config file. To use a config file all you have to do is add a file called `stryker-config.json` in the root of your test project and add a configuration section called stryker-config. Then you can add the options you want to configure to the file.

Example config file:
``` javascript
{
    "stryker-config":
    {
        "test-runner": "vstest",
        "reporters": [
            "progress",
            "html"
        ],
        "log-level": "info",
        "log-file":true,
        "timeout-ms": 10000,
        "project-file": "ExampleProject.csproj",
        "max-concurrent-test-runners": 4,
        "threshold-high": 80,
        "threshold-low": 70,
        "threshold-break": 60,
        "file-patterns": [
            "!ExampleClass.cs",
            "!ExampleDirectory",
            "!ExampleDirectory/ExampleClass2.cs",
            "!C:\\ExampleRepo\\ExampleDirectory\\ExampleClass.cs"
        ],
        "excluded-mutations": [
            "string",
            "Logical operators"
        ]
    }
}
```

## Config file location
If you want to integrate these settings in your existing settings json, make sure the section is called stryker-config and run stryker with the command
```
dotnet stryker --config-file-path <relativePathToFile>
dotnet stryker -cp <relativePathToFile>
```

Default: `"./stryker-config.json"`

## Coverage analysis
Use coverage info to speed up execution. Possible values are: off, perTest, all, perIsolatedTest.

- **off**: coverage data is not captured.
- **perTest**: capture the list of mutants covered by each test. For every mutant that has tests, only the tests that cover a the mutant are tested. Fastest option.
- **all**: capture the list of mutants covered by each test. Test only these mutants. Non covered mutants are assumed as survivors. Fast option.
- **perTestInIsolation**: like 'perTest', but running each test in an isolated run. This results in more accurate
coverage information for some mutants (see below), at the expanse of a longer startup time.

```
dotnet stryker --coverage-analysis perTest
dotnet stryker -ca perTest
```

Default: `"perTest"`
### Notes on coverage analysis
* The 'dotnet test' runner only supports `all` mode. This is due to dotnet test limitation
* Results are not impacted by coverage analysis. If you identify a suspicious survivor, run
Stryker again without coverage analysis and report an issue if this mutant is killed by this run.
* when using `perTest` mode, mutants that are executed as part as some static constructor/initializer 
are run against all tests, as Stryker can not reliably capture coverage for those. This is a consequence of static
constructors/initialisers being called only once during tests. This heuristic is not needed when using
`perTestInIsolation` due to test being run one by one.


## Abort test on fail
Abort unit testrun as soon as any one unit test fails. This can reduce the overall running time.

```
dotnet stryker --abort-test-on-fail
dotnet stryker -atof
```

Default: `"on"`
