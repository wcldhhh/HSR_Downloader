using HSR_DataDownloader;
using System.Text;

namespace HSR_DataDownloader;

public class DesignIndex
{
    public uint Magic;
    public uint Version;
    public int FileCount;
    public int DesignDataCount;
    public List<FileEntry> Files = new();

    public class FileEntry
    {
        public ulong NameHash;
        public string? FileHash;
        public ulong Size => (ulong)Entries.Sum(x => (long)x.Size);
        public ulong ReadSize;
        public uint Count => (uint)Entries.Count;
        public List<DataEntry> Entries = new();
        public string Lang = string.Empty;
        public byte Unk;

        public class DataEntry
        {
            public ulong NameHash;
            public uint Size;
            public uint Offset;
            public static DataEntry Read(EndianBinaryReader br, uint version)
            {
                var entry = new DataEntry();
                entry.NameHash = version >= 4 ? br.ReadUInt64BE() : (ulong)br.ReadUInt32BE();
                entry.Size = br.ReadUInt32BE();
                entry.Offset = br.ReadUInt32BE();
                return entry;
            }
        }

        public static FileEntry Read(EndianBinaryReader br, uint version)
        {
            var entry = new FileEntry();
            entry.NameHash = version >= 4 ? br.ReadUInt64BE() : (ulong)br.ReadUInt32BE();
            entry.FileHash = br.ReadStraightHash();
            entry.ReadSize = br.ReadUInt64BE();
            var cnt = br.ReadUInt32BE();
            for (var i = 0; i < cnt; i++)
                entry.Entries.Add(DataEntry.Read(br, version));
            var offset = 0u;
            foreach (var ientry in entry.Entries)
            {
                if (offset != ientry.Offset)
                    throw new Exception($"Offset mismatch in filehash {entry.FileHash}");
                offset += ientry.Size;
            }
            if (version >= 4)
            {
                var langLen = br.ReadUInt16BE();
                if (langLen > 0)
                {
                    var langBytes = br.ReadBytes(langLen);
                    entry.Lang = Encoding.UTF8.GetString(langBytes);
                }
            }
            entry.Unk = br.ReadByte();
            if (entry.ReadSize != entry.Size)
                throw new Exception($"Size mismatch in filehash {entry.FileHash}: read {entry.ReadSize}, calc {entry.Size}");
            return entry;
        }
    }

    /// <summary>
    /// Find the AllowedLanguage DataEntry (NameHash 0xE148B2BE for v3, 0xB804DBC76D81FB75 for v4)
    /// </summary>
    public (FileEntry.DataEntry dataEntry, FileEntry fileEntry) FindAllowedLanguage()
    {
        ulong target = Version >= 4 ? 0xB804DBC76D81FB75UL : 0xE148B2BEUL;
        return FindDataEntry(target);
    }

    /// <summary>
    /// Find a DataEntry by NameHash across all FileEntries
    /// </summary>
    public (FileEntry.DataEntry dataEntry, FileEntry fileEntry) FindDataEntry(ulong nameHash)
    {
        foreach (var file in Files)
        {
            foreach (var entry in file.Entries)
            {
                if (entry.NameHash == nameHash)
                    return (entry, file);
            }
        }
        throw new Exception($"DataEntry with NameHash 0x{nameHash:X} not found in DesignIndex");
    }

    /// <summary>
    /// Read raw bytes of a DataEntry from the corresponding .bytes file on disk
    /// </summary>
    public static byte[] ReadDataEntryBytes(string directory, FileEntry fileEntry, FileEntry.DataEntry dataEntry)
    {
        var filePath = Path.Combine(directory, $"{fileEntry.FileHash}.bytes");
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"DesignData file not found: {filePath}");
        using var fs = File.OpenRead(filePath);
        fs.Seek(dataEntry.Offset, SeekOrigin.Begin);
        var buffer = new byte[dataEntry.Size];
        fs.ReadExactly(buffer, 0, buffer.Length);
        return buffer;
    }

    /// <summary>
    /// Get all unique NameHashes in the index (useful for finding specific tables)
    /// </summary>
    public Dictionary<ulong, int> GetNameHashSet()
    {
        var dict = new Dictionary<ulong, int>();
        foreach (var file in Files)
        {
            foreach (var entry in file.Entries)
            {
                if (!dict.ContainsKey(entry.NameHash))
                    dict[entry.NameHash] = 0;
                dict[entry.NameHash]++;
            }
        }
        return dict;
    }

    public static DesignIndex Read(byte[] indexBytes)
    {
        using var msi = new MemoryStream(indexBytes);
        using var bri = new EndianBinaryReader(msi, Encoding.UTF8);
        return Read(bri);
    }

    public static DesignIndex Read(EndianBinaryReader br)
    {
        var index = new DesignIndex();
        index.Magic = br.ReadUInt32BE();
        index.Version = br.ReadUInt32BE();
        index.FileCount = br.ReadInt32BE();
        index.DesignDataCount = br.ReadInt32BE();
        if (index.Magic != 0xFF)
            throw new Exception($"Invalid DesignV magic: 0x{index.Magic:X}");
        if (index.Version != 3 && index.Version != 4)
            throw new Exception($"Unsupported DesignV version: {index.Version}");
        for (var i = 0; i < index.FileCount; i++)
            index.Files.Add(FileEntry.Read(br, index.Version));
        return index;
    }
}