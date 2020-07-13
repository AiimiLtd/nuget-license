using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetUtility
{
    class NPM
    {
        const string URLBASE = "https://www.npmjs.com/package/";

        private string indexPath;
        private string textPath;
        private string typesDirectory;
        private string ngxLicense;
        private HashSet<string> completed;

        public NPM(string outputFileName)
        {
            var directory = Path.GetDirectoryName(outputFileName);
            this.typesDirectory = Path.Combine(directory, "types");
            this.indexPath = Path.Combine(outputFileName, "Index.csv");
            this.textPath = Path.Combine(directory, $"NPMLicenses{DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss")}.txt");
            this.completed = new HashSet<string>();

            if (Directory.Exists(this.typesDirectory))
            {
                Directory.Delete(this.typesDirectory, true);
            }
            Directory.CreateDirectory(this.typesDirectory);
            File.Delete(this.indexPath);

        }

        public async Task Run(string inputLocation)
        {
            var inputList = Directory.GetFiles(Path.Combine(inputLocation, "artifacts"), "3rdpartylicenses.txt", SearchOption.AllDirectories);

            foreach (var inputFile in inputList)
            {
                await foreach (var chunk in this.SplitFileAsync(await File.ReadAllTextAsync(inputFile)))
                {
                    var sections = chunk.Split(Environment.NewLine);
                    var name = sections[0];
                    if (this.completed.Contains(name))
                    {
                        continue;
                    }
                    this.completed.Add(name);
                    await this.HandleChunks(sections);
                }
            }
        }

        private async IAsyncEnumerable<string> SplitFileAsync(string input)
        {
            using (var reader = new StringReader(input))
            {
                var chunk = new StringBuilder();
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line == "" || line.StartsWith('\n'))
                    {
                        var output = (char)reader.Peek();
                        if (output.ToString() == "" || output == '\n')
                        {
                            // Read through the file until we hit the end of the current chunk.
                            // Some chunks have extra lines, this should catch that.
                            do
                            {
                                _ = await reader.ReadLineAsync();
                                output = (char)reader.Peek();
                            } while (output == '\n');
                            yield return chunk.ToString();
                            chunk = new StringBuilder();
                        }
                        else
                        {
                            chunk.AppendLine(line);
                        }
                    }
                    else
                    {
                        chunk.AppendLine(line);
                    }
                }
            }
        }

        private async Task HandleChunks(string[] sections)
        {
            var name = sections[0];
            //if (this.completed.Contains(name))
            //{
            //    return;
            //}
            //this.completed.Add(name);

            var licenseType = sections[1];
            var text = string.Join(Environment.NewLine, sections.Skip(2));

            if (name == "ngx-bootstrap")
            {
                this.ngxLicense = text;
            }

            if (name.StartsWith("@angular/"))
            {
                this.WriteToIndex(name, "MIT", $"{Path.Combine(URLBASE, name)}");
                await this.WriteToDetailed(name, "https://raw.githubusercontent.com/angular/angular/master/LICENSE \n", $"{Path.Combine(URLBASE, name)}");
                await this.HandleChunks(sections.Skip(3).ToArray());
                return;
            }

            if (licenseType == string.Empty)
            {
                if (name.StartsWith("ngx-bootstrap"))
                {
                    this.WriteToIndex(name, "MIT", $"{Path.Combine(URLBASE, "ngx-bootstrap")}");
                    await this.WriteToDetailed(name, this.ngxLicense, $"{Path.Combine(URLBASE, "ngx-bootstrap")}");
                }
                else
                {
                    this.WriteToIndex(name, "unknown");
                }
                await this.HandleChunks(sections.Skip(2).ToArray());
                return;
            }
            this.WriteToIndex(name, licenseType);
            await this.WriteToDetailed(name, text, $"{Path.Combine(URLBASE, name)}");
        }

        private void WriteToIndex(string name, string license, string url = "")
        {
            url = string.IsNullOrEmpty(url) ? Path.Combine(URLBASE, name) : url;
            File.AppendAllTextAsync(this.indexPath, $"{name},{license},{url}\n");
            this.WriteToTypesFile(name, license);
        }

        private async Task WriteToDetailed(string packageName, string content, string packageUrl)
        {
            await File.AppendAllTextAsync(this.textPath, $"\n--------License For {packageName}--------\n");
            await File.AppendAllTextAsync(this.textPath, $"{packageUrl}\n");
            await File.AppendAllTextAsync(this.textPath, content);
        }

        private void WriteToTypesFile(string name, string license)
        {
            File.AppendAllTextAsync($"{Path.Combine(this.typesDirectory, $"{license.Replace(" ", "-")}.txt")}", $"{name}\n");
        }

    }
}
