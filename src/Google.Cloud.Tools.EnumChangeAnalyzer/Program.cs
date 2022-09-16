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

using Google.Cloud.Tools.EnumChangeAnalyzer;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using System.Text.RegularExpressions;

if (args.Length != 1)
{
    Console.WriteLine("Expected arguments:");
    Console.WriteLine("  - Path to enum change analysis JSON file");
    return 1;
}

var json = File.ReadAllText(args[0]);
var settings = new JsonSerializerSettings().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
var entries = JsonConvert.DeserializeObject<List<EnumHistoryEntry>>(json, settings);

var groupedByApi = entries.GroupBy(entry => ExtractApiName(entry.FullName))
    .Where(g => g.Key is not null)
    .OrderBy(g => g.Key)
    .ToList();

Console.WriteLine($"API: unchanged / changed");
var totalWithChanges = 0;
foreach (var group in groupedByApi)
{
    var unchanged = group.Count(e => e.Changes == 0);
    var changed = group.Count(e => e.Changes != 0);
    Console.WriteLine($"{group.Key}: {unchanged} / {changed}");
    if (changed != 0)
    {
        totalWithChanges++;
    }
}
Console.WriteLine($"Total APIs: {groupedByApi.Count} of which {totalWithChanges} had changes to enums");
Console.WriteLine($"Total enums: {groupedByApi.Sum(g => g.Count())} of which {groupedByApi.Sum(g => g.Count(e => e.Changes != 0))} had changes");
Console.WriteLine();
Console.WriteLine("Enums with changes:");
foreach (var group in groupedByApi)
{
    foreach (var entry in group.Where(e => e.Changes != 0))
    {
        Console.WriteLine($"{entry.FullName}: {string.Join(", ", entry.Values)}");
    }
}


return 0;

string ExtractApiName(string fullName)
{
    var pattern = new Regex($@"^(?<api>(.*)\.v[\d+])\.");
    var match = pattern.Match(fullName);
    return match.Success ? match.Groups["api"].Value : null;
}