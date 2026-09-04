# Compatibility and formats

The supported package boundary is Windows x64 with .NET 10. The bundled engine is 7-Zip 26.02. Loading rejects other process architectures and uses an explicit library path or deterministic application-local candidates; it does not search an unrelated system installation.

Explicit format selection wins. Otherwise lightweight leading signatures win over extensions. Extension fallback applies only to path inputs; RAR requires a reliable signature to distinguish RAR generations. Stream inputs without a recognized signature require an explicit format. Signature recognition alone is not proof that the complete archive is valid.

The table separates mapped engine capability from integration verification. A mapped format can be passed explicitly, but unverified formats have no promise of equivalent wrapper-level coverage. Five representative format families are verified with bundled fixtures and the pinned native DLL.

| Format | Explicit mapping | Extension fallback | Leading signature | Integration fixture |
|---|---|---|---|---|
| SevenZip | Yes | .7z | Yes | Yes |
| Arj | Yes | .arj | Yes | Yes |
| BZip2 | Yes | .bz2 | Yes | No |
| Cab | Yes | .cab | Yes | No |
| Chm | Yes | .chm | Yes | No |
| Compound | Yes | — | No | No |
| Cpio | Yes | — | No | No |
| Deb | Yes | .deb | Yes | No |
| GZip | Yes | .gz | Yes | No |
| Iso | Yes | .iso | No | No |
| Lzh | Yes | .lzh | No | Yes |
| Lzma | Yes | .lzma | No | No |
| Nsis | Yes | — | No | No |
| Rar | Yes | Requires signature | Yes | Yes |
| Rar5 | Yes | Requires signature | Yes | No |
| Rpm | Yes | .rpm | Yes | No |
| Split | Yes | — | No | No |
| Tar | Yes | .tar | No | No |
| Wim | Yes | .wim | No | No |
| Lzw | Yes | .z | No | No |
| Zip | Yes | .zip | Yes | Yes |
| Udf | Yes | .udf | No | No |
| Xar | Yes | .xar | Yes | No |
| Mub | Yes | .mub | No | No |
| Hfs | Yes | .hfs | No | No |
| Dmg | Yes | .dmg | No | No |
| XZ | Yes | .xz | No | No |
| Mslz | Yes | — | No | No |
| PE | Yes | .exe, .dll | No | No |
| Elf | Yes | — | No | No |
| Swf | Yes | .swf | Yes | No |
| Vhd | Yes | .vhd | No | No |
| Flv | Yes | .flv | Yes | No |
| SquashFS | Yes | — | Yes | No |
| Lzma86 | Yes | — | No | No |
| Ppmd | Yes | — | No | No |
| TE | Yes | — | No | No |
| UEFIc | Yes | — | No | No |
| UEFIs | Yes | — | No | No |
| CramFS | Yes | — | No | No |
| APM | Yes | — | No | No |
| Swfc | Yes | — | No | No |
| Ntfs | Yes | — | No | No |
| Fat | Yes | — | No | No |
| Mbr | Yes | — | No | No |
| MachO | Yes | — | No | No |
| Zstd | Yes | .zst | Yes | No |
| Undefined, Msi, invalid enum values | No | No | No | Controlled rejection |

ZIP also recognizes the empty-archive end record. TAR, ISO, LZH, DMG and VHD are not treated as leading-signature formats: previous offset-zero guesses were removed. No broad scanning or MIME heuristics are used. RAR5 signature mapping is tested; the bundled RAR extraction fixture verifies the older RAR family.

Encrypted archive behavior remains experimental independently of this unencrypted format matrix. Additional format fixtures, archive creation and platform expansion remain post-1.0 work.

Native provenance is recorded in `scripts/native-provenance.json` and checked by package verification. [7-Zip 26.02 upstream release](https://github.com/ip7z/7zip/releases/tag/26.02) provides the native source and distribution.
