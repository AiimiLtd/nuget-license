using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.GZip;
using Octokit;
using System.Text.RegularExpressions;

namespace NugetUtility
{
    class PyPi
    {
        private string requirementsLocation;
        private List<string> files;
        private HttpClient client;

        public PyPi(string location)
        {
            this.requirementsLocation = location;
            this.files = GetRelevantFilesInLocation();
            this.client = new HttpClient();
        }

        public async Task Run(string outputFile)
        {
            var directory = Path.GetDirectoryName(outputFile);
            var typesDirectory = Path.Combine(directory, "Types");
            var tempDirectory = Path.Combine(directory, "temp");
            if (Directory.Exists(typesDirectory))
            {
                Directory.Delete(typesDirectory, true);
            }

            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(tempDirectory);
            Directory.CreateDirectory(typesDirectory);

            var timeNow = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss");
            var indexPath = Path.Combine(directory, $"Index.csv");
            File.Delete(indexPath);
            var output = Path.Combine(directory, $"PythonLicenses{timeNow}.txt");

            var types = new Dictionary<string, List<string>>();

            var packages = new HashSet<(string, string)>();
            Console.WriteLine("Building package list");
            foreach (var file in files)
            {
                foreach (var line in await File.ReadAllLinesAsync(file))
                {
                    var splits = line.Split("==");
                    packages.Add((splits[0], splits[1]));
                }
            }
            Console.WriteLine($"Found {packages.Count} packages!");
            var sortedPackages = packages.ToList(); //If we care about order
            sortedPackages.Sort((x, y) => x.Item1.CompareTo(y.Item1));

            foreach (var line in sortedPackages)
            {
                var name = line.Item1;
                var version = line.Item2;
                Console.WriteLine($"Handling {name}");
                try
                {
                    var url = this.InsightMakerPackages().ContainsKey(name)
                        ? this.InsightMakerPackages()[name]
                        : $"https://pypi.org/pypi/{name}/{version}/json";

                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    using (var response = await this.client.SendAsync(request))
                    {
                        JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
                        var license = content.SelectToken("info").SelectToken("license").ToString();

                        if (name == "python-docx")
                        {
                            license = "MIT";
                        }
                        if (name == "tornado")
                        {
                            license = "Apache2.0";
                        }

                        if (!string.IsNullOrEmpty(license))
                        {
                            if (!types.ContainsKey(license))
                            {
                                types.Add(license, new List<string>());
                            }
                            types[license].Add(name);
                            await this.WriteToIndexFile(indexPath, name, license, url);
                        }
                        else
                        {
                            var metadata = content.SelectToken("info").SelectToken("classifiers").ToObject<List<string>>();
                            var licenseSplits = metadata.Where(s => s.Contains("License")).First().Replace(" ", "").Split("::");
                            license = string.Join("-", licenseSplits.Skip(1));
                            if (!types.ContainsKey(license))
                            {
                                types.Add(license, new List<string>());
                            }
                            types[license].Add(name);
                            await this.WriteToIndexFile(indexPath, name, license, url);
                        }

                        try
                        {
                            this.PackageToLicenseText().TryGetValue(name, out var projectURL);
                            if (string.IsNullOrEmpty(projectURL))
                            {
                                projectURL = content.SelectToken("info").SelectToken("home_page").ToString();
                            }
                            if (name.Contains("jupyter"))
                            {
                                projectURL = "https://github.com/jupyter/jupyter_client";
                            }

                            if (projectURL.Contains("github") || this.PackageToLicenseText().ContainsKey(name))
                            {
                                await this.GetLicenseFromURL(projectURL, output, name, version);
                            }
                            else
                            {
                                await this.GetLicenseFromPackage(content, tempDirectory, output, name, version);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("already exists"))
                            {
                                continue;
                            }
                            Console.WriteLine($"Failed to get license from {line} - {ex.Message}");
                        }

                    }
                }
                catch (Exception ex)
                {
                    if (name.Contains("backports.csv"))
                    {
                        await this.WriteToIndexFile(indexPath, "backports.csv", "PSF", "https://pypi.org/project/backports.csv/");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to get license from {line.ToString()} - {ex.Message}");
                    }
                }
            }
            this.WriteLicenseTypes(typesDirectory, types);
            Directory.Delete(tempDirectory, true);
        }

        #region LicenseTextDiscovery
        private async Task GetLicenseFromURL(string url, string output, string name, string version)
        {
            if (url.Contains("github"))
            {
                var githubToken = "69a37edea8276ab4a2183641d5ebd0a381218c22";
                var endpoint = "https://api.github.com/repos";
                var splits = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var author = "";
                var repo = "";
                try
                {
                    author = splits[2];
                    repo = splits[3];
                }
                catch
                {
                    author = splits[1].Split('.')[0];
                    repo = splits[2];
                }
                var githubUrl = Path.Combine(endpoint, author, repo, "license");

                var githubClient = new GitHubClient(new ProductHeaderValue("LicenseExtractor"), new Uri(githubUrl));
                var basicAuth = new Credentials("lmorgan86", githubToken);
                githubClient.Credentials = basicAuth;

                var licenseContents = await githubClient.Repository.GetLicenseContents(author, repo);
                url = licenseContents.DownloadUrl;

            }

            using (var response = await this.client.GetAsync(url))
            {
                await this.WriteToFile(output, await response.Content.ReadAsStringAsync(), name, version, url);
            }
        }


