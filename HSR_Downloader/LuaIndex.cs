using HSR_DataDownloader;
using System.Text;

namespace HSR_DataDownloader;

/// <summary>
/// Unified LuaIndex that supports both Beta (v3/v4 with 0xFF magic) and Rel (legacy) formats.
/// </summary>
public class LuaIndex
{
    // Beta fields
    public uint Magic;
    public uint Version;

    // Rel fields
    public long Unk1;

    // Common fields
    public int FileCount;
    public int DesignDataCount; // Beta: DesignDataCount, Rel: Unk2
    public List<FileEntry> Files = new();

    /// <summary>Which format was detected during reading.</summary>
    public ServerMode DetectedMode { get; private set; }

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

            public static DataEntry Read(EndianBinaryReader br, ServerMode mode, uint version)
            {
                var entry = new DataEntry();
                if (mode == ServerMode.Beta && version >= 4)
                    entry.NameHash = br.ReadUInt64BE();
                else
                    entry.NameHash = (ulong)br.ReadUInt32BE();
                entry.Size = br.ReadUInt32BE();
                entry.Offset = br.ReadUInt32BE();
                return entry;
            }
        }

        public static FileEntry Read(EndianBinaryReader br, ServerMode mode, uint version)
        {
            var entry = new FileEntry();

            if (mode == ServerMode.Beta && version >= 4)
                entry.NameHash = br.ReadUInt64BE();
            else
                entry.NameHash = (ulong)br.ReadUInt32BE();

            entry.FileHash = br.ReadStraightHash();
            entry.ReadSize = br.ReadUInt64BE();
            var cnt = br.ReadUInt32BE();

            for (var i = 0; i < cnt; i++)
                entry.Entries.Add(DataEntry.Read(br, mode, version));

            var offset = 0u;
            foreach (var ientry in entry.Entries)
            {
                if (offset != ientry.Offset)
                    throw new Exception($"Offset mismatch in filehash {entry.FileHash}");
                offset += ientry.Size;
            }

            // Language tag: Beta v4+ and Rel both have length-prefixed lang string
            if (mode == ServerMode.Beta && version >= 4)
            {
                var langLen = br.ReadUInt16BE();
                if (langLen > 0)
                {
                    var langBytes = br.ReadBytes(langLen);
                    entry.Lang = Encoding.UTF8.GetString(langBytes);
                }
            }
            else if (mode == ServerMode.Rel)
            {
                var langLen = br.ReadUInt16BE();
                if (langLen > 0)
                {
                    var langBuf = br.ReadBytes(langLen);
                    entry.Lang = Encoding.UTF8.GetString(langBuf);
                }
            }
            // Beta v3: no language tag

            entry.Unk = br.ReadByte();

            if (entry.ReadSize != entry.Size)
                throw new Exception($"Size mismatch in filehash {entry.FileHash}: read {entry.ReadSize}, calc {entry.Size} (diff {entry.ReadSize - entry.Size})");
            return entry;
        }
    }

    public static LuaIndex Read(byte[] indexBytes)
    {
        using var msi = new MemoryStream(indexBytes);
        using var bri = new EndianBinaryReader(msi, Encoding.UTF8);
        return Read(bri);
    }

    public static LuaIndex Read(EndianBinaryReader br)
    {
        var index = new LuaIndex();

        // Save position to detect format
        var startPos = br.BaseStream.Position;

        // Try reading as Beta format (starts with Magic + Version)
        var firstUint = br.ReadUInt32BE();
        var secondUint = br.ReadUInt32BE();

        if (firstUint == 0xFF && (secondUint == 3 || secondUint == 4))
        {
            // Beta format: Magic(0xFF) + Version(3 or 4)
            index.DetectedMode = ServerMode.Beta;
            index.Magic = firstUint;
            index.Version = secondUint;
            index.FileCount = br.ReadInt32BE();
            index.DesignDataCount = br.ReadInt32BE();
        }
        else
        {
            // Rel format: Unk1(8 bytes) + FileCount(4 BE) + Unk2(4 BE)
            br.BaseStream.Position = startPos;
            index.DetectedMode = ServerMode.Rel;
            index.Unk1 = br.ReadInt64();
            index.FileCount = br.ReadInt32BE();
            index.DesignDataCount = br.ReadInt32BE();
        }

        for (var i = 0; i < index.FileCount; i++)
            index.Files.Add(FileEntry.Read(br, index.DetectedMode, index.Version));

        return index;
    }

    public void RecalcSizeOffsets()
    {
        foreach (var file in Files)
        {
            var offset = 0u;
            foreach (var entry in file.Entries)
            {
                entry.Offset = offset;
                offset += entry.Size;
            }
        }
    }
}