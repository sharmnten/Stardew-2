using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using StardewBrowser.Platform;
using StardewBrowser.Platform.Storage;
using StardewValley;
using StardewValley.SaveSerialization;

namespace StardewBrowser.GameStorage;

public static class BrowserSaveBridge
{
    public static Task DeleteAsync(string slot) => BrowserPersistence.Current.DeleteAsync(slot);

    public static XmlReader OpenXmlReader(byte[] bytes)
    {
        Stream source = new MemoryStream(bytes, writable: false);
        // Same format detection and decoder as original SaveGame.TryReadSaveFile/FindSaveGames.
        if (bytes.Length > 0 && bytes[0] == 120)
            source = new Ionic.Zlib.ZlibStream(source, Ionic.Zlib.CompressionMode.Decompress);
        return XmlReader.Create(source, new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = true });
    }
    public static async Task SaveAsync(IEnumerator<int> original)
    {
        await CooperativeOperation.Run(original, stopAt: 100);
        string slot = SaveGame.FilterFileName(Game1.GetSaveGameName()) + "_" + Game1.uniqueIDForThisGame;
        string directory = Path.Combine(BrowserSaveStore.Root, "Saves", slot);
        var files = new Dictionary<string, byte[]>();
        foreach (string name in new[] { slot, "SaveGameInfo", slot + "_old", "SaveGameInfo_old" })
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path)) files.Add(name, File.ReadAllBytes(path));
        }
        await BrowserPersistence.Current.PersistAsync(new(slot, files));
    }

    public static void ValidateImport(IReadOnlyDictionary<string, byte[]> files)
    {
        string slot = files.Keys.Single(name => name != "SaveGameInfo" && !name.EndsWith("_old", StringComparison.Ordinal));
        T Deserialize<T>(byte[] bytes)
        {
            using var reader = OpenXmlReader(bytes);
            return SaveSerializer.Deserialize<T>(reader);
        }
        var save = Deserialize<SaveGame>(files[slot]);
        var farmer = Deserialize<Farmer>(files["SaveGameInfo"]);
        if (save?.player == null || farmer == null || save.uniqueIDForThisGame == 0
            || save.player.Name != farmer.Name || save.player.UniqueMultiplayerID != farmer.UniqueMultiplayerID
            || !slot.EndsWith("_" + save.uniqueIDForThisGame, StringComparison.Ordinal))
            throw new InvalidDataException("The farm save and SaveGameInfo do not describe the same farmer.");
        if (Utility.CompareGameVersions(Game1.version, farmer.gameVersion, ignore_platform_specific: true) < 0)
            throw new InvalidDataException("This save needs a newer version of Stardew Valley.");
        // Preserve old-version bytes. The original Load path performs all migrations after import.
    }
}
