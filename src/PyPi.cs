using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

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
            Directory.CreateDirectory(directory);

            var output = Path.Combine(directory, $"PythonLicenses{DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss")}.txt");

            foreach (var file in files)
            {
                foreach (var line in await File.ReadAllLinesAsync(file))
                {
                    try
                    {
                        var split = line.Split("==");
                        var url = this.InsightMakerPackages().ContainsKey(split[0])
                            ? this.InsightMakerPackages()[split[0]]
                            : $"https://pypi.org/pypi/{split[0]}/{split[1]}/json";

                        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                        using (var response = await this.client.SendAsync(request))
                        {
                            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
                            var license = content.SelectToken("info").SelectToken("license").ToString();
                            if (!string.IsNullOrEmpty(license))
                            {
                                await this.WriteToFile(output, license, split[0], split[1], url);
                            }
                            else
                            {
                                var metadata = content.SelectToken("info").SelectToken("classifiers").ToObject<List<string>>();
                                await this.WriteToFile(output, metadata.Where(s => s.Contains("License")).First(), split[0], split[1], url);
                            }
                        }
                    }
                    catch
                    {
                        if (line.Contains("backports.csv"))
                        {
                            await this.WriteToFile(output, "PSF", "backports.csv", "1.0.7", "https://pypi.org/project/backports.csv/");
                        }
                        else
                        {
                            Console.WriteLine($"Failed to get license from {line}");
                        }
                    }
                }
            }
        }

        private async Task WriteToFile(string outpath, string content, string packageName, string packageVersion, string packageUrl)
        {
            await File.AppendAllTextAsync(outpath, $"\n--------License For {packageName}-{packageVersion}--------\n");
            await File.AppendAllTextAsync(outpath, $"{packageUrl}\n");
            await File.AppendAllTextAsync(outpath, content);
        }

        private Dictionary<string, string> InsightMakerPackages() => new Dictionary<string, string>()
        {
            {"torch", "https://pypi.org/pypi/torch/1.5.0/json"},
            {"torchvision", "https://pypi.org/pypi/torchvision/0.6.0/json"},
            {"PyQt5-sip", "https://pypi.org/pypi/PyQt5-sip/12.8.0/json"}
        };

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
    }
}
