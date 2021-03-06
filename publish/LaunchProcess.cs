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
            string processName = args.Length > 0 ? args[0] : "cmd.exe";
            using (Process process = new Process())
            {
                System.Console.WriteLine($"Starting process {processName}");
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.FileName = processName;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.Arguments = "info";
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                System.Console.WriteLine(output);
            }
        }
    }
}
