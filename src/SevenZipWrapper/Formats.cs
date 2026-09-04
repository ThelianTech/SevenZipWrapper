
namespace SevenZipWrapper;

using System.Collections.Frozen;

/// <summary>
/// Provides read-only mappings between file extensions, archive format GUIDs, and file signatures.
/// </summary>
public static class Formats
{
    /// <summary>
    /// Maps lowercase file extensions (without leading dot) to their <see cref="SevenZipFormat"/>.
    /// </summary>
    internal static readonly FrozenDictionary<string, SevenZipFormat> ExtensionFormatMapping =
        new Dictionary<string, SevenZipFormat>(StringComparer.OrdinalIgnoreCase)
        {
            ["7z"] = SevenZipFormat.SevenZip,
            ["gz"] = SevenZipFormat.GZip,
            ["tar"] = SevenZipFormat.Tar,
            ["rar"] = SevenZipFormat.Rar,
            ["zip"] = SevenZipFormat.Zip,
            ["lzma"] = SevenZipFormat.Lzma,
            ["lzh"] = SevenZipFormat.Lzh,
            ["arj"] = SevenZipFormat.Arj,
            ["bz2"] = SevenZipFormat.BZip2,
            ["cab"] = SevenZipFormat.Cab,
            ["chm"] = SevenZipFormat.Chm,
            ["deb"] = SevenZipFormat.Deb,
            ["iso"] = SevenZipFormat.Iso,
            ["rpm"] = SevenZipFormat.Rpm,
            ["wim"] = SevenZipFormat.Wim,
            ["udf"] = SevenZipFormat.Udf,
            ["mub"] = SevenZipFormat.Mub,
            ["xar"] = SevenZipFormat.Xar,
            ["hfs"] = SevenZipFormat.Hfs,
            ["dmg"] = SevenZipFormat.Dmg,
            ["z"] = SevenZipFormat.Lzw,
            ["xz"] = SevenZipFormat.XZ,
            ["flv"] = SevenZipFormat.Flv,
            ["swf"] = SevenZipFormat.Swf,
            ["exe"] = SevenZipFormat.PE,
            ["dll"] = SevenZipFormat.PE,
            ["vhd"] = SevenZipFormat.Vhd,
            ["zst"] = SevenZipFormat.Zstd
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps each <see cref="SevenZipFormat"/> to its 7z COM class GUID.
    /// </summary>
    internal static readonly FrozenDictionary<SevenZipFormat, Guid> FormatGuidMapping =
        new Dictionary<SevenZipFormat, Guid>
        {
            [SevenZipFormat.SevenZip] = new("23170f69-40c1-278a-1000-000110070000"),
            [SevenZipFormat.Arj] = new("23170f69-40c1-278a-1000-000110040000"),
            [SevenZipFormat.BZip2] = new("23170f69-40c1-278a-1000-000110020000"),
            [SevenZipFormat.Cab] = new("23170f69-40c1-278a-1000-000110080000"),
            [SevenZipFormat.Chm] = new("23170f69-40c1-278a-1000-000110e90000"),
            [SevenZipFormat.Compound] = new("23170f69-40c1-278a-1000-000110e50000"),
            [SevenZipFormat.Cpio] = new("23170f69-40c1-278a-1000-000110ed0000"),
            [SevenZipFormat.Deb] = new("23170f69-40c1-278a-1000-000110ec0000"),
            [SevenZipFormat.GZip] = new("23170f69-40c1-278a-1000-000110ef0000"),
            [SevenZipFormat.Iso] = new("23170f69-40c1-278a-1000-000110e70000"),
            [SevenZipFormat.Lzh] = new("23170f69-40c1-278a-1000-000110060000"),
            [SevenZipFormat.Lzma] = new("23170f69-40c1-278a-1000-0001100a0000"),
            [SevenZipFormat.Nsis] = new("23170f69-40c1-278a-1000-000110090000"),
            [SevenZipFormat.Rar] = new("23170f69-40c1-278a-1000-000110030000"),
            [SevenZipFormat.Rar5] = new("23170f69-40c1-278a-1000-000110cc0000"),
            [SevenZipFormat.Rpm] = new("23170f69-40c1-278a-1000-000110eb0000"),
            [SevenZipFormat.Split] = new("23170f69-40c1-278a-1000-000110ea0000"),
            [SevenZipFormat.Tar] = new("23170f69-40c1-278a-1000-000110ee0000"),
            [SevenZipFormat.Wim] = new("23170f69-40c1-278a-1000-000110e60000"),
            [SevenZipFormat.Lzw] = new("23170f69-40c1-278a-1000-000110050000"),
            [SevenZipFormat.Zip] = new("23170f69-40c1-278a-1000-000110010000"),
            [SevenZipFormat.Udf] = new("23170f69-40c1-278a-1000-000110e00000"),
            [SevenZipFormat.Xar] = new("23170f69-40c1-278a-1000-000110e10000"),
            [SevenZipFormat.Mub] = new("23170f69-40c1-278a-1000-000110e20000"),
            [SevenZipFormat.Hfs] = new("23170f69-40c1-278a-1000-000110e30000"),
            [SevenZipFormat.Dmg] = new("23170f69-40c1-278a-1000-000110e40000"),
            [SevenZipFormat.XZ] = new("23170f69-40c1-278a-1000-0001100c0000"),
            [SevenZipFormat.Mslz] = new("23170f69-40c1-278a-1000-000110d50000"),
            [SevenZipFormat.PE] = new("23170f69-40c1-278a-1000-000110dd0000"),
            [SevenZipFormat.Elf] = new("23170f69-40c1-278a-1000-000110de0000"),
            [SevenZipFormat.Swf] = new("23170f69-40c1-278a-1000-000110d70000"),
            [SevenZipFormat.Vhd] = new("23170f69-40c1-278a-1000-000110dc0000"),
            [SevenZipFormat.Flv] = new("23170f69-40c1-278a-1000-000110d60000"),
            [SevenZipFormat.SquashFS] = new("23170f69-40c1-278a-1000-000110d20000"),
            [SevenZipFormat.Lzma86] = new("23170f69-40c1-278a-1000-0001100b0000"),
            [SevenZipFormat.Ppmd] = new("23170f69-40c1-278a-1000-0001100d0000"),
            [SevenZipFormat.TE] = new("23170f69-40c1-278a-1000-000110cf0000"),
            [SevenZipFormat.UEFIc] = new("23170f69-40c1-278a-1000-000110d00000"),
            [SevenZipFormat.UEFIs] = new("23170f69-40c1-278a-1000-000110d10000"),
            [SevenZipFormat.CramFS] = new("23170f69-40c1-278a-1000-000110d30000"),
            [SevenZipFormat.APM] = new("23170f69-40c1-278a-1000-000110d40000"),
            [SevenZipFormat.Swfc] = new("23170f69-40c1-278a-1000-000110d80000"),
            [SevenZipFormat.Ntfs] = new("23170f69-40c1-278a-1000-000110d90000"),
            [SevenZipFormat.Fat] = new("23170f69-40c1-278a-1000-000110da0000"),
            [SevenZipFormat.Mbr] = new("23170f69-40c1-278a-1000-000110db0000"),
            [SevenZipFormat.MachO] = new("23170f69-40c1-278a-1000-000110df0000"),
            [SevenZipFormat.Zstd] = new("23170f69-40c1-278a-1000-0001100e0000")
        }.ToFrozenDictionary();

    /// <summary>
    /// Maps each <see cref="SevenZipFormat"/> to its leading file signature (magic bytes),
    /// ordered from longest to shortest for correct matching precedence.
    /// </summary>
    internal static readonly FrozenDictionary<SevenZipFormat, byte[]> FileSignatures =
        new Dictionary<SevenZipFormat, byte[]>
        {
            [SevenZipFormat.Rar5] = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00],
            [SevenZipFormat.Rar] = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00],
            [SevenZipFormat.Vhd] = [0x63, 0x6F, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x78],
            [SevenZipFormat.Deb] = [0x21, 0x3C, 0x61, 0x72, 0x63, 0x68, 0x3E],
            [SevenZipFormat.Dmg] = [0x78, 0x01, 0x73, 0x0D, 0x62, 0x62, 0x60],
            [SevenZipFormat.SevenZip] = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C],
            [SevenZipFormat.Tar] = [0x75, 0x73, 0x74, 0x61, 0x72],
            [SevenZipFormat.Iso] = [0x43, 0x44, 0x30, 0x30, 0x31],
            [SevenZipFormat.Zstd] = [0x28, 0xB5, 0x2F, 0xFD],
            [SevenZipFormat.Cab] = [0x4D, 0x53, 0x43, 0x46],
            [SevenZipFormat.Rpm] = [0xED, 0xAB, 0xEE, 0xDB],
            [SevenZipFormat.Xar] = [0x78, 0x61, 0x72, 0x21],
            [SevenZipFormat.Chm] = [0x49, 0x54, 0x53, 0x46],
            [SevenZipFormat.SquashFS] = [0x68, 0x73, 0x71, 0x73],
            [SevenZipFormat.BZip2] = [0x42, 0x5A, 0x68],
            [SevenZipFormat.Flv] = [0x46, 0x4C, 0x56],
            [SevenZipFormat.Swf] = [0x46, 0x57, 0x53],
            [SevenZipFormat.Lzh] = [0x2D, 0x6C, 0x68],
            [SevenZipFormat.GZip] = [0x1F, 0x8B],
            [SevenZipFormat.Zip] = [0x50, 0x4B],
            [SevenZipFormat.Arj] = [0x60, 0xEA]
        }.ToFrozenDictionary();

    /// <summary>
    /// The length of the longest file signature in <see cref="FileSignatures"/>.
    /// </summary>
    internal static readonly int MaxSignatureLength = FileSignatures.Values.Max(v => v.Length);
}