        private async Task GetLicenseFromPackage(JObject content, string directory, string output, string name, string version)
        {
            var downloadableOptions = content.SelectToken($"releases['{version}']") as JArray;
            var downloadableUrl = "";
            try
            {
                downloadableUrl = downloadableOptions[1].SelectToken("url").ToString();
            }
            catch
            {
                downloadableUrl = downloadableOptions[0].SelectToken("url").ToString();
            }
            var extension = $"{downloadableUrl.Substring(downloadableUrl.LastIndexOf($"{version}") + version.Length)}";
            var specifier = $"{downloadableUrl.Substring(downloadableUrl.LastIndexOf('/') + 1)}";
            var tempLocation = Path.Combine(directory, specifier);

            if (extension.Contains(".whl"))
            {
                tempLocation = tempLocation.Replace(".whl", ".zip");
            }

            using (var res = await this.client.GetAsync(downloadableUrl))
            using (var fs = new FileStream(tempLocation, System.IO.FileMode.CreateNew))
            {
                await res.Content.CopyToAsync(fs);
            }

            this.ExtractPackage(tempLocation, directory);

            var licenseFolder = "";
            var licenseFileName = "";

            if (this.PackageNameToLicenseFolderName().ContainsKey(name))
            {
                name = this.PackageNameToLicenseFolderName()[name];
            }

            try
            {
                licenseFolder = $"{name}-{version}";
                licenseFileName = this.GetLicenseFile(Path.Combine(directory, licenseFolder), name);
            }
            catch
            {
                licenseFolder = $"{name}-{version}.dist-info";
                licenseFileName = this.GetLicenseFile(Path.Combine(directory, licenseFolder), name);
            }
            await this.WriteToFile(output, await File.ReadAllTextAsync(licenseFileName), name, version);
            Directory.Delete(Path.Combine(directory, licenseFolder), true);
            File.Delete(tempLocation);
        }
        #endregion

        #region Utilities
        private void ExtractPackage(string archiveName, string destination)
        {
            if (archiveName.EndsWith("tar.gz"))
            {
                using (var inStream = File.OpenRead(archiveName))
                using (var gzStream = new GZipInputStream(inStream))
                {
                    var tarchive = TarArchive.CreateInputTarArchive(gzStream);
                    tarchive.ExtractContents(destination);
                    tarchive.Close();
                    gzStream.Close();
                    inStream.Close();
                }
            }
            else if (archiveName.EndsWith("zip"))
            {
                ZipFile.ExtractToDirectory(archiveName, destination);
            }
        }

        private async Task WriteToFile(string outpath, string content, string packageName, string packageVersion, string packageUrl = "")
        {
            await File.AppendAllTextAsync(outpath, $"\n--------License For {packageName}-{packageVersion}--------\n");
            await File.AppendAllTextAsync(outpath, $"{packageUrl}\n");
            await File.AppendAllTextAsync(outpath, content);
        }

        private async Task WriteToIndexFile(string outpath, string name, string licenseType, string packageURL)
        {
            await File.AppendAllTextAsync(outpath, $"{name},{licenseType},{packageURL}\n");
        }

        private List<string> GetRelevantFilesInLocation()
        {
            var list = new List<string>();
            foreach (var file in Directory.GetFiles(this.requirementsLocation))
            {
                if (file.Contains("requirements"))
                {
                    list.Add(file);
                }
            }
            return list;
        }

        private void WriteLicenseTypes(string directory, Dictionary<string, List<string>> types)
        {
            foreach (var (k, v) in types)
            {
                var fileName = this.FileNameCleanup(k);
                var destination = Path.Combine(directory, $"{fileName}.txt");
                File.AppendAllLinesAsync(destination, v);
            }
        }

        private string FileNameCleanup(string name) => Regex.Replace(name, @"<[^>]*>", String.Empty).Replace(" ", "").Replace("/", "").Replace("\\", "");

        private string GetLicenseFileName(string packageName)
        {
            switch (packageName)
            {
                case "astroid":
                    return "COPYING";

                case "docutils":
                    return "COPYING";

                case "ipython-genutils":
                    return "COPYING.md";

                default:
                    return "LICENSE";
            }
        }

        private string GetLicenseFile(string inputPath, string name) => Directory.GetFiles(inputPath).Where(f => f.ToUpper().Contains(this.GetLicenseFileName(name))).First();
        #endregion

        #region Dictionaries
        private Dictionary<string, string> InsightMakerPackages() => new Dictionary<string, string>()
        {
            {"torch", "https://pypi.org/pypi/torch/1.5.0/json"},
            {"torchvision", "https://pypi.org/pypi/torchvision/0.6.0/json"},
            {"PyQt5-sip", "https://pypi.org/pypi/PyQt5-sip/12.8.0/json"}
        };

        private Dictionary<string, string> PackageToLicenseText() => new Dictionary<string, string>()
        {
            {"et-xmlfile", "https://bitbucket.org/openpyxl/et_xmlfile/raw/50973a6de49c9451ec9cdbadd0e4a5a95a2e52b4/LICENCE.rst"},
            {"matplotlib", "https://matplotlib.org/_sources/users/license.rst.txt"},
            {"PyQt5-sip", "https://www.gnu.org/licenses/gpl-3.0.txt"},
            {"scikit-learn", "https://github.com/scikit-learn/scikit-learn"},
            {"mpld3", "https://github.com/mpld3/mpld3"},
            {"networkx", "https://github.com/networkx/networkx/"},
            {"torch", "https://github.com/pytorch/pytorch"},
            {"pywin32", "https://github.com/python/cpython/"}, //Uses the PSF license, so using the python github for the license text.
            {"typesentry", "https://github.com/h2oai/typesentry"}
        };

        private Dictionary<string, string> PackageNameToLicenseFolderName() => new Dictionary<string, string>()
        {
            {"importlib-metadata", "importlib_metadata"},
            {"ipython-genutils", "ipython_genutils"},
            {"spacy-lookups-data", "spacy_lookups_data"}
        };
        #endregion
    }
}
