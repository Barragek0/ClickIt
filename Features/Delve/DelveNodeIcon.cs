namespace ClickIt.Features.Delve;

internal sealed class DelveNodeIcon
{
    private const string ResourcePrefix = "ClickIt.Delve.Textures.";
    private const string Extension = ".webp";
    private const string DownloadRoot = "https://raw.githubusercontent.com/Barragek0/ClickIt/main/Features/Delve/Textures/";
    private const string HashIndexUrl = "https://api.github.com/repos/Barragek0/ClickIt/contents/Features/Delve/Textures?ref=main";
    private const int MaxTextureBytes = 128 * 1024;
    private static readonly string BundleVersion = typeof(DelveNodeIcon).Module.ModuleVersionId.ToString("N");
    private static readonly HttpClient TextureClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly Lazy<Task<Dictionary<string, string>>> RemoteHashes = new(() => ReadRemoteHashesAsync(TextureClient));
    private static readonly SemaphoreSlim DownloadSlots = new(4);
    private static readonly DelveNodeIcon Generic = new(null);
    // Keep the canonical GitHub filenames available even when the assembly has no embedded textures.
    private static readonly string[] AssetNames =
    [
        "AbyssalCity", "AbyssEncounter", "Armour", "AzuriteVein", "AzuriteVeinII", "AzuriteVeinIII",
        "Bestiary", "Beyond", "Blood", "BossAbyss", "BossProtoVaal", "BossVaalCity", "Breach", "Chaos",
        "Chest", "Cold", "Currency", "Curse", "Fire", "Fossil", "Gems", "Legion", "Lightning", "Maps",
        "Minion", "ProtoVaalCity", "Strongbox", "Trinkets", "VaalCity", "WarbandChaos", "WarbandFire",
        "WarbandLightning", "Weapons"
    ];
    private static readonly Dictionary<string, DelveNodeIcon> Icons = AssetNames.ToDictionary(static name => name,
        static name => new DelveNodeIcon(name), StringComparer.OrdinalIgnoreCase);

    internal readonly record struct TextureResult(string? Path, string? Error = null);

    internal string? AssetName { get; }
    internal string? TextureName { get; }

    private DelveNodeIcon(string? assetName)
    {
        AssetName = assetName;
        TextureName = assetName == null ? null : $"ClickIt.Delve.{BundleVersion}.{assetName}";
    }

    internal static DelveNodeIcon Resolve(string? image)
    {
        string? key = Path.GetFileNameWithoutExtension(image);
        return key != null && Icons.TryGetValue(key, out DelveNodeIcon? icon) ? icon : Generic;
    }

    internal Task<TextureResult> LoadAsync(Func<string> getConfigDirectory)
        => AssetName is { } assetName
            ? Task.Run(() => LoadTextureAsync(assetName, getConfigDirectory,
                typeof(DelveNodeIcon).Assembly.GetManifestResourceStream, TextureClient, () => RemoteHashes.Value))
            : Task.FromResult(default(TextureResult));

