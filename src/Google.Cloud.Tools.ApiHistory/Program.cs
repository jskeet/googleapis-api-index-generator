// Copyright 2022, Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Cloud.Tools.ApiHistory;
using LibGit2Sharp;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using System.Diagnostics;

if (args.Length != 4)
{
    Console.WriteLine("Expected arguments:");
    Console.WriteLine("  - Path to googleapis repository");
    Console.WriteLine("  - Path to protoc");
    Console.WriteLine("  - Path to well-known-types root");
    Console.WriteLine("  - Output directory");
    return 1;
}

string googleapis = args[0];
string protoc = args[1];
string wktRoot = args[2];
string output = args[3];

Directory.CreateDirectory(output);

using var repo = new Repository(googleapis);
var allCommits = repo.Commits.ToList();
Console.WriteLine($"Repo has {allCommits.Count} commits");
var protoCommits = allCommits.Where(ContainsProtoChanges).ToList();
Console.WriteLine($"Creating history for {protoCommits.Count} commits affecting .proto files");

string googleDirectory = Path.Combine(googleapis, "google");
string grafeasDirectory = Path.Combine(googleapis, "grafeas2");
var commonOptions = new[]
{
    $"-I{wktRoot}",
    $"-I{googleapis}",
    $"--include_imports",
};
var optionsFile = Path.Combine(output, "protoc-options.txt");

var entries = new List<HistoryIndexEntry>();

foreach (var commit in protoCommits)
{
    Console.WriteLine($"{DateTime.UtcNow:yyyy-MM-dd'T'HH:mm:ss'Z'} Processing {commit.Sha}");
    Commands.Checkout(repo, commit);
    var files = GetProtoFiles(googleDirectory).Concat(GetProtoFiles(grafeasDirectory));
    var options = commonOptions.Concat(files).Append($"--descriptor_set_out={output}/{commit.Sha}");
    File.WriteAllLines(optionsFile, options);
    var psi = new ProcessStartInfo
    {
        FileName = protoc,
        Arguments = $"@{optionsFile}",
        RedirectStandardError = true,
        RedirectStandardOutput = true
    };
    var process = Process.Start(psi);
    DumpOutput(process.StandardError, Path.Combine(output, $"{commit.Sha}-err.txt"));
    DumpOutput(process.StandardOutput, Path.Combine(output, $"{commit.Sha}-out.txt"));
    process.WaitForExit();
    var entry = HistoryIndexEntry.FromCommit(commit);
    if (process.ExitCode != 0)
    {
        Console.WriteLine("Protoc failed");
        entry.ProtocFailed = true;
    }
    entries.Add(entry);
}

entries = entries.OrderBy(e => e.Timestamp).ToList();
var settings = new JsonSerializerSettings().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
File.WriteAllText(Path.Combine(output, "index.json"), JsonConvert.SerializeObject(entries, Formatting.Indented, settings));

return 0;

IEnumerable<string> GetProtoFiles(string directory)
{
    if (!Directory.Exists(directory))
    {
        return Array.Empty<string>();
    }
    return Directory.GetFiles(directory, "*.proto", SearchOption.AllDirectories)
        .Select(p => p.Replace('\\', '/'));
}

bool ContainsProtoChanges(Commit commit)
{
    var parents = commit.Parents.ToList();
    // If there's more than one parent, always generate.
    if (parents.Count != 1)
    {
        return true;
    }
    var diff = repo.Diff.Compare<TreeChanges>(commit.Tree, parents[0].Tree);
    return diff.Any(change => change.Path.EndsWith(".proto", StringComparison.Ordinal));
}

void DumpOutput(StreamReader reader, string file)
{
    new Thread(Dump).Start();
    void Dump()
    {
        using var output = File.CreateText(file);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            output.WriteLine(line);
        }
    }
}