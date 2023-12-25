using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace DetectDuplicates;


public class SolutionFile
{
    public string Name { get; set; }
    public string FilePath { get; set; }
    public List<ProjectFile> ProjectFiles { get; set; } = new List<ProjectFile>();
    public SolutionFile(string name, string path)
    {
        Name = name;
        FilePath = path;
        // Read the entire solution file
        var solutionFile = File.ReadAllText(FilePath);
        // Find all the project references
        var projectReferences = Regex.Matches(solutionFile, """
                                                            Project\("\{.*\}"\)\s*=\s*"(.*)",\s*"(.*)",\s*".*"
                                                            """);
        // For each project reference
        foreach (Match projectMatch in projectReferences)
        {
            // Skip the folders (they don't have a path)
            // TODO - Handle .vbproj and .fsproj
            if (projectMatch.Groups[2].Value.Contains(".csproj"))
            {
                // Get the project file path
                var projectFilePath = Path.Combine(Path.GetDirectoryName(FilePath)!, projectMatch.Groups[2].Value);
                ProjectFiles.Add(new ProjectFile(projectFilePath));
            }
        }
    }
}

public class ProjectFile : IEquatable<ProjectFile>
{
    public string Name { get; set; }
    public string Path { get; set; }
    public List<ProjectFile> ProjectReferences { get; set; } = new List<ProjectFile>();
    public List<PackageReference> PackageReferences { get; set; } = new List<PackageReference>();
    
    private List<ProjectFile>? _transitiveProjectReferences;
    public List<ProjectFile> TransitiveProjectReferences
    {
        get
        {
            if (_transitiveProjectReferences == null)
            {
                _transitiveProjectReferences = new List<ProjectFile>();
                foreach (var projectFile in ProjectReferences)
                {
                    _transitiveProjectReferences.AddRange(projectFile.ProjectReferences);
                    _transitiveProjectReferences.AddRange(projectFile.TransitiveProjectReferences);
                }
            }
            return _transitiveProjectReferences;
        }
    }
    
    private List<PackageReference>? _transitivePackageReferences;
    public List<PackageReference> TransitivePackageReferences
    {
        get
        {
            if (_transitivePackageReferences == null)
            {
                _transitivePackageReferences = new List<PackageReference>();
                foreach (var projectFile in ProjectReferences)
                {
                    _transitivePackageReferences.AddRange(projectFile.PackageReferences);
                    _transitivePackageReferences.AddRange(projectFile.TransitivePackageReferences);
                }
            }
            return _transitivePackageReferences;
        }
    }

    public ProjectFile(string path)
    {
        Name = System.IO.Path.GetFileName(path);
        Path = path;
        // Load the project file
        var projectXml = XDocument.Load(Path);
        // Parse this as an XML file
        var itemGroupElements = projectXml.Root.Elements("ItemGroup");
        var projectReferencesXml = itemGroupElements.Elements("ProjectReference");
        // TODO - Handle the target framework and conditional includes
        foreach (var projectReferenceXml in projectReferencesXml)
        {
            var includePath = projectReferenceXml.Attribute("Include")?.Value;
            var fullPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), includePath);
            ProjectReferences.Add(new ProjectFile(fullPath));
        }
        var packageReferencesXml = itemGroupElements.Elements("PackageReference");
        foreach (var packageReferenceXml in packageReferencesXml)
        {
            var includeName = packageReferenceXml.Attribute("Include")?.Value;
            var includeVersion = packageReferenceXml.Attribute("Version")?.Value;
            PackageReferences.Add(new PackageReference(includeName, includeVersion));
        }
    }

    public List<PackageReference> DuplicatePackageReferences() => this.PackageReferences.Intersect(TransitivePackageReferences).ToList();
    public List<ProjectFile> DuplicateProjectReferences() => this.ProjectReferences.Intersect(TransitiveProjectReferences).ToList();
    public bool Equals(ProjectFile other) => other.Name.Equals(this.Name);
    public override int GetHashCode() => Name.GetHashCode();
}

// TODO - Memoize this?
public class PackageReference : IEquatable<PackageReference>
{
    public string Name { get; set; }
    public string Version { get; set; }
    public List<PackageReference> PackageReferences { get; set; } = new List<PackageReference>();
    
    public List<PackageReference> TransitivePackageReferences
    {
        get
        {
            var allPackageReferences = new List<PackageReference>();
            foreach (var packageReference in PackageReferences)
            {
                allPackageReferences.AddRange(packageReference.PackageReferences);
                allPackageReferences.AddRange(packageReference.TransitivePackageReferences);
            }
            return allPackageReferences;
        }
    }
    
    public PackageReference(string name, string version)
    {
        Name = name;
        Version = version;
        // Load the nuspec file
        var nuspecPath = Path.Combine(Constants.NugetRepositoryFolder, Name.ToLowerInvariant(), Version, $"{Name.ToLowerInvariant()}.nuspec");
        if (!File.Exists(nuspecPath))
            // TODO - Handle looking in other locations?
            return;
        // Parse this as an XML file
        var xReader = new XmlTextReader(nuspecPath);
        xReader.Namespaces = false;
        var nuspecXml = XDocument.Load(xReader);
        var dependenciesXml = nuspecXml.Root.Element("metadata").Element("dependencies");
        if (dependenciesXml == null)
            return;
        // TODO - Handle the target framework
        var dependencyGroupsXml = dependenciesXml.Element("group") ?? dependenciesXml;
        var dependencies = dependencyGroupsXml.Elements("dependency");
        foreach (var dependency in dependencies)
        {
            var refname = dependency.Attribute("id")?.Value;
            var refversion = dependency.Attribute("version")?.Value;
            PackageReferences.Add(new PackageReference(refname, refversion));
        }
    }

    public bool Equals(PackageReference other) => other.Name.Equals(this.Name);

    public override int GetHashCode() => Name.GetHashCode();
}