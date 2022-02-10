// csproj-patcher
// (c) Anton Smetanin 2022
// Reasoning:
//
// This tool is written specifically to be used in Nix buildDotnetModule function,
// since I could not find any way to specify
// The documentation says that -p:ReferencePath=... can be used to define search paths,
// although I could not make it work. Also, it's unclear whether ReferencePath can be used
// when the name of the assembly file is not the same as the name of the assembly,
// e.g.
//
// The logic is very simple but the focus was on providing user-friendly
// error reporting at the expense of some verbosity.
//
// This tool is using Microsoft.DotNet.Cli.Sln.Internal.dll which is a part of dotnet SDK.
// Although the classes and methods used are public, the API is not documented and may be changed or removed in future versions.


using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.DotNet.Cli.Sln.Internal;

namespace CsprojReferencePatcher {
    class Program {
        private static string ProjectFileExample = "Example: --projectFile MyDir/MyProject.csproj";
        private static string ReferenceExample = "Example: --reference MyCompany.MyProject.MyModule=../MyDir/MyProject.dll";
        private static string Help = @"
Command line tool for patching C# project files [version 1.0]

Usage:    csproj-patcher --projectFile <file> [--reference <assemblyName>=<assemblyPath>]

csproj-patcher is a tool for patching C# project files (.csproj).
It searches the input file for <Reference> tags with an ""Include"" attribute
and replaces <HintPath> with the specified path if it already exists,
or adds a new <HintPath> if it doesn't.

Example:
    
    $ csproj-patcher --projectFile directory/app.csproj --reference UnityEngine.UI=../../Unity/Editor/Data/UnityExtensions/Unity/GUISystem/UnityEngine.UI.dll

    projectFile should be either a dotnet project file (.csproj) or a Visual Studio solution file (.sln).
    When the provided file is a solution file, all projects belonging to the solution will be patched.
";

        public static void Main(string[] args) {
            var projectFile = default(string);
            var references = new Dictionary<string, string>();

            #region Parse command line arguments
            {
                for (var i = 0; i < args.Length; i++) {
                    if (args[i] == "--projectFile") {
                        i++;
                        projectFile = i < args.Length ? args[i] : throw new Exception($"Error: --projectFile should be followed by a path to the project after a space.\n{ProjectFileExample}");
                    } else if (args[i] == "--reference") {
                        i++;
                        var reference = i < args.Length ? args[i] : throw new Exception($"Error: --reference should be followed by a pair of assembly name and its path separated by an equals sign, after a space.\n{ReferenceExample}");
                        var equalsSignPosition = reference.IndexOf("=", StringComparison.InvariantCulture);

                        if (equalsSignPosition == -1) {
                            throw new Exception($"Error: Could not find the equals sign (=) in the following reference: {reference}.\n--reference should be followed by a pair of assembly name and its path separated by an equals sign, after a space.\n{ReferenceExample}");
                        }

                        var assemblyName = reference.Substring(0, equalsSignPosition);
                        var assemblyPath = reference.Substring(equalsSignPosition + 1);

                        if (references.TryGetValue(assemblyName, out var existingPath)) {
                            throw new Exception($"Error: reference {reference} already defined as {assemblyName}={existingPath}. References should not be duplicated.");
                        }

                        references[assemblyName] = assemblyPath;
                    }
                }
            }
            #endregion

            #region Patch project file
            {
                if (projectFile == null) {
                    throw new Exception($"Error: --projectFile argument required.\n{ProjectFileExample}");
                }

                var unusedReferences = new Dictionary<string, string>(references);

                if (projectFile.EndsWith(".sln")) {
                    var solution = SlnFile.Read(projectFile);

                    foreach (var project in solution.Projects) {
                        Patch(Path.Combine(Path.GetDirectoryName(projectFile), project.FilePath));
                    }
                } else {
                    Patch(projectFile);
                }

                void Patch(string projectFile) {
                    try {
                        var document = new XmlDocument();
                        document.Load(projectFile);

                        foreach (XmlNode referenceNode in document.SelectNodes("//Reference[@Include]")) {
                            var originalName = referenceNode.Attributes["Include"].Value;

                            if (references.TryGetValue(originalName, out var assemblyPath)) {
                                var hintPath = referenceNode.SelectSingleNode("HintPath")
                                    ?? referenceNode.AppendChild(document.CreateElement("HintPath"));

                                if (hintPath != null) {
                                    hintPath.InnerText = assemblyPath;
                                }

                                unusedReferences.Remove(originalName);
                            }
                        }

                        document.Save(projectFile);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        foreach (var reference in unusedReferences.Keys) {
                            Console.Out.WriteLine($"Warning: Reference \"{reference}\" was not found in any of the project files. Make sure there are no typos.");
                        }
                    } catch (FileNotFoundException) {
                        throw new Exception($"Error: project file {projectFile} could not be found.");
                    } catch (XmlException) {
                        throw new Exception($"Error: provided project file {projectFile} could not be parsed as a valid XML.");
                    }
                }
            }
            #endregion
        }
    }
}