using System.Text;

namespace HSR_DataDownloader;

public class EndianBinaryReader : BinaryReader
{
    public EndianBinaryReader(Stream input) : base(input)
    {
    }

    public EndianBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
    {
    }

    public EndianBinaryReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
    {
    }

    public ushort ReadUInt16BE() => (ushort)SwapBytes16(base.ReadUInt16());

    public uint ReadUInt32BE() => SwapBytes(base.ReadUInt32());

    public ulong ReadUInt64BE() => SwapBytes(base.ReadUInt64());

    public short ReadInt16BE() => (short)SwapBytes16(base.ReadUInt16());

    public int ReadInt32BE() => (int)SwapBytes(base.ReadUInt32());

    public long ReadInt64BE() => (long)SwapBytes(base.ReadUInt64());

    public List<bool> ReadExcelBitfield()
    {
        var res = new List<bool>();
        var curByte = (byte)0;
        do
        {
            curByte = base.ReadByte();
            var proc = curByte & 0x7f;
            for (int i = 0; i < 7; i++)
            {
                res.Add((proc & 0x1) != 0);
                proc >>= 1;
            }
        } while ((curByte & 0x80) != 0);

        return res;
    }

    public ulong ReadVarInt()
    {
        ulong res = 0;
        int shift = 0;
        ulong read = base.ReadByte();
        while ((read & 0x80) == 0x80)
        {
            ulong tmp = read & 0x7f;
            res |= tmp << shift;
            read = base.ReadByte();
            shift += 7;
        }
        ulong tmp2 = read & 0x7f;
        res |= tmp2 << shift;
        return res;
    }

    public long ReadSignedVarInt()
    {
        var pre = ReadVarInt();
        return DecodeZigZag(pre);
    }

    private static long DecodeZigZag(ulong value)
    {
        if ((value & 0x1) == 0x1)
            return -1 * ((long)(value >> 1) + 1);
        return (long)(value >> 1);
    }

    /// <summary>
    /// Read 16 bytes as a hash: reads 4 x uint32 LE, reverses each chunk's bytes, returns lowercase hex.
    /// Used for M_DesignV/M_LuaV IndexHash parsing.
    /// </summary>
    public string ReadHash()
    {
        var fullHash = new byte[16];
        for (
            int i = 0, k = 0;
            i < 4;
            i++)
        {
            var chunk = base.ReadBytes(4);
            for (
                int j = 3;
                j >= 0;
                j--, k++)
            {
                fullHash[k] = chunk[j];
            }
        }
        return Convert.ToHexString(fullHash).ToLower();
    }

    /// <summary>
    /// Read 16 bytes directly as a hash (no byte reversal). Returns lowercase hex.
    /// Used for DesignIndex/LuaIndex FileHash parsing.
    /// </summary>
    public string ReadStraightHash()
    {
        var fullHash = base.ReadBytes(16);
        return Convert.ToHexString(fullHash).ToLower();
    }

    private uint SwapBytes16(uint x)
    {
        return ((x & 0xFF00) >> 8) | ((x & 0x00FF) << 8);
    }

    private uint SwapBytes(uint x)
    {
        // swap adjacent 16-bit blocks
        x = (x >> 16) | (x << 16);
        // swap adjacent 8-bit blocks
        return ((x & 0xFF00FF00) >> 8) | ((x & 0x00FF00FF) << 8);
    }

    private ulong SwapBytes(ulong x)
    {
        // swap adjacent 32-bit blocks
        x = (x >> 32) | (x << 32);
        // swap adjacent 16-bit blocks
        x = ((x & 0xFFFF0000FFFF0000) >> 16) | ((x & 0x0000FFFF0000FFFF) << 16);
        // swap adjacent 8-bit blocks
        return ((x & 0xFF00FF00FF00FF00) >> 8) | ((x & 0x00FF00FF00FF00FF) << 8);
    }
}