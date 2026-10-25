using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Fifawc10Launcher;

public enum SourceKind { None, Xbox360Iso, Xbox360Folder, Ps3Iso, Unknown }

public sealed record GameCheck(SourceKind Kind, bool Playable, string Message);

/// <summary>
/// Finds out what a game copy is (Xbox 360 or PS3, disc image or folder) and whether
/// it is the exact version this build was recompiled from.
/// </summary>
public static class GameSource
{
    // The recompiled code matches these files byte for byte.
    static readonly Dictionary<string, string> KnownHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default.xex"] = "D0DF5465E077EAEBEADDA252C786D9322551AB13EF23AAD461836E510971337D",
        ["fifadllzf.xex"] = "462676F9FBAB83530C2659E79AA43E3DBE81B43DC917DB9299933C19FA34905D",
    };

    const int Sector = 2048;
    static readonly byte[] XdvdfsMagic = Encoding.ASCII.GetBytes("MICROSOFT*XBOX*MEDIA");
    // Game partition offsets: plain XDVDFS, XGD3, XGD2, XGD1.
    static readonly long[] PartitionOffsets = { 0, 0x2080000, 0xFD90000, 0x18300000 };

    public static GameCheck Check(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return new(SourceKind.None, false, "Choose your copy of the game.");
        try
        {
            if (Directory.Exists(path)) return CheckFolder(path);
            if (File.Exists(path)) return CheckImage(path);
            return new(SourceKind.None, false, "File or folder not found.");
        }
        catch (Exception e)
        {
            return new(SourceKind.Unknown, false, $"Could not read it: {e.Message}");
        }
    }

    static GameCheck CheckFolder(string dir)
    {
        foreach (var name in KnownHashes.Keys)
        {
            var file = Path.Combine(dir, name);
            if (!File.Exists(file))
                return new(SourceKind.Unknown, false, $"{name} is missing. Pick the folder that holds default.xex.");
            using var s = File.OpenRead(file);
            if (!HashMatches(name, s))
                return new(SourceKind.Xbox360Folder, false, WrongVersion);
        }
        return new(SourceKind.Xbox360Folder, true, "Xbox 360 game folder - verified.");
    }

    static GameCheck CheckImage(string file)
    {
        using var f = File.OpenRead(file);
        if (FindPartition(f) is { } part)
        {
            foreach (var name in KnownHashes.Keys)
            {
                var entry = FindEntry(f, part, name);
                if (entry is null)
                    return new(SourceKind.Xbox360Iso, false, "This Xbox 360 disc is not 2010 FIFA World Cup South Africa.");
                using var s = new SubStream(f, part.Base + (long)entry.Value.Sector * Sector, entry.Value.Size);
                if (!HashMatches(name, s))
                    return new(SourceKind.Xbox360Iso, false, WrongVersion);
            }
            return new(SourceKind.Xbox360Iso, true, "Xbox 360 disc image - verified.");
        }
        var s1 = new byte[16];
        f.Seek(Sector, SeekOrigin.Begin);
        f.ReadExactly(s1);
        if (Encoding.ASCII.GetString(s1).StartsWith("PlayStation3"))
            return new(SourceKind.Ps3Iso, false,
                "This is the PS3 version. This release runs the Xbox 360 version; PS3 support is planned.");
        return new(SourceKind.Unknown, false, "Not an Xbox 360 or PS3 disc image.");
    }

    const string WrongVersion =
        "This copy is a different version or region than the one this port was built from. " +
        "Use the Europe Xbox 360 disc (En, Es, Nl).";

    static bool HashMatches(string name, Stream s) =>
        Convert.ToHexString(SHA256.HashData(s)).Equals(KnownHashes[name], StringComparison.OrdinalIgnoreCase);

    record struct Partition(long Base, uint RootSector, uint RootSize);
    record struct Entry(uint Sector, uint Size);

    static Partition? FindPartition(FileStream f)
    {
        var buf = new byte[28];
        foreach (var b in PartitionOffsets)
        {
            if (b + 32 * Sector + 28 > f.Length) continue;
            f.Seek(b + 32 * Sector, SeekOrigin.Begin);
            f.ReadExactly(buf);
            if (buf.AsSpan(0, 20).SequenceEqual(XdvdfsMagic))
                return new Partition(b, BitConverter.ToUInt32(buf, 20), BitConverter.ToUInt32(buf, 24));
        }
        return null;
    }

    // Looks a file up in the root directory (a binary tree of entries).
    static Entry? FindEntry(FileStream f, Partition p, string name)
    {
        var dir = new byte[p.RootSize];
        f.Seek(p.Base + (long)p.RootSector * Sector, SeekOrigin.Begin);
        f.ReadExactly(dir);
        var stack = new Stack<int>();
        var seen = new HashSet<int>();
        stack.Push(0);
        while (stack.Count > 0)
        {
            int off = stack.Pop();
            if (!seen.Add(off) || off + 14 > dir.Length) continue;
            ushort left = BitConverter.ToUInt16(dir, off), right = BitConverter.ToUInt16(dir, off + 2);
            if (left == 0xFFFF && right == 0xFFFF) continue;
            uint start = BitConverter.ToUInt32(dir, off + 4), size = BitConverter.ToUInt32(dir, off + 8);
            int len = dir[off + 13];
            if (off + 14 + len > dir.Length) continue;
            var entryName = Encoding.ASCII.GetString(dir, off + 14, len);
            if (entryName.Equals(name, StringComparison.OrdinalIgnoreCase)) return new Entry(start, size);
            if (left != 0) stack.Push(left * 4);
            if (right != 0) stack.Push(right * 4);
        }
        return null;
    }

    /// <summary>A read-only window onto part of another stream.</summary>
    sealed class SubStream(Stream inner, long start, long length) : Stream
    {
        long pos;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => pos; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = (int)Math.Min(count, length - pos);
            if (count <= 0) return 0;
            inner.Seek(start + pos, SeekOrigin.Begin);
            int n = inner.Read(buffer, offset, count);
            pos += n;
            return n;
        }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }
}
