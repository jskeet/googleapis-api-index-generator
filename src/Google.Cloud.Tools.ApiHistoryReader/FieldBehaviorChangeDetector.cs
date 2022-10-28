using Google.Api;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Serialization.JsonNet;

namespace Google.Cloud.Tools.ApiHistoryReader;

internal class FieldBehaviorChangeDetector : IHistoryProcessor
{
    private static readonly IList<FieldBehavior> s_noBehaviors = new FieldBehavior[0];

    private readonly string _historyDirectory;

    internal FieldBehaviorChangeDetector(string historyDirectory) =>
        _historyDirectory = historyDirectory;

    public ExtensionRegistry ExtensionRegistry => new ExtensionRegistry { FieldBehaviorExtensions.FieldBehavior };

    public void ProcessHistory(IEnumerable<HistoryEntry> historyEntries)
    {
        var changes = new List<Change>();
        // true = required, false = optional, null = neither specified
        var fieldToRequired = new Dictionary<string, bool?>();

        foreach (var historyEntry in historyEntries)
        {
            foreach (var field in GetFields(historyEntry.FileDescriptors))
            {
                var behaviors = field.GetOptions()?.GetExtension(FieldBehaviorExtensions.FieldBehavior) ?? s_noBehaviors;
                var required = behaviors.Contains(FieldBehavior.Required) ? true
                    : behaviors.Contains(FieldBehavior.Optional) ? false
                    : default(bool?);

                var previous = fieldToRequired.GetValueOrDefault(field.FullName);
                fieldToRequired[field.FullName] = required;

                if (previous != required && previous != null)
                {
                    var change = new Change
                    {
                        FullName = field.FullName,
                        ChangeSha = historyEntry.Sha,
                        ChangeTimestamp = historyEntry.Timestamp,
                        OldRequired = previous,
                        NewRequired = required
                    };
                    changes.Add(change);
                    Console.WriteLine($"In {historyEntry.Sha}, {field.FullName} changed from {Describe(previous)} to {Describe(required)}");
                }
            }
        }

        Console.WriteLine($"Completed: {changes.Count} changes in {fieldToRequired.Count} fields");

        var ordered = changes.OrderBy(e => e.FullName).ToList();
        var settings = new JsonSerializerSettings().ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
        File.WriteAllText(Path.Combine(_historyDirectory, "field-changes.json"), JsonConvert.SerializeObject(ordered, Formatting.Indented, settings));

        string Describe(bool? required) => required switch
        {
            true => "required",
            false => "optional",
            null => "unspecified"
        };
    }

    private IEnumerable<FieldDescriptor> GetFields(IReadOnlyList<FileDescriptor> fileDescriptors) =>
        fileDescriptors.SelectMany(fd => fd.MessageTypes.SelectMany(GetFields));

    private IEnumerable<FieldDescriptor> GetFields(MessageDescriptor messageDescriptor) =>
        messageDescriptor.Fields.InFieldNumberOrder().Concat(messageDescriptor.NestedTypes.SelectMany(GetFields));

    public class Change
    {
        public string FullName { get; set; }
        public string ChangeSha { get; set; }
        public Instant ChangeTimestamp { get; set; }
        public bool? OldRequired { get; set; }
        public bool? NewRequired { get; set; }
    }
}
