using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DetectDuplicates
{
    class Program
    {
        static int Main(string[] args)
        {
            var path = args.Any() ? args[0] : Directory.GetCurrentDirectory();
            // TODO - Use logging?
            Console.WriteLine($"Looking for duplicates in {path}");
            Console.WriteLine("");

            bool duplicatesFound = false;

            var slnFiles = Directory.EnumerateFiles(path, "*.sln");
            
            foreach (var slnFile in slnFiles)
            {
                var fileName = Path.GetFileName(slnFile);
                var solutionFile = new SolutionFile(fileName, slnFile);
                foreach (var projectFile in solutionFile.ProjectFiles.Where(projectFile => projectFile.DuplicatePackageReferences().Count != 0 || projectFile.DuplicateProjectReferences().Count != 0))
                {
                    Console.WriteLine($"Project:{projectFile.Name}");
                    Console.WriteLine($"  Path:{projectFile.Path}");
                    Console.WriteLine("  Duplicate Project References:");
                    Console.WriteLine($"  {string.Join(Environment.NewLine + "  ", projectFile.DuplicateProjectReferences().Select(x => x.Name))}");
                    Console.WriteLine("  Duplicate Package References:");
                    Console.WriteLine($"  {string.Join(Environment.NewLine + "  ", projectFile.DuplicatePackageReferences().Select(x => x.Name))}");
                }
            }

            Console.WriteLine("");
            Console.WriteLine("Duplicate detection complete!");
            return 0;
        }
    }
}