using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NugetUtility
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<PackageOptions>(args);
            return await result.MapResult(
                options => Execute(options),
                errors => Task.FromResult(1));
        }

        private static async Task<int> Execute(PackageOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.ProjectDirectory))
            {
                Console.WriteLine("ERROR(S):");
                Console.WriteLine("-i\tInput the Directory Path (csproj file)");

                return 1;
            }

            await Program.CreateDirectories(options.OutputFileName);
            var baseOutput = options.OutputFileName;
            if (options.NugetLicenses)
            {
                Console.WriteLine("Beginning Nuget License Aggregation...");
                options.OutputFileName = Path.Combine(baseOutput, "nuget\\");
                Methods methods = new Methods(options);
                var projectsWithPackages = await methods.GetPackages();
                var mappedLibraryInfo = methods.MapPackagesToLibraryInfo(projectsWithPackages);
                HandleInvalidLicenses(methods, mappedLibraryInfo, options.AllowedLicenseType);

                if (options.ExportLicenseTexts)
                {
                    await methods.ExportLicenseTexts(mappedLibraryInfo);
                }

                if (options.Print == true)
                {
                    Console.WriteLine();
                    Console.WriteLine("Project Reference(s) Analysis...");
                    methods.PrintLicenses(mappedLibraryInfo);
                }

                if (options.JsonOutput)
                {
                    methods.SaveAsJson(mappedLibraryInfo);
                }
                else
                {
                    methods.SaveAsTextFile(mappedLibraryInfo);
                }
            }

            if (options.PythonLicenses)
            {
                Console.WriteLine("Beginning Python License Aggregation...");
                options.OutputFileName = Path.Combine(baseOutput, "python\\");
                var python = new PyPi(options);
                await python.Run(options.OutputFileName);
            }

            if (options.NPMLicense)
            {
                Console.WriteLine("Beginning NPM License Aggregation...");
                options.OutputFileName = Path.Combine(baseOutput, "npm\\");
                var npm = new NPM(options.OutputFileName);
                await npm.Run(options.ProjectDirectory);
            }

            return 0;
        }

        private static void HandleInvalidLicenses(Methods methods, List<LibraryInfo> libraries, ICollection<string> allowedLicenseType)
        {
            var invalidPackages = methods.ValidateLicenses(libraries);

            if (!invalidPackages.IsValid)
            {
                throw new InvalidLicensesException<LibraryInfo>(invalidPackages, allowedLicenseType);
            }
        }

        private static async Task CreateDirectories(string basePath)
        {
            await Task.Run(() =>
            {
                var baseDirectory = Path.GetDirectoryName(basePath);
                Directory.CreateDirectory(baseDirectory);
                Directory.CreateDirectory(Path.Combine(baseDirectory, "python"));
                Directory.CreateDirectory(Path.Combine(baseDirectory, "nuget"));
                Directory.CreateDirectory(Path.Combine(baseDirectory, "npm"));
                Directory.CreateDirectory(Path.Combine(baseDirectory, "other"));
            });


        }
    }
}
