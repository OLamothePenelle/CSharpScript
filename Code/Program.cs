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
        static void PrintUsage()
        {
            Console.WriteLine(
@"
C#.exe allows you to execute cs files from the command line.

Usage: C# [switches] <filename> [args...]

[switches]
    -t, -template: Outputs a template HelloWorld C# file named 'filename'
    -h, -help: Prints this help message

<filename>:
    File to compile and run (unless using -t)

[args...]
    Arguments to pass to the script when executed.
");
        }

        static void Main(string[] args)
        {
            bool OutputHelp = args.Any(arg => arg == "-h" || arg == "-help");
            if (OutputHelp)
            {
                PrintUsage();
                return;
            }
            
            string filename = args.FirstOrDefault(arg => !arg.StartsWith("-"));
            if (filename == null)
            {
                Console.WriteLine($"Missing argument for C# file to execute.");
                PrintUsage();
                return;
            }
            
            bool OutputTemplate = args.Any(arg => arg == "-t" || arg == "-template");
            if (OutputTemplate)
            {
                WriteTemplateFile(filename);
                return;
            }
            else if (!File.Exists(filename))
            {
                Console.WriteLine($"Can't find file  \"{args[0]}\" in current directory: {System.IO.Directory.GetCurrentDirectory()}");
                PrintUsage();
                return;
            }

            string codeFilename = args[0];
            string exeFilename = Compile(codeFilename);
            
            Execute(exeFilename, args.Skip(1).ToArray());
        }

        private static string Compile(string codeFilename)
        {
            string tempFolderName = ".tmp";
            
            string exeFilename = Path.Combine(Directory.GetCurrentDirectory(), tempFolderName, System.IO.Path.GetFileNameWithoutExtension(codeFilename) + ".exe");

            if (!System.IO.Directory.Exists(tempFolderName))
            {
                System.IO.Directory.CreateDirectory(tempFolderName);
            }
            else if (File.Exists(exeFilename))
            {
                var lastCompilationTime = File.GetLastWriteTimeUtc(exeFilename);
                var lastCodeChangeTime = File.GetLastWriteTimeUtc(codeFilename);
                TimeSpan timeDifference = (lastCompilationTime - lastCodeChangeTime).Duration();
                bool alreadyCompiled = timeDifference < TimeSpan.FromSeconds(2);
                if (alreadyCompiled)
                {
                    return exeFilename;
                }
            }

        
            string sourceCode = File.ReadAllText(codeFilename);
            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(sourceCode);

            IEnumerable<string> DefaultNamespaces = new[]
            {
                "System",
                "System.IO",
                "System.Net",
                "System.Linq",
                "System.Text",
                "System.Text.RegularExpressions",
                "System.Collections.Generic"
            };

            string RuntimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();

            string[] DotNetLibraries = new[]
            {
                RuntimeDirectory + "mscorlib.dll",
                RuntimeDirectory + "System.dll",
                RuntimeDirectory + "System.Core.dll",
                typeof(object).GetTypeInfo().Assembly.Location,
                RuntimeDirectory + "System.Runtime.dll",
                RuntimeDirectory + "System.Diagnostics.Debug.dll",
                RuntimeDirectory + "System.IO.dll",
                RuntimeDirectory + "System.Linq.dll",
                RuntimeDirectory + "System.Linq.Parallel.dll",
                RuntimeDirectory + "System.Console.dll"
            };

            CSharpCompilationOptions DefaultCompilationOptions =
                new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithOverflowChecks(true)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithUsings(DefaultNamespaces);

            string assemblyName = "CSharpScriptAssembly";
            var compilation = CSharpCompilation.Create(assemblyName)
                .AddReferences(DotNetLibraries.Select(x => MetadataReference.CreateFromFile(x)))
                .WithOptions(DefaultCompilationOptions)
                .AddSyntaxTrees(parsedSyntaxTree);

            var result = compilation.Emit(exeFilename);

            if (!result.Success)
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    Console.WriteLine(diagnostic);
                }
            }

            return exeFilename;
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

        private static void WriteTemplateFile(string filename)
        {
            const string HelloWorldTemplate =
@"
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
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello World!"");
        }
    }
}
";
            if (string.IsNullOrWhiteSpace(Path.GetExtension(filename)))
            {
                filename += ".cs";
            }

            if (File.Exists(filename))
            {
                Console.WriteLine($"Can't output template file: {filename} already exists.");
                return;
            }

            File.WriteAllText(filename, HelloWorldTemplate);
            Console.WriteLine($"File {filename} created.\nUse C# {filename} to execute it.");
        }
    }
}
