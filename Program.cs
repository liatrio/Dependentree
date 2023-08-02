using System.Xml;
using System.Xml.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.CommandLine;
using System.Runtime.Versioning;

public class Program {
    public static void Main(string path, string heatdata = "", string output = "dependentreeData.js") {
        if (string.IsNullOrEmpty(path)) {
            Console.WriteLine("Must supply path to repository");
            return;
        }

        var parsedHeatData = new Dictionary<string, int>();

        if (!string.IsNullOrEmpty(heatdata)) {
            var lines = File.ReadAllLines(heatdata)
                .Where(w => !string.IsNullOrWhiteSpace(w));
            var lineRegex = new Regex(@"\s*(\d*)\s*(.*)");

            foreach(var line in lines) {
                var matches = lineRegex.Match(line);
                var folder = Path.GetDirectoryName(Path.Combine(path, matches.Groups[2].Value));
                var changeCount = int.Parse(matches.Groups[1].Value);

                if (parsedHeatData.ContainsKey(folder)) {
                    parsedHeatData[folder] += changeCount;
                }
                else {
                    parsedHeatData.Add(folder, int.Parse(matches.Groups[1].Value));
                }
            }
        }

        var data = new Data();

        data.Projects = LoadProjects(path);

        foreach(var project in data.Projects) {
            var dependsOnProjects = data.Projects.Where(w => project.Value.DependsOn.Contains(w.Key));

            var changedFolders = parsedHeatData.Keys.Where(w => w.StartsWith(project.Value.Path));

            foreach(var changedFolder in changedFolders) {
                project.Value.Heat += parsedHeatData[changedFolder];
            }

            if(data.HeatCap < project.Value.Heat) {
                data.HeatCap = project.Value.Heat;
            }

            foreach(var dependsOnProject in dependsOnProjects) {
                dependsOnProject.Value.DependedOnBy.Add(project.Key);
            }
        }

        var outputData = "dependentreeData = " + JsonSerializer.Serialize(data, typeof(Data), new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(output, outputData);
    }

    public static Dictionary<string, Project> LoadProjects(string path) {
        var projects = new Dictionary<string, Project>();
        var serializer = new XmlSerializer(typeof(ProjectFile));

        var subDirectories = Directory.GetDirectories(path);

        foreach (var subDirectory in subDirectories) {
            var subProjects = LoadProjects(subDirectory);

            projects = projects.Concat(subProjects)
                .ToLookup(x => x.Key, x => x.Value)
                .ToDictionary(x => x.Key, g => g.First());
        }

        var projFiles = Directory.GetFiles(path, "*.csproj");

        foreach(var projFile in projFiles) {
            using (var reader = new FileStream(projFile, FileMode.Open)) {
                var xmlReader = new XmlTextReader(reader) { Namespaces = false };
                var parsed = serializer.Deserialize(xmlReader) as ProjectFile;
                var name = Path.GetFileNameWithoutExtension(projFile).Replace(" ", "_");

                var projectDependencies = parsed.ItemGroups
                    .Where(w => w.ProjectReferences.Any())
                    .SelectMany(s => s.ProjectReferences)
                    .Where(w => w.Include != "")
                    .Select(s => Path.GetFileNameWithoutExtension(s.Include))
                    .ToList();

                var directDependencies = parsed.ItemGroups
                    .Where(w => w.References.Any())
                    .SelectMany(s => s.References)
                    .Where(w =>
                        w.Include != ""
                        && !string.IsNullOrEmpty(w.HintPath)
                        && File.Exists(Path.Combine(Path.GetDirectoryName(projFile), w.HintPath))
                    ).Select(s => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projFile), s.HintPath)))
                    .ToList();

                var framework = parsed.PropertyGroups
                    .Where(w =>
                        !string.IsNullOrEmpty(w.TargetFramework)
                        || !string.IsNullOrEmpty(w.TargetFrameworkVersion)
                    ).Select(s => s.TargetFramework ?? s.TargetFrameworkVersion)
                    .FirstOrDefault();

                var project = new Project {
                    Path = Path.GetDirectoryName(projFile),
                    DependsOn = projectDependencies,
                    DependedOnBy = new List<string>(),
                    DependsOnDirect = directDependencies,
                    Framework = framework,
                    Name = name
                };

                if (projects.ContainsKey(name)) {
                    var count = projects.Keys.Count(c => c.StartsWith(name));
                    name = $"{name}_{count}";
                }

                projects.Add(name, project);
            }
        }

        return projects;
    }

    public class Reference
    {
        [XmlAttribute]
        public string Include { get; set; }
        public string HintPath { get; set; }
    }


    public class ProjectReference {
        [XmlAttribute]
        public string Include { get; set; }

        public ProjectReference() {
            Include = "";
        }
    }

    public class ItemGroup {
        [XmlElement("ProjectReference")]
        public List<ProjectReference> ProjectReferences { get; set; }
        [XmlElement("Reference")]
        public List<Reference> References { get; set; }

        public ItemGroup() {
            ProjectReferences = new List<ProjectReference>();
        }
    }

    public class PropertyGroup {
        [XmlElement("TargetFramework")]
        public string? TargetFramework { get; set; }
        [XmlElement("TargetFrameworkVersion")]
        public string? TargetFrameworkVersion { get; set; }
    }

    [XmlRoot("Project")]
    public class ProjectFile {
        [XmlElement("ItemGroup")]
        public List<ItemGroup> ItemGroups { get; set; }
        [XmlElement("PropertyGroup")]
        public List<PropertyGroup> PropertyGroups { get; set; }

        public ProjectFile() {
            ItemGroups = new List<ItemGroup>();
            PropertyGroups = new List<PropertyGroup>();
        }
    }

    public class Data {
        public Dictionary<string, Project> Projects { get; set; }
        public List<string> DirectReferences { get; set; }
        public int HeatCap { get; set; }

        public Data() {
            DirectReferences = new List<string>();
            Projects = new Dictionary<string, Project>();
        }
    }

    public class Project {
        public string Name { get; set; }
        public string Path { get; set; }
        public List<string> DependsOn { get; set; }
        public List<string> DependedOnBy { get; set; }
        public List<string> DependsOnDirect { get; set; }

        public int Heat { get; set; }
        public string Framework { get; set; }

        public Project() {
            Path = "";
            DependsOn = new List<string>();
            DependedOnBy = new List<string>();
            DependsOnDirect = new List<string>();
            Heat = 0;
            Framework = "";
            Name = "";
        }
    }
}