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
using Google.Cloud.Tools.ApiHistoryReader;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.Win32;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using NodaTime.Text;

if (args.Length != 1)
{
    Console.WriteLine("Expected arguments:");
    Console.WriteLine("  - Path to API history directory");
    return 1;
}

string historyDirectory = args[0];
string historyIndexFile = Path.Combine(historyDirectory, "index.json");
string historyIndexJson = File.ReadAllText(historyIndexFile);

var settings = new JsonSerializerSettings().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
var historyIndex = JsonConvert.DeserializeObject<List<HistoryIndexEntry>>(historyIndexJson, settings);

Console.WriteLine($"Index contains {historyIndex.Count} entries, with {historyIndex.Count(entry => !entry.ProtocFailed)} descriptor sets");

var timestampPattern = InstantPattern.General;

IHistoryProcessor processor = new EnumChangeDetector(historyDirectory);
processor.ProcessHistory(LoadHistoryEntries());

IEnumerable<HistoryEntry> LoadHistoryEntries()
{
    foreach (var historyIndexEntry in historyIndex)
    {
        if (historyIndexEntry.ProtocFailed)
        {
            Log($"Skipping {historyIndexEntry.Sha} from {timestampPattern.Format(historyIndexEntry.Timestamp)}: protoc failed");
            continue;
        }
        Log($"Processing {historyIndexEntry.Sha} from {timestampPattern.Format(historyIndexEntry.Timestamp)}");
        using var fileDescriptorSetStream = File.OpenRead(Path.Combine(historyDirectory, historyIndexEntry.Sha));
        var fileDescriptorSet = FileDescriptorSet.Parser.ParseFrom(fileDescriptorSetStream);
        // TODO: This will be horribly inefficient... see just how bad it is.
        var descriptorByteStrings = fileDescriptorSet.File.Select(proto => proto.ToByteString());
        var allDescriptors = FileDescriptor.BuildFromByteStrings(descriptorByteStrings, null)
            .ToList()
            .AsReadOnly();
        var historyEntry = new HistoryEntry(historyIndexEntry.Sha, historyIndexEntry.Timestamp, allDescriptors);
        yield return historyEntry;
    }
}

return 0;

void Log(string message) =>
    Console.WriteLine($"{timestampPattern.Format(SystemClock.Instance.GetCurrentInstant())}: {message}");


