using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace CSharpScript
{
    class Program
    {
        const string HelloWorldTemplate = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Primitives;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace CSharpScript
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello World!"");
        }

        static List<string> ExecuteCommand(string filename, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = false;
            startInfo.UseShellExecute = false;
            startInfo.FileName = filename;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.Arguments = arguments;
            startInfo.RedirectStandardOutput = true;

            List<string> outputLines = new List<string>();
            try
            {
                Process exeProcess = Process.Start(startInfo);
                while (!exeProcess.StandardOutput.EndOfStream)
                {
                    outputLines.Add(exeProcess.StandardOutput.ReadLine());
                }
            }
            catch (Exception e)
            {
                // Log error.
                outputLines.Add(e.ToString());
            }

            return outputLines;
        }
    }
}
";

        const string Usage = @"
C#.exe allows you to execute cs files from the command line.

Usage: C# [switches] <filename> [args...]

[switches]
    -t, -template: Outputs a template HelloWorld C# file named 'filename'
    -h, -help: Prints this help message

<filename>:
    File to compile and run (unless using -t)

[args...]
    Arguments to pass to the script when executed.
";

        static void PrintUsage()
        {
            Console.WriteLine(Usage);
        }

        static bool Verbose = false;
        static void VerboseWrite(string message) { if (Verbose) Console.WriteLine(message); }

        static void Main(string[] args)
        {
            bool OutputHelp = args.Any(arg => arg == "-h" || arg == "-help");
            if (OutputHelp)
            {
                PrintUsage();
                return;
            }

            Verbose = args.Any(arg => arg == "-v" || arg == "-verbose");

            string codeFilename = args.FirstOrDefault(arg => !arg.StartsWith("-"));
            bool outputTemplate = args.Any(arg => arg == "-t" || arg == "-template");
            bool force = args.Any(arg => arg == "-f" || arg == "-force");

            if (codeFilename == null)
            {
                Console.WriteLine($"Missing argument for C# file to execute.");
                PrintUsage();
                return;
            }
            
            if (outputTemplate)
            {
                VerboseWrite($"Writing template file \"{codeFilename}\".");
                WriteTemplateFile(codeFilename, force);
                return;
            }
            else if (!File.Exists(codeFilename))
            {
                Console.WriteLine($"Can't find file  \"{args[0]}\" in current directory: {System.IO.Directory.GetCurrentDirectory()}");
                PrintUsage();
                return;
            }


            string exeFilename;
            VerboseWrite($"Compiling file \"{codeFilename}\".");
            if (Compile(codeFilename, out exeFilename, force))
            {
                VerboseWrite($"Compilation successful. Executing \"{exeFilename}\".");
                Execute(exeFilename, args.Skip(1).ToArray());
            }
        }

        private static bool Compile(string codeFilename, out string exeFilename, bool force)
        {
            string tempFolderName = ".cstmp"; // TODO: Should this be beside the target code file?
            
            exeFilename = Path.Combine(Directory.GetCurrentDirectory(), tempFolderName, System.IO.Path.GetFileNameWithoutExtension(codeFilename) + ".exe");

            if (!System.IO.Directory.Exists(tempFolderName))
            {
                System.IO.Directory.CreateDirectory(tempFolderName);
            }
            else if (File.Exists(exeFilename))
            {
                if (force)
                {
                    File.Delete(exeFilename);
                }
                else
                {
                    var lastCompilationTime = File.GetLastWriteTimeUtc(exeFilename);
                    var lastCodeChangeTime = File.GetLastWriteTimeUtc(codeFilename);
                    TimeSpan timeDifference = (lastCompilationTime - lastCodeChangeTime).Duration();
                    bool alreadyCompiled = timeDifference < TimeSpan.FromSeconds(2);
                    if (alreadyCompiled)
                    {
                        return true;
                    }
                }
            }
        
            string sourceCode = File.ReadAllText(codeFilename);
            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(sourceCode);

            IEnumerable<string> DefaultNamespaces = new[]
            {
                "System",
                //"System.IO",
                //"System.Net",
                //"System.Linq",
                //"System.Text",
                //"System.Text.RegularExpressions",
                //"System.Collections.Generic",
                //"System.ComponentModel"
            };

            string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

            //https://stackoverflow.com/questions/58453115/roslyn-c-sharp-csharpcompilation-compiling-dynamic
            string[] DotNetLibraries = new[]
            {
                runtimeDirectory + "mscorlib.dll",
                runtimeDirectory + "System.dll",
                runtimeDirectory + "System.Collections.dll",
                runtimeDirectory + "System.Console.dll",
                runtimeDirectory + "System.Core.dll",
                runtimeDirectory + "System.Data.dll",
                runtimeDirectory + "System.IO.dll",
                runtimeDirectory + "System.Linq.dll",
                runtimeDirectory + "System.Linq.Parallel.dll",
                runtimeDirectory + "System.Runtime.dll",
                runtimeDirectory + "System.Runtime.Extensions.dll", //System.Runtime.Extensions

                typeof(object).GetTypeInfo().Assembly.Location,
                typeof(System.Diagnostics.Process).GetTypeInfo().Assembly.Location,
                typeof(System.ComponentModel.Component).GetTypeInfo().Assembly.Location
            };

            CSharpCompilationOptions defaultCompilationOptions =
                new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithOverflowChecks(true)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithUsings(DefaultNamespaces);

            string assemblyName = "CSharpScriptAssembly";
            var compilation = CSharpCompilation.Create(assemblyName)
                .AddReferences(DotNetLibraries.Select(x => MetadataReference.CreateFromFile(x)))
                .WithOptions(defaultCompilationOptions)
                .AddSyntaxTrees(parsedSyntaxTree);

            var result = compilation.Emit(exeFilename);

            if (!result.Success)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    Console.WriteLine(diagnostic);
                }
                return false;
            }

            return true;
        }

        private static void Execute(string filename, string[] args)
        {
            Assembly assembly = Assembly.LoadFile(filename);

            MethodInfo method = assembly.DefinedTypes.Select(type => type.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)).FirstOrDefault(m => m != null);
            if (method == null)
            {
                Console.WriteLine("Could not find a Main method. Make sure to implement \"static void Main(string[] args) { }\"");
                return;
            }

            method.Invoke(null, new[] { args });
        }

        private static void WriteTemplateFile(string filename, bool force)
        {
            if (string.IsNullOrWhiteSpace(Path.GetExtension(filename)))
            {
                filename += ".cs";
            }

            if (File.Exists(filename))
            {
                if (force)
                {
                    File.Delete(filename);
                }
                else
                {
                    Console.WriteLine($"Can't output template file: {filename} already exists.");
                    return;
                }
            }

            File.WriteAllText(filename, HelloWorldTemplate);
            Console.WriteLine($"File {filename} created.\nUse C# {filename} to execute it.");
        }
    }
}
