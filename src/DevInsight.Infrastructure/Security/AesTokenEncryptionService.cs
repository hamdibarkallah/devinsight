using System.Security.Cryptography;
using System.Text;
using DevInsight.Application.Common;
using Microsoft.Extensions.Configuration;
namespace DevInsight.Infrastructure.Security;
public class AesTokenEncryptionService : ITokenEncryptionService
{
    private readonly byte[] _key;
    public AesTokenEncryptionService(IConfiguration config)
    {
        var keyStr = config["Encryption:Key"] ?? throw new InvalidOperationException("Encryption:Key not configured.");
        _key = Convert.FromBase64String(keyStr);
    }
    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key; aes.GenerateIV();
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = aes.CreateEncryptor().TransformFinalBlock(plain, 0, plain.Length);
        var result = new byte[aes.IV.Length + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
        return Convert.ToBase64String(result);
    }
    public string Decrypt(string cipherText)
    {
        var full = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = _key;
        var iv = new byte[aes.BlockSize / 8];
        var cipher = new byte[full.Length - iv.Length];
        Buffer.BlockCopy(full, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(full, iv.Length, cipher, 0, cipher.Length);
        aes.IV = iv;
        return Encoding.UTF8.GetString(aes.CreateDecryptor().TransformFinalBlock(cipher, 0, cipher.Length));
    }
}
