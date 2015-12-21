using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Xml;
using System.IO;
using System.Linq;

namespace FactoryDefaultWidgetFileManifestUpdater
{
    enum ExitCode : int
    {
        Success = 0,
        InvalidParameters = 1,
        Error = 2
    }

    class Program
    {
        static XmlDocument _project;
        static string _basePath;
        static bool _changedProject;
        static Stream _outStream;
        static string _projectFilename;
        static string _cfsPath;
        static Guid _factoryDefaultProviderId;
        static string _outputFileName;

        static int Main(string[] args)
        {
            if (args.Length < 4)
            {
                System.Console.Error.WriteLine("Usage: FactoryDefaultWidgetFileManifestUpdater /proj:PROJECT_FILE /providerid:GUID /cfs:CFS_BASE_PATH /out:OUTPUT_FILE");
                return (int)ExitCode.InvalidParameters;
            }

            try
            {
                ParseCommandLineArgs(args);
                ValidateCommandLineArgs();

                _project = new XmlDocument();
                _project.Load(_projectFilename);

                _basePath = System.IO.Path.GetDirectoryName(_projectFilename) + System.IO.Path.DirectorySeparatorChar;
                _changedProject = false;

                RemoveAllProviderFilesFromProject();

                if (File.Exists(_outputFileName))
                    File.Delete(_outputFileName);

                using (_outStream = File.OpenWrite(_outputFileName))
                {
                    WriteManifestSource();
                }

                if (_changedProject)
                    _project.Save(_projectFilename);
            }
            catch (Exception ex)
            {
                System.Console.Error.Write(ex.ToString());
                return (int)ExitCode.Error;
            }

            return (int)ExitCode.Success;
        }

