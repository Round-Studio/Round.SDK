using System.Security.Cryptography;
using System.Text;

namespace Round.SDK.Helper.IO;

public static class FileHashCalculator
{
    public enum HashType
    {
        MD5,
        SHA1,
        SHA256,
        SHA512
    }

    public static string CalculateHash(string filePath, HashType hashType)
    {
        using (var stream = File.OpenRead(filePath))
        {
            HashAlgorithm hashAlgorithm = hashType switch
            {
                HashType.MD5 => MD5.Create(),
                HashType.SHA1 => SHA1.Create(),
                HashType.SHA256 => SHA256.Create(),
                HashType.SHA512 => SHA512.Create(),
                _ => throw new ArgumentException("不支持的哈希类型")
            };

            using (hashAlgorithm)
            {
                byte[] hashBytes = hashAlgorithm.ComputeHash(stream);
                return ByteArrayToHexString(hashBytes);
            }
        }
    }

    private static string ByteArrayToHexString(byte[] bytes)
    {
        StringBuilder sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    // 替代方法：使用 BitConverter（结果中包含连字符）
    private static string ByteArrayToHexStringWithDash(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}