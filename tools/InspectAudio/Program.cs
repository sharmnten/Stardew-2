using System.Collections;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework.Audio;

if (args.Length != 2) throw new ArgumentException("Usage: InspectAudio <original-content-root> <report.json>");
string originalRoot = Path.GetFullPath(args[0]);
string root = "OriginalAudio";
Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, root, "XACT"));
foreach (string name in new[] { "FarmerSounds.xgs", "Sound Bank.xsb" })
    File.Copy(Path.Combine(originalRoot, "XACT", name), Path.Combine(AppContext.BaseDirectory, root, "XACT", name), overwrite: true);
// Parsing metadata does not create a wave bank, play a cue, or initialize hardware.
var engine = new AudioEngine(Path.Combine(root, "XACT", "FarmerSounds.xgs"));
var bank = new SoundBank(engine, Path.Combine(root, "XACT", "Sound Bank.xsb"));
var definitions = (Dictionary<string, CueDefinition>)Field(bank, "_cues")!;
var waveBankNames = (string[])Field(bank, "_waveBankNames")!;
var cues = definitions.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => Snapshot(pair.Value));
var report = new {
    gameVersion = "1.6.15.24356", parserAssembly = typeof(AudioEngine).Assembly.FullName,
    waveBankNames, cueCount = cues.Count, cues,
    categories = Snapshot(Field(engine, "_categories")),
    globalVariables = Snapshot(Field(engine, "_variables")),
    cueVariables = Snapshot(Field(engine, "_cueVariables")),
    rpcCurves = Snapshot(Field(engine, "RpcCurves")),
    reverbCurves = Snapshot(Field(engine, "_reverbCurves")),
    reverbSettings = Snapshot(Field(engine, "_reverbSettings"))
};
File.WriteAllText(args[1], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine($"Original parser read {cues.Count} cues in {waveBankNames.Length} wave banks; no playback initialized.");

static object? Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value);

static object? Snapshot(object? value, int depth = 0)
{
    if (value == null) return null;
    Type type = value.GetType();
    if (type.IsPrimitive || value is string or decimal) return value;
    if (type.IsEnum) return new { type = type.Name, value = Convert.ToInt32(value), name = value.ToString() };
    if (value is Delegate || value is AudioEngine or SoundBank or SoundEffect or SoundEffectInstance) return new { reference = type.Name };
    if (depth > 16) throw new InvalidDataException("Unexpected recursive XACT metadata: " + type.FullName);
    if (value is IEnumerable sequence) return sequence.Cast<object?>().Select(item => Snapshot(item, depth + 1)).ToArray();
    var fields = new SortedDictionary<string, object?>();
    for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (field.Name is "_clip" or "_engine" || typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
            fields[field.Name] = Snapshot(field.GetValue(value), depth + 1);
        }
    return new { type = type.Name, fields };
}
