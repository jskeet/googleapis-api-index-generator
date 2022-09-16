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

using Google.Protobuf.Reflection;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using System.Collections.Concurrent;

namespace Google.Cloud.Tools.ApiHistoryReader;

internal class EnumChangeDetector : IHistoryProcessor
{
    private string _historyDirectory;

    internal EnumChangeDetector(string historyDirectory) =>
        _historyDirectory = historyDirectory;

    public void ProcessHistory(IEnumerable<HistoryEntry> historyEntries)
    {
        var enumEntries = new ConcurrentDictionary<string, EnumHistoryEntry>();

        foreach (var entry in historyEntries)
        {
            foreach (var enumDefinition in GetEnumDefinitions(entry.FileDescriptors))
            {
                string name = enumDefinition.FullName;
                var count = enumDefinition.Values.Count;
                var enumEntry = enumEntries.GetOrAdd(name, name => new EnumHistoryEntry
                {
                    FullName = name,
                    IntroducedSha = entry.Sha,
                    IntroducedTimestamp = entry.Timestamp,
                    MinValues = count,
                    MaxValues = count,
                    Values = enumDefinition.Values.Select(def => def.Name).ToList()
                });
                if (enumEntry.Values.Count != count)
                {
                    enumEntry.Values = enumDefinition.Values.Select(def => def.Name).ToList();
                    enumEntry.MaxValues = Math.Max(count, enumEntry.MaxValues);
                    enumEntry.MinValues = Math.Min(count, enumEntry.MinValues);
                    enumEntry.Changes++;
                }
            }
        }

        var ordered = enumEntries.Values.OrderBy(e => e.FullName).ToList();
        var settings = new JsonSerializerSettings().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        File.WriteAllText(Path.Combine(_historyDirectory, "enum-changes.json"), JsonConvert.SerializeObject(ordered, Formatting.Indented, settings));

        Console.WriteLine($"Total enums: {ordered.Count}");
        Console.WriteLine($"Enums with no changes: {ordered.Count(x => x.Changes == 0)}");
        Console.WriteLine($"Enums with changes: {ordered.Count(x => x.Changes != 0)}");
    }

    private IEnumerable<EnumDescriptor> GetEnumDefinitions(IReadOnlyList<FileDescriptor> fileDescriptors) =>
        fileDescriptors.SelectMany(GetEnumDefinitions);

    private IEnumerable<EnumDescriptor> GetEnumDefinitions(FileDescriptor fileDescriptor) =>
        fileDescriptor.EnumTypes.Concat(fileDescriptor.MessageTypes.SelectMany(GetEnumDefinitions));

    private IEnumerable<EnumDescriptor> GetEnumDefinitions(MessageDescriptor messageDescriptor) =>
        messageDescriptor.EnumTypes.Concat(messageDescriptor.NestedTypes.SelectMany(GetEnumDefinitions));
}

public class EnumHistoryEntry
{
    public string FullName { get; set; }
    public string IntroducedSha { get; set; }
    public Instant IntroducedTimestamp { get; set; }
    public int MinValues { get; set; }
    public int MaxValues { get; set; }
    public int Changes { get; set; }
    public List<string> Values { get; set; }
}
