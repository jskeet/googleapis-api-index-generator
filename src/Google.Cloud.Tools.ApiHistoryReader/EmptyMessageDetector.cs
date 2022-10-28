using Google.Protobuf;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;

namespace Google.Cloud.Tools.ApiHistoryReader;

internal class EmptyMessageDetector : IHistoryProcessor
{
    private readonly string _historyDirectory;

    internal EmptyMessageDetector(string historyDirectory) =>
        _historyDirectory = historyDirectory;

    public ExtensionRegistry ExtensionRegistry => null;

    public void ProcessHistory(IEnumerable<HistoryEntry> historyEntries)
    {
        var seenMessageNames = new HashSet<string>();
        var emptyMessagesByName = new Dictionary<string, EmptyMessageHistoryEntry>();
        var previouslyEmptyMessages = new List<PreviouslyEmptyMessageHistoryEntry>();
        var warnings = new List<string>();

        foreach (var entry in historyEntries)
        {
            foreach (var message in entry.GetAllMessages())
            {
                var name = message.FullName;
                bool newMessage = seenMessageNames.Add(name);
                bool empty = message.Fields.InDeclarationOrder().Count == 0;
                if (empty)
                {
                    if (!emptyMessagesByName.ContainsKey(name))
                    {
                        if (!newMessage)
                        {
                            warnings.Add($"  {name} was non-empty, but became empty in {entry.Sha}.");
                        }
                        emptyMessagesByName[name] = new EmptyMessageHistoryEntry
                        {
                            FullName = name,
                            IntroducedSha = entry.Sha,
                            IntroducedTimestamp = entry.Timestamp
                        };
                    }
                }
                else
                {
                    if (emptyMessagesByName.TryGetValue(name, out var emptyEntry))
                    {
                        emptyMessagesByName.Remove(name);
                        previouslyEmptyMessages.Add(emptyEntry.AfterPopulation(entry));
                    }
                }
            }
        }

        var emptyMessagesReport = new EmptyMessagesReport
        {
            EmptyMessages = emptyMessagesByName.Values.OrderBy(e => e.IntroducedTimestamp).ThenBy(e => e.FullName).ToList(),
            PreviouslyEmptyMessages = previouslyEmptyMessages.OrderBy(e => e.IntroducedTimestamp).ThenBy(e => e.FullName).ToList(),
            Warnings = warnings
        };

        var settings = new JsonSerializerSettings().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        File.WriteAllText(Path.Combine(_historyDirectory, "empty-messages.json"), JsonConvert.SerializeObject(emptyMessagesReport, Formatting.Indented, settings));

        // Log the warnings to the console as a point of interest. (There may well not be any.)
        warnings.ForEach(Console.WriteLine);
    }
}

public class EmptyMessagesReport
{
    public List<EmptyMessageHistoryEntry> EmptyMessages { get; set; }
    public List<PreviouslyEmptyMessageHistoryEntry> PreviouslyEmptyMessages { get; set; }
    public List<string> Warnings { get; set; }
}

public class EmptyMessageHistoryEntry
{
    public string FullName { get; set; }
    public string IntroducedSha { get; set; }
    public Instant IntroducedTimestamp { get; set; }

    internal PreviouslyEmptyMessageHistoryEntry AfterPopulation(HistoryEntry entry) =>
        new PreviouslyEmptyMessageHistoryEntry
        {
            FullName = FullName,
            IntroducedSha = IntroducedSha,
            IntroducedTimestamp = IntroducedTimestamp,
            PopulatedSha = entry.Sha,
            PopulatedTimestamp = entry.Timestamp
        };
}

public class PreviouslyEmptyMessageHistoryEntry
{
    public string FullName { get; set; }
    public string IntroducedSha { get; set; }
    public Instant IntroducedTimestamp { get; set; }

    public string PopulatedSha { get; set; }
    public Instant PopulatedTimestamp { get; set; }
}