    internal static async Task<TextureResult> LoadTextureAsync(string assetName, Func<string> getConfigDirectory,
        Func<string, Stream?> openResource, HttpClient http, Func<Task<Dictionary<string, string>>>? getHashes = null)
    {
        string fileName = assetName + Extension;
        try
        {
            string configDirectory = getConfigDirectory();
            if (string.IsNullOrWhiteSpace(configDirectory))
                throw new InvalidOperationException("ClickIt's config directory is unavailable.");
            string path = Path.Combine(configDirectory, "DelveTextures", fileName);
            byte[]? cached = await TryReadWebpAsync(() => File.Exists(path) ? File.OpenRead(path) : null).ConfigureAwait(false);

            byte[]? texture = await TryReadWebpAsync(() => openResource(ResourcePrefix + fileName)).ConfigureAwait(false);
            if (texture != null)
            {
                if (cached == null || !cached.AsSpan().SequenceEqual(texture))
                    await WriteTextureAsync(path, texture).ConfigureAwait(false);
                return new TextureResult(path);
            }

            string? expectedHash = null;
            if (cached != null)
            {
                var hashes = await (getHashes?.Invoke() ?? ReadRemoteHashesAsync(http)).ConfigureAwait(false);
                if (!hashes.TryGetValue(fileName, out expectedHash) || MatchesGitBlob(cached, expectedHash))
                    return new TextureResult(path);
            }

            try
            {
                texture = await DownloadTextureAsync(fileName, http).ConfigureAwait(false);
                if (expectedHash != null && !MatchesGitBlob(texture, expectedHash))
                    throw new InvalidDataException("GitHub texture hash changed during download.");
            }
            catch (Exception ex)
            {
                string reason = ex switch
                {
                    HttpRequestException { StatusCode: { } status } => $"HTTP {(int)status}",
                    OperationCanceledException => "request timed out",
                    InvalidDataException or EndOfStreamException => "invalid or changed texture",
                    _ => ex.GetType().Name
                };
                return new TextureResult(cached == null ? null : path, $"Could not load {fileName} from GitHub ({reason}).");
            }

            await WriteTextureAsync(path, texture).ConfigureAwait(false);
            return new TextureResult(path);
        }
        catch (Exception ex)
        {
            return new TextureResult(null, $"Could not cache {fileName} ({ex.GetType().Name}).");
        }
    }

    private static async Task<Dictionary<string, string>> ReadRemoteHashesAsync(HttpClient http)
    {
        Dictionary<string, string> hashes = new(StringComparer.Ordinal);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, HashIndexUrl);
            request.Headers.UserAgent.ParseAdd("ClickIt");
            using HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            foreach (var file in json.RootElement.EnumerateArray())
            {
                if (file.TryGetProperty("name", out var name) && name.GetString() is { } fileName
                    && file.TryGetProperty("sha", out var sha) && sha.GetString() is { Length: 40 } hash)
                    hashes[fileName] = hash;
            }
        }
        catch
        {
            // Keep usable local artwork when GitHub's metadata endpoint is unavailable.
        }

        return hashes;
    }

    private static bool MatchesGitBlob(byte[] bytes, string expectedHash)
    {
        // GitHub reports the Git blob identifier, which includes the type and length header.
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA1);
        hash.AppendData(System.Text.Encoding.ASCII.GetBytes(FormattableString.Invariant($"blob {bytes.Length}\0")));
        hash.AppendData(bytes);
        return Convert.ToHexString(hash.GetHashAndReset()).Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<byte[]?> TryReadWebpAsync(Func<Stream?> open)
    {
        try
        {
            using Stream? stream = open();
            return stream == null ? null : await ReadWebpAsync(stream, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Missing, unreadable, and malformed resources all use the next source.
            return null;
        }
    }

    private static async Task<byte[]> DownloadTextureAsync(string fileName, HttpClient http)
    {
        await DownloadSlots.WaitAsync().ConfigureAwait(false);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using HttpResponseMessage response = await http.GetAsync(DownloadRoot + fileName,
                HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxTextureBytes)
                throw new InvalidDataException("Texture exceeds the size limit.");
            using Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            return await ReadWebpAsync(stream, timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            DownloadSlots.Release();
        }
    }

    private static async Task<byte[]> ReadWebpAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[20];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        uint size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4));
        uint firstChunkSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16, 4));
        ReadOnlySpan<byte> chunk = header.AsSpan(12, 4);
        if (!header.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !header.AsSpan(8, 4).SequenceEqual("WEBP"u8)
            || !(chunk.SequenceEqual("VP8 "u8) || chunk.SequenceEqual("VP8L"u8) || chunk.SequenceEqual("VP8X"u8))
            || size <= 12 || size > MaxTextureBytes - 8 || firstChunkSize == 0 || firstChunkSize > size - 12)
            throw new InvalidDataException("Invalid WebP header.");

        byte[] bytes = new byte[size + 8];
        header.CopyTo(bytes, 0);
        await stream.ReadExactlyAsync(bytes.AsMemory(header.Length), cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    private static async Task WriteTextureAsync(string path, byte[] texture)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(staging, texture).ConfigureAwait(false);
            File.Move(staging, path, overwrite: true);
        }
        finally
        {
            try { File.Delete(staging); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
