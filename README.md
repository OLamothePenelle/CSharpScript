Compiles a C#.exe that can be used to compile .cs files from the command line  
and execute them, basically creating a C# scripting language.  

How To deploy:  
1. Right click project CSharpScript and click Publish  
2. Click Publish button  
3. Either:  
    3.1. Copy C#.exe from publish\ to C:\Windows\System32, or  
    3.2. Add publish\ to your PATH.  
4. Write some C# file, including a static void main function  
5. Run it from a console command: c# MyNewFile.cs [arguments to give the program]  

Test:
An Hello World script is available here: publish\test.cs  
1. Open a Console in that folder  
2. Execute: c# test.cs SomeTestArgument  

TODO:  
    callstacks are not great when the .cs file contains errors  
