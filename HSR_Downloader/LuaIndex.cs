using HSR_DataDownloader;
using System.Text;

namespace HSR_DataDownloader;

public class LuaIndex
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

            // Language tag (v4+): length-prefixed string (uint16 BE length + UTF-8 bytes)
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

        // Header: Magic(4) + Version(4) + FileCount(4) + DesignDataCount(4)
        index.Magic = br.ReadUInt32BE();
        index.Version = br.ReadUInt32BE();
        index.FileCount = br.ReadInt32BE();
        index.DesignDataCount = br.ReadInt32BE();

        if (index.Magic != 0xFF)
            throw new Exception($"Invalid LuaV magic: 0x{index.Magic:X} (expected 0xFF)");
        if (index.Version != 3 && index.Version != 4)
            throw new Exception($"Unsupported LuaV version: {index.Version} (supported: 3, 4)");

        for (var i = 0; i < index.FileCount; i++)
            index.Files.Add(FileEntry.Read(br, index.Version));

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