using System.Security.Cryptography;
using Flowza.Identity.Application.Abstractions;

namespace Flowza.Identity.Application.Users;

/// <summary>
/// 16 random characters from a cryptographic generator, without look-alike characters (0/O, 1/l/I), so an admin can
/// read it out. About 92 bits of randomness: never common, never a username, longer than the minimum (IAM-06).
/// </summary>
public sealed class TemporaryPasswordGenerator : ITemporaryPasswordGenerator
{
    public const int Length = 16;
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);
}
