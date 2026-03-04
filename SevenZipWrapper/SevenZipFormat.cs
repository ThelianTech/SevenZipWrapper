namespace SevenZipWrapper;

/// <summary>
/// Identifies a supported 7-Zip archive format.
/// </summary>
public enum SevenZipFormat
{
    /// <summary>Default invalid format value.</summary>
    Undefined = 0,

    /// <summary>Open 7-Zip archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/7-Zip">Wikipedia</a></remarks>
    SevenZip,

    /// <summary>Proprietary ARJ archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/ARJ">Wikipedia</a></remarks>
    Arj,

    /// <summary>Open BZip2 archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Bzip2">Wikipedia</a></remarks>
    BZip2,

    /// <summary>Microsoft Cabinet archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Cabinet_(file_format)">Wikipedia</a></remarks>
    Cab,

    /// <summary>Microsoft Compiled HTML Help file format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Microsoft_Compiled_HTML_Help">Wikipedia</a></remarks>
    Chm,

    /// <summary>Microsoft Compound Binary file format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Compound_File_Binary_Format">Wikipedia</a></remarks>
    Compound,

    /// <summary>Open CPIO archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Cpio">Wikipedia</a></remarks>
    Cpio,

    /// <summary>Open Debian software package format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Deb_(file_format)">Wikipedia</a></remarks>
    Deb,

    /// <summary>Open GZip archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Gzip">Wikipedia</a></remarks>
    GZip,

    /// <summary>Open ISO disk image format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/ISO_image">Wikipedia</a></remarks>
    Iso,

    /// <summary>Open LZH archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/LHA_(file_format)">Wikipedia</a></remarks>
    Lzh,

    /// <summary>Open LZMA raw archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Markov_chain_algorithm">Wikipedia</a></remarks>
    Lzma,

    /// <summary>Nullsoft Scriptable Install System package format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/NSIS">Wikipedia</a></remarks>
    Nsis,

    /// <summary>RAR archive format (pre-RAR5).</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/RAR_(file_format)">Wikipedia</a></remarks>
    Rar,

    /// <summary>RAR archive format, version 5.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/RAR_(file_format)">Wikipedia</a></remarks>
    Rar5,

    /// <summary>Open RPM software package format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/RPM_Package_Manager">Wikipedia</a></remarks>
    Rpm,

    /// <summary>Open split file format.</summary>
    Split,

    /// <summary>Open TAR archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Tar_(computing)">Wikipedia</a></remarks>
    Tar,

    /// <summary>Microsoft Windows Imaging disk image format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Windows_Imaging_Format">Wikipedia</a></remarks>
    Wim,

    /// <summary>Open LZW archive format (compress / Z).</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Compress_(software)">Wikipedia</a></remarks>
    Lzw,

    /// <summary>Open ZIP archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/ZIP_(file_format)">Wikipedia</a></remarks>
    Zip,

    /// <summary>Open UDF disk image format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Universal_Disk_Format">Wikipedia</a></remarks>
    Udf,

    /// <summary>XAR open-source archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Xar_(archiver)">Wikipedia</a></remarks>
    Xar,

    /// <summary>Mach-O Universal Binary (fat binary) container.</summary>
    Mub,

    /// <summary>Apple HFS/HFS+ disk image format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/HFS_Plus">Wikipedia</a></remarks>
    Hfs,

    /// <summary>Apple Disk Image format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Apple_Disk_Image">Wikipedia</a></remarks>
    Dmg,

    /// <summary>Open XZ archive format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Xz">Wikipedia</a></remarks>
    XZ,

    /// <summary>MSLZ compressed file format.</summary>
    Mslz,

    /// <summary>Flash Video container format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Flash_Video">Wikipedia</a></remarks>
    Flv,

    /// <summary>Shockwave Flash format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/SWF">Wikipedia</a></remarks>
    Swf,

    /// <summary>Windows Portable Executable format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Portable_Executable">Wikipedia</a></remarks>
    PE,

    /// <summary>Linux ELF executable format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Executable_and_Linkable_Format">Wikipedia</a></remarks>
    Elf,

    /// <summary>Windows Installer Database format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Windows_Installer">Wikipedia</a></remarks>
    Msi,

    /// <summary>Microsoft Virtual Hard Disk format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/VHD_(file_format)">Wikipedia</a></remarks>
    Vhd,

    /// <summary>SquashFS compressed read-only file system.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/SquashFS">Wikipedia</a></remarks>
    SquashFS,

    /// <summary>LZMA86 archive format.</summary>
    Lzma86,

    /// <summary>Prediction by Partial Matching compression format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Prediction_by_partial_matching">Wikipedia</a></remarks>
    Ppmd,

    /// <summary>TE (Terse Executable) format.</summary>
    TE,

    /// <summary>UEFI compressed firmware volume format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/UEFI">Wikipedia</a></remarks>
    UEFIc,

    /// <summary>UEFI system firmware volume format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/UEFI">Wikipedia</a></remarks>
    UEFIs,

    /// <summary>Compressed ROM file system format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Cramfs">Wikipedia</a></remarks>
    CramFS,

    /// <summary>Apple Partition Map format.</summary>
    APM,

    /// <summary>Compressed Shockwave Flash format.</summary>
    Swfc,

    /// <summary>NTFS file system format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/NTFS">Wikipedia</a></remarks>
    Ntfs,

    /// <summary>FAT file system format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/File_Allocation_Table">Wikipedia</a></remarks>
    Fat,

    /// <summary>Master Boot Record format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Master_boot_record">Wikipedia</a></remarks>
    Mbr,

    /// <summary>Mach-O executable format.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Mach-O">Wikipedia</a></remarks>
    MachO,

    /// <summary>Zstandard compression format. Supported since 7z 23.01+.</summary>
    /// <remarks><a href="https://en.wikipedia.org/wiki/Zstd">Wikipedia</a></remarks>
    Zstd
}