using System.IO.Hashing;
using System.Security.Cryptography;

namespace SevenZipWrapper.Tests.Helpers;

/// <summary>
/// Extension methods for computing hash strings from byte arrays.
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// Returns the lowercase hex MD5 hash of <paramref name="value"/>.
    /// </summary>
    public static string MD5String(this byte[] value)
    {
        return Convert.ToHexStringLower(MD5.HashData(value));
    }

    /// <summary>
    /// Returns the uppercase hex CRC-32 hash of <paramref name="value"/>.
    /// </summary>
    public static string CRC32String(this byte[] value)
    {
        byte[] hash = Crc32.Hash(value);
        return Convert.ToHexString(hash); // uppercase to match old behavior
    }
}