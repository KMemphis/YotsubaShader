using System.Security.Cryptography;
using System.Text;

namespace YotsubaShader.Community;

/// <summary>
/// A short code both the app and the sign-in page show for the same login id. Without it, someone could send a victim a
/// link that signs them in on a login the attacker started, and collect the victim's session. The user compares the codes.
/// </summary>
public static class LoginCode
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // no 0/O/1/I/L

    public static string From(string loginId)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(loginId));
        var sb = new StringBuilder(9);
        for (var i = 0; i < 8; i++)
        {
            if (i == 4) sb.Append('-');
            sb.Append(Alphabet[hash[i] % Alphabet.Length]);
        }
        return sb.ToString();
    }
}
