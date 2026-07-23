using System.Security.Cryptography;

namespace AstroDeviceHub;

public sealed record ServerAccessSettings(bool RemoteAccessEnabled, string AccessToken)
{
    public static ServerAccessSettings Default => new(false, string.Empty);
}

public sealed record UpdateServerAccessRequest(bool RemoteAccessEnabled, string? AccessToken);

/// <summary>Persists the optional LAN service mode outside the installation directory.</summary>
public sealed class ServerAccessStore
{
    private readonly object _sync = new();
    private readonly string _path;

    public ServerAccessStore()
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AstroDeviceHub",
            "server-access.json");
    }

    public ServerAccessSettings Read()
    {
        lock (_sync)
        {
            try
            {
                if (File.Exists(_path))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<ServerAccessSettings>(File.ReadAllText(_path))
                        ?? ServerAccessSettings.Default;
                }
            }
            catch (System.Text.Json.JsonException) { }
            return ServerAccessSettings.Default;
        }
    }

    public ServerAccessSettings Update(UpdateServerAccessRequest request)
    {
        lock (_sync)
        {
            var current = Read();
            var token = string.IsNullOrWhiteSpace(request.AccessToken) ? current.AccessToken : request.AccessToken.Trim();
            if (request.RemoteAccessEnabled && string.IsNullOrWhiteSpace(token)) token = CreateToken();
            var settings = new ServerAccessSettings(request.RemoteAccessEnabled, token);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, System.Text.Json.JsonSerializer.Serialize(settings));
            return settings;
        }
    }

    public static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    public bool IsValidRemoteToken(string? candidate)
    {
        var expected = Read().AccessToken;
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(candidate)) return false;
        var left = System.Text.Encoding.UTF8.GetBytes(expected);
        var right = System.Text.Encoding.UTF8.GetBytes(candidate.Trim());
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
