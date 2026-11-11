using System.Security.Cryptography;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Services;

internal static class InviteCodes
{
    // No 0/O/1/I: codes get read aloud and typed from screenshots.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, HikingGroup.InviteCodeLength);
}