        static void WriteManifestSource()
        {
            WriteManifestSource(@"using System;
using System.Collections.Generic;
using Samples.Model;

namespace Samples.Generated
{
	internal static class FactoryDefaultWidgetFileManifest
	{
		internal static List<InstallableFile> Files()
        {
            var files = new List<InstallableFile>();
			
");

            Guid factoryDefaultProviderId, widgetId, themeId;
            foreach (var filePath in Directory.GetFiles(Path.Combine(_cfsPath, "defaultwidgets", _factoryDefaultProviderId.ToString("N")), "*.*", SearchOption.AllDirectories))
            {
                var file = new FileInfo(filePath);
                var pathComponents = filePath
                    .Substring(_cfsPath.Length, filePath.Length - _cfsPath.Length - file.Name.Length)
                    .Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                var version = GetDateBasedVersion(file.LastWriteTimeUtc);

                if (pathComponents.Length == 2 && Guid.TryParse(pathComponents[1], out factoryDefaultProviderId))
                {
                    var embedPath = EmbedProviderFileInProject(file);

                    // widget definition file
                    WriteManifestSourceLine(
                        string.Format(
                            "\t\t\tfiles.Add(new WidgetDefinitionFile() {{ FileName = \"{0}\", LastModifiedVersion = new System.Version({1}, {2}, {3}, {4}), WidgetProviderId = new System.Guid(\"{5}\"), ResourcePath = @\"{6}\" }});",
                            file.Name,
                            version.Major,
                            version.Minor,
                            version.Build,
                            version.Revision,
                            factoryDefaultProviderId.ToString(),
                            embedPath
                            )
                        );
                }
                else if (pathComponents.Length == 3 && Guid.TryParse(pathComponents[1], out factoryDefaultProviderId) && Guid.TryParse(pathComponents[2], out widgetId))
                {
                    var embedPath = EmbedProviderFileInProject(file, widgetId);

                    // widget supplementary file
                    WriteManifestSourceLine(
                       string.Format(
                           "\t\t\tfiles.Add(new SupplementaryFile() {{ FileName = \"{0}\", LastModifiedVersion = new System.Version({1}, {2}, {3}, {4}), WidgetId = new System.Guid(\"{5}\"), WidgetProviderId = new System.Guid(\"{6}\"), ResourcePath = @\"{7}\" }});",
                           file.Name,
                           version.Major,
                           version.Minor,
                           version.Build,
                           version.Revision,
                           widgetId.ToString(),
                           factoryDefaultProviderId.ToString(),
                           embedPath
                           )
                       );
                }
                else if (pathComponents.Length == 4 && Guid.TryParse(pathComponents[1], out factoryDefaultProviderId) && Guid.TryParse(pathComponents[2], out widgetId) && Guid.TryParse(pathComponents[3], out themeId))
                {
                    var embedPath = EmbedProviderFileInProject(file, widgetId, themeId);

                    // theme-versioned widget supplementary file
                    WriteManifestSourceLine(
                        string.Format(
                            "\t\t\tfiles.Add(new SupplementaryFile() {{ FileName = \"{0}\", LastModifiedVersion = new System.Version({1}, {2}, {3}, {4}), ThemeId = new System.Guid(\"{5}\"), WidgetId = new System.Guid(\"{6}\"), WidgetProviderId = new System.Guid(\"{7}\"), ResourcePath = @\"{8}\" }});",
                            file.Name,
                            version.Major,
                            version.Minor,
                            version.Build,
                            version.Revision,
                            themeId.ToString(),
                            widgetId.ToString(),
                            factoryDefaultProviderId.ToString(),
                            embedPath
                            )
                        );
                }
            }

            WriteManifestSourceLine(@"
			return files;
		}
	}
}");
        }

        static void WriteManifestSourceLine(string text)
        {
            WriteManifestSource(text + Environment.NewLine);
        }

        static void WriteManifestSource(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            _outStream.Write(bytes, 0, bytes.Length);
        }

        static Version GetDateBasedVersion(DateTime d)
        {
            return new Version(d.Year, d.Month, d.Day, (d.Hour * 60) + d.Minute);
        }

        static void RemoveAllProviderFilesFromProject()
        {
            string path;

            XmlNamespaceManager mgr = new XmlNamespaceManager(_project.NameTable);
            mgr.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");

            var nodes = _project.SelectNodes("/x:Project/x:ItemGroup/x:EmbeddedResource[starts-with(@Include,'" + MakeProjectRelativePath() + "')]", mgr);
            if (nodes.Count > 0)
            {
                Log("Found " + nodes.Count.ToString() + " existing embedded files for this factory default provider. Removing all...");

                foreach (XmlNode node in nodes)
                {
                    path = Path.Combine(_basePath, node.Attributes["Include"].Value);
                    if (File.Exists(path))
                        File.Delete(path);

                    node.ParentNode.RemoveChild(node);

                    Log("Removed: " + node.Attributes["Include"].Value);
                }

                _changedProject = true;
            }

            path = Path.Combine(_basePath, MakeProjectRelativePath());
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        
        static string EmbedProviderFileInProject(FileInfo file, Guid? widgetId = null, Guid? themeId = null)
        {
            XmlNamespaceManager mgr = new XmlNamespaceManager(_project.NameTable);
            mgr.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");

            var fileProjectPath = MakeProjectRelativePath(widgetId, themeId, file.Name);

            var fileProjectPathComponents = fileProjectPath.Split(Path.DirectorySeparatorChar);
            var directory = new DirectoryInfo(_basePath);
            for (var i = 0; i < fileProjectPathComponents.Length - 1; i++)
            {
                var childDir = directory.GetDirectories(fileProjectPathComponents[i]).FirstOrDefault();
                if (childDir == null)
                    childDir = directory.CreateSubdirectory(fileProjectPathComponents[i]);

                directory = childDir;
            }

            File.Copy(file.FullName, Path.Combine(_basePath, fileProjectPath), true);

            var nodes = _project.SelectNodes("/x:Project/x:ItemGroup/x:EmbeddedResource[@Include='" + fileProjectPath + "']", mgr);
            if (nodes.Count > 0)
            {
                Log("File already embedded: " + fileProjectPath);
                return fileProjectPath;
            }

            // does it exist as a different node?
            nodes = _project.SelectNodes("/x:Project/x:ItemGroup/*[@Include='" + fileProjectPath + "']", mgr);
            if (nodes.Count > 0)
            {
                Log("Removed " + nodes.Count + " existing records in project for file: " + fileProjectPath);

                // remove them
                foreach (XmlNode node in nodes)
                    node.ParentNode.RemoveChild(node);

                _changedProject = true;
            }

            var parentNode = _project.SelectSingleNode("/x:Project/x:ItemGroup", mgr);
            if (parentNode != null)
            {
                System.Console.Out.WriteLine("Added embedded resource: " + fileProjectPath);

                var element = _project.CreateElement("EmbeddedResource", "http://schemas.microsoft.com/developer/msbuild/2003");
                var attribute = _project.CreateAttribute("Include");
                attribute.Value = fileProjectPath;
                element.Attributes.Append(attribute);
                parentNode.AppendChild(element);

                _changedProject = true;
            }
            else
                throw new InvalidDataException("Couldn't add embedded resource. Couldn't find valid parent in project file.");

            return fileProjectPath;
        }

        static string MakeProjectRelativePath()
        {
            return Path.Combine("filestorage\\defaultwidgets\\", _factoryDefaultProviderId.ToString("N"));
        }

        static string MakeProjectRelativePath(Guid? widgetId, Guid? themeId, string fileName)
        {
            var path = MakeProjectRelativePath();

            if (widgetId.HasValue)
                path = Path.Combine(path, widgetId.Value.ToString("N"));

            if (themeId.HasValue)
                path = Path.Combine(path, themeId.Value.ToString("N"));

            return Path.Combine(path, fileName);
        }

        static void ValidateCommandLineArgs()
        {
            if (string.IsNullOrEmpty(_projectFilename) || !File.Exists(_projectFilename))
                throw new ArgumentException("Project file was not provided or does not exist.", "/proj");

            if (string.IsNullOrEmpty(_cfsPath) || !Directory.Exists(_cfsPath))
                throw new ArgumentException("CFS path was not provided or does not exist.", "/cfs:");

            if (string.IsNullOrEmpty(_outputFileName))
                throw new ArgumentException("Output filename was not provided.", "/out");

            if (_factoryDefaultProviderId == Guid.Empty)
                throw new ArgumentException("Provider ID was not provided or is not a valid GUID.", "/providerid");
        }

        static void ParseCommandLineArgs(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("/proj:"))
                {
                    _projectFilename = GetCommandLineValue("/proj:", arg);
                    Log("Param /proj:=" + _projectFilename);
                }
                else if (arg.StartsWith("/cfs:"))
                {
                    _cfsPath = GetCommandLineValue("/cfs:", arg).Replace("\"", "");
                    Log("Param /cfs:=" + _cfsPath);
                }
                else if (arg.StartsWith("/out:"))
                {
                    _outputFileName = GetCommandLineValue("/out:", arg);
                    Log("Param /out:" + _outputFileName);
                }
                else if (arg.StartsWith("/providerid:"))
                {
                    if (Guid.TryParse(GetCommandLineValue("/providerid:", arg), out _factoryDefaultProviderId))
                        Log("Param /providerid: " + _factoryDefaultProviderId.ToString("N"));
                }
                else if (arg.Trim() != string.Empty)
                    Log("Unrecognized param " + arg);
            }
        }

        static string GetCommandLineValue(string parameterName, string value)
        {
            return value.Substring(parameterName.Length, value.Length - parameterName.Length).Trim();
        }

        static void Log(string message)
        {
            Console.Out.WriteLine(message);
        }
    }
}
