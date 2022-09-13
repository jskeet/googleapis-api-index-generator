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

using NodaTime;
using System.Text.Json.Serialization;

namespace Google.Cloud.Tools.EnumChangeAnalyzer;

public class EnumHistoryEntry
{
    public string FullName { get; set; }
    public string IntroducedSha { get; set; }
    public Instant IntroducedTimestamp { get; set; }
    public int MinValues { get; set; }
    public int MaxValues { get; set; }
    public int Changes { get; set; }

    [JsonIgnore]
    public int CurrentValues { get; set; }
}
