﻿using Moq;
using Shouldly;
using Stryker.Core.Exceptions;
using Stryker.Core.Initialisation;
using Stryker.Core.Options;
using Stryker.Core.ProjectComponents;
using Stryker.Core.TestRunners;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Stryker.Core.UnitTest.Initialisation
{
    public class InputFileResolverTests
    {
        private string _currentDirectory { get; set; }
        private string _filesystemRoot { get; set; }
        private string _basePath { get; set; }
        private readonly string _sourceFile;
        private readonly string _testProjectPath;
        private readonly string _projectUnderTestPath;
        private readonly string _defaultTestProjectFileContents;

        public InputFileResolverTests()
        {
            _currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _filesystemRoot = Path.GetPathRoot(_currentDirectory);
            _basePath = Path.Combine(_filesystemRoot, "TestProject");

            _sourceFile = File.ReadAllText(_currentDirectory + "/TestResources/ExampleSourceFile.cs");
            _testProjectPath = FilePathUtils.ConvertPathSeparators(Path.Combine(_filesystemRoot, "TestProject", "TestProject.csproj"));
            _projectUnderTestPath = FilePathUtils.ConvertPathSeparators(Path.Combine(_filesystemRoot, "ExampleProject", "ExampleProject.csproj"));
            _defaultTestProjectFileContents = @"<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>netcoreapp2.0</TargetFramework>
        <IsPackable>false</IsPackable>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version = ""15.5.0"" />
        <PackageReference Include=""xunit"" Version=""2.3.1"" />
        <PackageReference Include=""xunit.runner.visualstudio"" Version=""2.3.1"" />
        <DotNetCliToolReference Include=""dotnet-xunit"" Version=""2.3.1"" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include=""..\ExampleProject\ExampleProject.csproj"" />
    </ItemGroup>
</Project>";
        }

        [Fact]
        public void InputFileResolver_InitializeShouldFindFilesRecursively()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { _projectUnderTestPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(_sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "OneFolderDeeper", "Recursive.cs"), new MockFileData(_sourceFile)},
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", "bin", "Debug", "netcoreapp2.0"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "obj", "Release", "netcoreapp2.0"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_testProjectPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    References = new string[] { "" }
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var result = target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath));

            result.ProjectContents.GetAllFiles().Count().ShouldBe(2);
        }

        [Fact]
        public void InputFileResolver_InitializeShouldResolveImportedProject()
        {
            string sourceFile = File.ReadAllText(_currentDirectory + "/TestResources/ExampleSourceFile.cs");

            string sharedItems = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                <PropertyGroup>
                <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
                <HasSharedItems>true</HasSharedItems>
                <SharedGUID>0425a660-ca7d-43f6-93ab-f72c95d506e3</SharedGUID>
                </PropertyGroup>
                <ItemGroup>
                <Compile Include=""$(MSBuildThisFileDirectory)shared.cs"" />
                </ItemGroup>
                </Project>";
            string projectFile = @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>netcoreapp2.0</TargetFramework>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
    </ItemGroup>
               
     <Import Project=""../SharedProject/Example.projitems"" Label=""Shared"" />

</Project>";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "SharedProject", "Example.projitems"), new MockFileData(sharedItems)},
                    { Path.Combine(_filesystemRoot, "SharedProject", "Shared.cs"), new MockFileData(sourceFile)},
                    { _projectUnderTestPath, new MockFileData(projectFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "OneFolderDeeper", "Recursive.cs"), new MockFileData(sourceFile)},
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", "bin", "Debug", "netcoreapp2.0"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "obj", "Release", "netcoreapp2.0"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>() { Path.Combine(_filesystemRoot, "SharedProject", "Example.projitems") });
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_testProjectPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    Properties = new Dictionary<string, string>(),
                    References = new string[] { "" }
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath,
                    Properties = new Dictionary<string, string>()
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var result = target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath));

            result.ProjectContents.GetAllFiles().Count().ShouldBe(3);
        }

        [Fact]
        public void InputFileResolver_InitializeShouldNotResolveImportedPropsFile()
        {
            string sourceFile = File.ReadAllText(_currentDirectory + "/TestResources/ExampleSourceFile.cs");

            string projectFile = @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>netcoreapp2.0</TargetFramework>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
    </ItemGroup>
               
     <Import Project=""../NonSharedProject/Example.props"" Label=""Shared"" />

</Project>";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "NonSharedProject", "Example.props"), new MockFileData("")},
                    { Path.Combine(_filesystemRoot, "NonSharedProject", "NonSharedSource.cs"), new MockFileData(sourceFile)},
                    { _projectUnderTestPath, new MockFileData(projectFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "OneFolderDeeper", "Recursive.cs"), new MockFileData(sourceFile)},
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", "bin", "Debug", "netcoreapp2.0"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "obj", "Release", "netcoreapp2.0"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_testProjectPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    Properties = new Dictionary<string, string>(),
                    References = new string[] { "" }
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath,
                    Properties = new Dictionary<string, string>()
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var result = target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath));

            result.ProjectContents.GetAllFiles().Count().ShouldBe(2);
        }

        [Fact]
        public void InputFileResolver_InitializeShouldResolveMultipleImportedProjects()
        {
            string sourceFile = File.ReadAllText(_currentDirectory + "/TestResources/ExampleSourceFile.cs");

            string sharedItems = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                <PropertyGroup>
                <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
                <HasSharedItems>true</HasSharedItems>
                <SharedGUID>0425a660-ca7d-43f6-93ab-f72c95d506e3</SharedGUID>
                </PropertyGroup>
                <ItemGroup>
                <Compile Include=""$(MSBuildThisFileDirectory)shared.cs"" />
                </ItemGroup>
                </Project>";
            string sharedItems2 = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
                <PropertyGroup>
                <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
                <HasSharedItems>true</HasSharedItems>
                <SharedGUID>0425a660-ca7d-43f6-93ab-f72c95d506e3</SharedGUID>
                </PropertyGroup>
                <ItemGroup>
                <Compile Include=""$(MSBuildThisFileDirectory)shared.cs"" />
                </ItemGroup>
                </Project>";
            string projectFile = @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>netcoreapp2.0</TargetFramework>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
    </ItemGroup>
               
     <Import Project=""../SharedProject1/Example.projitems"" Label=""Shared"" />
     <Import Project=""../SharedProject2/Example.projitems"" Label=""Shared"" />

</Project>";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "SharedProject1", "Example.projitems"), new MockFileData(sharedItems)},
                    { Path.Combine(_filesystemRoot, "SharedProject1", "Shared.cs"), new MockFileData(sourceFile)},
                    { Path.Combine(_filesystemRoot, "SharedProject2", "Example.projitems"), new MockFileData(sharedItems2)},
                    { Path.Combine(_filesystemRoot, "SharedProject2", "Shared.cs"), new MockFileData(sourceFile)},
                    { _projectUnderTestPath, new MockFileData(projectFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "OneFolderDeeper", "Recursive.cs"), new MockFileData(sourceFile)},
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", "bin", "Debug", "netcoreapp2.0"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "obj", "Release", "netcoreapp2.0"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>()
            {
                Path.Combine(_filesystemRoot, "SharedProject1", "Example.projitems"),
                Path.Combine(_filesystemRoot, "SharedProject2", "Example.projitems")
            });
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    Properties = new Dictionary<string, string>(),
                    References = new string[] { "" }
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath,
                    Properties = new Dictionary<string, string>(),
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var result = target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath));

            result.ProjectContents.GetAllFiles().Count().ShouldBe(4);
        }

        [Fact]
        public void InputFileResolver_InitializeShouldThrowOnMissingSharedProject()
        {
            string sourceFile = File.ReadAllText(_currentDirectory + "/TestResources/ExampleSourceFile.cs");

            string projectFile = @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>netcoreapp2.0</TargetFramework>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
    </ItemGroup>
               
     <Import Project=""../SharedProject/Example.projitems"" Label=""Shared"" />

    <ItemGroup>
        <ProjectReference Include=""../ExampleProject/ExampleProject.csproj"" />
    </ItemGroup>
</Project>";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "SharedProject", "Shared.cs"), new MockFileData(sourceFile)},
                    { _projectUnderTestPath, new MockFileData(projectFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "OneFolderDeeper", "Recursive.cs"), new MockFileData(sourceFile)},
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", "bin", "Debug", "netcoreapp2.0"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "obj", "Release", "netcoreapp2.0"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string> { "../SharedProject/Example.projitems" });
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    Properties = new Dictionary<string, string>(),
                    References = new string[] { }
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath,
                    Properties = new Dictionary<string, string>(),
                    References = new string[] { }
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            Assert.Throws<FileNotFoundException>(() => target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath)));

        }

        [Fact]
        public void InputFileResolver_InitializeShouldResolvePropertiesInSharedProjectImports()
        {
            string sourceFile = File.ReadAllText(_currentDirectory + "/TestResources/ExampleSourceFile.cs");

            string sharedFile = "<Project />";

            string projectFile = @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>netcoreapp2.0</TargetFramework>
        <IsPackable>false</IsPackable>
        <SharedDir>SharedProject</SharedDir>
    </PropertyGroup>

    <ItemGroup>
    </ItemGroup>

    <Import Project=""../$(SharedDir)/Example.projitems"" Label=""Shared"" />

    <ItemGroup>
        <ProjectReference Include=""../ExampleProject/ExampleProject.csproj"" />
    </ItemGroup>
</Project>";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "SharedProject", "Example.projitems"), new MockFileData(sharedFile)},
                    { Path.Combine(_filesystemRoot, "SharedProject", "Shared.cs"), new MockFileData(sourceFile)},
                    { _projectUnderTestPath, new MockFileData(projectFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "OneFolderDeeper", "Recursive.cs"), new MockFileData(sourceFile)},
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", "bin", "Debug", "netcoreapp2.0"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "obj", "Release", "netcoreapp2.0"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>() { Path.Combine(_filesystemRoot, "SharedProject", "Example.projitems") });
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_testProjectPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    References = new string[] { "" },
                    Properties = new Dictionary<string, string>()
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath,
                    Properties = new Dictionary<string, string>()
                    {
                        { "SharedDir", "SharedProject" },
                    },
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var result = target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath));

            var allFiles = result.ProjectContents.GetAllFiles();

            allFiles.Count().ShouldBe(3);
            allFiles.ShouldContain(f => f.Name == "Shared.cs");
        }

        [Fact]
        public void InputFileResolver_InitializeShouldThrowIfImportPropertyCannotBeResolved()
        {
            string sourceFile = File.ReadAllText(_currentDirectory + "/TestResources/ExampleSourceFile.cs");

            string sharedFile = "<Project />";

            string projectFile = @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>netcoreapp2.0</TargetFramework>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
    </ItemGroup>

    <Import Project=""../$(SharedDir)/Example.projitems"" Label=""Shared"" />

    <ItemGroup>
        <ProjectReference Include=""../ExampleProject/ExampleProject.csproj"" />
    </ItemGroup>
</Project>";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "SharedProject", "Example.projitems"), new MockFileData(sharedFile)},
                    { Path.Combine(_filesystemRoot, "SharedProject", "Shared.cs"), new MockFileData(sourceFile)},
                    { _projectUnderTestPath, new MockFileData(projectFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "OneFolderDeeper", "Recursive.cs"), new MockFileData(sourceFile)},
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", "bin", "Debug", "netcoreapp2.0"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "obj", "Release", "netcoreapp2.0"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.Is<XDocument>(d => d.ToString() == XDocument.Parse(_defaultTestProjectFileContents).ToString()))).Returns(new List<string> { });
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.Is<XDocument>(d => d.ToString() == XDocument.Parse(projectFile).ToString()))).Returns(new List<string> { "../$(SharedDir)/Example.projitems" });
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_testProjectPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    References = new string[] { }
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath,
                    Properties = new Dictionary<string, string>(),
                    References = new string[] { }
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var exception = Assert.Throws<StrykerInputException>(() => target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath)));
            exception.Message.ShouldBe($"Missing MSBuild property (SharedDir) in project reference (../$(SharedDir)/Example.projitems). Please check your project file ({_projectUnderTestPath}) and try again.");
        }

        [Fact]
        public void InputFileResolver_ResolveInputShouldFindCompileLinkedFiles()
        {
            string compileLinkedFile = File.ReadAllText(_currentDirectory + "/TestResources/ExampleSourceFile.cs");

            string projectFile = @"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <TargetFramework>netcoreapp2.0</TargetFramework>
        <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
        <Compile Include=""..\..\ExampleSourceFile.cs"" Link=""ExampleSourceFile.cs"" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include=""../ExampleProject/ExampleProject.csproj"" />
    </ItemGroup>
</Project>";

            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "ExtraFile", "ExampleSourceFile.cs"), new MockFileData(compileLinkedFile)},
                    { _projectUnderTestPath, new MockFileData(projectFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(compileLinkedFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "OneFolderDeeper", "Recursive.cs"), new MockFileData(compileLinkedFile)},
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", "bin", "Debug", "netcoreapp2.0"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "obj", "Release", "netcoreapp2.0"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string> { });
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>() { { Path.Combine(_filesystemRoot, "ExtraFile", "ExampleSourceFile.cs"), Path.Combine(_filesystemRoot, "ExampleProject", "ExampleSourceFile.cs") } });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_testProjectPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    References = new string[] { }
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath,
                    Properties = new Dictionary<string, string>(),
                    References = new string[] { }
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var result = target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath));

            (result.ProjectContents.Children.First() as FolderComposite).Children.Any(c => c.FullPath == Path.Combine(_filesystemRoot, "ExtraFile", "ExampleSourceFile.cs")).ShouldBeTrue("Compile linked file should be included in project contents");
            (result.ProjectContents.Children.First() as FolderComposite).Children.Any(c => c.RelativePath == Path.Combine("ExampleProject", "ExampleSourceFile.cs")).ShouldBeTrue("Compile linked file should be included in project contents");
        }

        [Theory]
        [InlineData("bin")]
        [InlineData("obj")]
        [InlineData("node_modules")]
        public void InputFileResolver_InitializeShouldIgnoreBinFolder(string folderName)
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "ExampleProject", "ExampleProject.csproj"), new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(_sourceFile)},
                    { Path.Combine(_filesystemRoot, "TestProject", "TestProject.csproj"), new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", folderName, "somecsharpfile.cs"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", folderName, "subfolder", "somecsharpfile.cs"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { "" },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    References = new string[] { "" }
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var result = target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath));

            result.ProjectContents.Children.Count.ShouldBe(1);
        }

        [Fact]
        public void InputFileResolver_InitializeShouldMarkFilesAsExcluded()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { _projectUnderTestPath, new MockFileData(_defaultTestProjectFileContents) },
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(_sourceFile) },
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive2.cs"), new MockFileData(_sourceFile) },
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive3.cs"), new MockFileData(_sourceFile) },
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents) },
                    { Path.Combine(_filesystemRoot, "TestProject", "Debug", "somecsharpfile.cs"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "Release", "subfolder", "somecsharpfile.cs"), new MockFileData("Bytecode") }
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_testProjectPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    References = new string[] { "" }
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var result = target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath, filesToExclude: new[] { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs") }));

            result.ProjectContents.GetAllFiles().Count(c => c.IsExcluded).ShouldBe(1);
        }

        [Fact]
        public void InputFileResolver_ShouldThrowExceptionOnNoProjectFile()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData("content")}
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { "" },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            Assert.Throws<StrykerInputException>(() => target.ScanProjectFile(Path.Combine(_filesystemRoot, "ExampleProject")));
        }

        [Fact]
        public void InputFileResolver_ShouldThrowStrykerInputExceptionOnTwoProjectFiles()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { Path.Combine(_filesystemRoot, "ExampleProject", "ExampleProject.csproj"), new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "ExampleProject2.csproj"), new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData("content")}
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { "" },
                    TargetFramework = "netcore2.1",
                    ProjectFilePath = _testProjectPath
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            Assert.Throws<StrykerInputException>(() => target.ScanProjectFile(Path.Combine(_filesystemRoot, "ExampleProject")));
        }

        [Fact]
        public void InputFileResolver_ShouldNotThrowExceptionOnTwoProjectFilesInDifferentLocations()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { Path.Combine(_filesystemRoot, "ExampleProject", "ExampleProject.csproj"), new MockFileData(_defaultTestProjectFileContents)},
                { Path.Combine(_filesystemRoot, "ExampleProject\\ExampleProject2", "ExampleProject2.csproj"), new MockFileData(_defaultTestProjectFileContents)},
                { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData("content")}
            });

            var target = new InputFileResolver(fileSystem, null);

            var actual = target.ScanProjectFile(Path.Combine(_filesystemRoot, "ExampleProject"));

            actual.ShouldBe(Path.Combine(_filesystemRoot, "ExampleProject", "ExampleProject.csproj"));
        }

        [Fact]
        public void InitialisationProcess_ShouldThrowOnMsTestV1Detected()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { _projectUnderTestPath, new MockFileData(_defaultTestProjectFileContents)},
                { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(_sourceFile)},
                { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)}
            });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { "" },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    References = new string[] { "Microsoft.VisualStudio.QualityTools.UnitTestFramework" }
                });

            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            Assert.Throws<StrykerInputException>(() => target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath)));
        }

        [Fact]
        public void InitialisationProcess_ShouldKeepDotnetTestIfIsTestProjectSet()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { _projectUnderTestPath, new MockFileData(_defaultTestProjectFileContents)},
                { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(_sourceFile)},
                { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)}
            });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { "" },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    Properties = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "IsTestProject", "true" } }),
                    References = new string[] { "" }
                });

            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);
            var options = new StrykerOptions(fileSystem: fileSystem, basePath: _basePath);

            target.ResolveInput(options);

            options.TestRunner.ShouldBe(TestRunner.DotnetTest);
        }


        [Fact]
        public void InitialisationProcess_ShouldForceVsTestIfIsTestProjectNotSetAndFullFramework()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { _projectUnderTestPath, new MockFileData(_defaultTestProjectFileContents)},
                { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(_sourceFile)},
                { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)}
            });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { "" },
                    TargetFramework = "net4.5",
                    ProjectFilePath = _testProjectPath,
                    Properties = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()),
                    References = new string[] { "" }
                });
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());

            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);
            var options = new StrykerOptions(fileSystem: fileSystem, basePath: _basePath);

            target.ResolveInput(options);

            options.TestRunner.ShouldBe(TestRunner.VsTest);
        }

        [Fact]
        public void InitialisationProcess_ShouldForceVsTestIfIsTestProjectSetFalseAndFullFramework()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { _projectUnderTestPath, new MockFileData(_defaultTestProjectFileContents)},
                { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(_sourceFile)},
                { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)}
            });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { "" },
                    TargetFramework = "net4.5",
                    ProjectFilePath = _testProjectPath,
                    Properties = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { { "IsTestProject", "false" } }),
                    References = new string[0]
                });

            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);
            var options = new StrykerOptions(fileSystem: fileSystem, basePath: _basePath);

            target.ResolveInput(options);

            options.TestRunner.ShouldBe(TestRunner.VsTest);
        }

        [Fact]
        public void InitialisationProcess_ShouldKeepDotnetTestIfNotFullFramework()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { _projectUnderTestPath, new MockFileData(_defaultTestProjectFileContents)},
                { Path.Combine(_filesystemRoot, "ExampleProject", "Recursive.cs"), new MockFileData(_sourceFile)},
                { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)}
            });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { "" },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    References = new string[0]
                });

            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);
            var options = new StrykerOptions(fileSystem: fileSystem, basePath: _basePath);

            target.ResolveInput(options);

            options.TestRunner.ShouldBe(TestRunner.DotnetTest);
        }

        [Fact]
        public void InputFileResolver_ShouldSkipXamlFiles()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    { _projectUnderTestPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "app.cs"), new MockFileData(_sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "app.xaml"), new MockFileData(_sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "app.xaml.cs"), new MockFileData(_sourceFile)},
                    { Path.Combine(_filesystemRoot, "ExampleProject", "OneFolderDeeper", "Recursive.cs"), new MockFileData(_sourceFile)},
                    { _testProjectPath, new MockFileData(_defaultTestProjectFileContents)},
                    { Path.Combine(_filesystemRoot, "TestProject", "bin", "Debug", "netcoreapp2.0"), new MockFileData("Bytecode") },
                    { Path.Combine(_filesystemRoot, "TestProject", "obj", "Release", "netcoreapp2.0"), new MockFileData("Bytecode") },
                });

            var projectFileReaderMock = new Mock<IProjectFileReader>(MockBehavior.Strict);
            projectFileReaderMock.Setup(x => x.FindSharedProjects(It.IsAny<XDocument>())).Returns(new List<string>());
            projectFileReaderMock.Setup(x => x.FindLinkedFiles(It.IsAny<XDocument>())).Returns(new Dictionary<string, string>());
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_testProjectPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>() { _projectUnderTestPath },
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _testProjectPath,
                    References = new string[] { "" }
                });
            projectFileReaderMock.Setup(x => x.AnalyzeProject(_projectUnderTestPath, null))
                .Returns(new ProjectAnalyzerResult(null, null)
                {
                    ProjectReferences = new List<string>(),
                    TargetFramework = "netcoreapp2.1",
                    ProjectFilePath = _projectUnderTestPath
                });
            var target = new InputFileResolver(fileSystem, projectFileReaderMock.Object);

            var result = target.ResolveInput(new StrykerOptions(fileSystem: fileSystem, basePath: _basePath));

            result.ProjectContents.GetAllFiles().Count().ShouldBe(2);
        }
    }
}
