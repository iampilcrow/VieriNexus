using System.Security.Cryptography;
using System.Text;

namespace VieriLink;

internal static class TokenProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VieriLink.Discord.BotToken.v1");

    public static string Protect(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return string.Empty;
        byte[] plain = Encoding.UTF8.GetBytes(token.Trim());
        return Convert.ToBase64String(ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser));
    }

    public static string Unprotect(string encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted)) return string.Empty;
        try
        {
            byte[] plain = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return string.Empty;
        }
    }
}
