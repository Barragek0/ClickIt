namespace ClickIt.Features.Delve;

internal sealed class DelveNodeIcon
{
    private const string ResourcePrefix = "ClickIt.Delve.Textures.";
    private const string Extension = ".webp";
    private const string DownloadRoot = "https://raw.githubusercontent.com/Barragek0/ClickIt/main/Features/Delve/Textures/";
    private const int MaxTextureBytes = 128 * 1024;
    private static readonly string BundleVersion = typeof(DelveNodeIcon).Module.ModuleVersionId.ToString("N");
    private static readonly HttpClient TextureClient = new() { Timeout = TimeSpan.FromSeconds(8) };
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

    private readonly Lazy<Task<TextureResult>>? _load;
    internal string? AssetName { get; }
    internal string? TextureName { get; }
    internal TextureResult Texture => _load is { IsValueCreated: true } && _load.Value.IsCompletedSuccessfully
        ? _load.Value.Result
        : default;

    private DelveNodeIcon(string? assetName)
    {
        AssetName = assetName;
        if (assetName == null)
            return;

        TextureName = $"ClickIt.Delve.{BundleVersion}.{assetName}";
        _load = new Lazy<Task<TextureResult>>(() => Task.Run(() => LoadTextureAsync(assetName,
            static () => Path.Combine(Path.GetTempPath(), "ClickIt", "DelveTextures", BundleVersion),
            typeof(DelveNodeIcon).Assembly.GetManifestResourceStream, TextureClient)));
    }

    internal static DelveNodeIcon Resolve(string? image)
    {
        string? key = Path.GetFileNameWithoutExtension(image);
        return key != null && Icons.TryGetValue(key, out DelveNodeIcon? icon) ? icon : Generic;
    }

    internal void Prepare() => _ = _load?.Value;

    internal static async Task<TextureResult> LoadTextureAsync(string assetName, Func<string> getFolder,
        Func<string, Stream?> openResource, HttpClient http)
    {
        string fileName = assetName + Extension;
        try
        {
            string path = Path.Combine(getFolder(), fileName);
            if (await TryReadWebpAsync(() => File.Exists(path) ? File.OpenRead(path) : null).ConfigureAwait(false) != null)
                return new TextureResult(path);

            byte[]? texture = await TryReadWebpAsync(() => openResource(ResourcePrefix + fileName)).ConfigureAwait(false);
            if (texture == null)
            {
                try
                {
                    texture = await DownloadTextureAsync(fileName, http).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    string reason = ex switch
                    {
                        HttpRequestException { StatusCode: { } status } => $"HTTP {(int)status}",
                        OperationCanceledException => "request timed out",
                        InvalidDataException or EndOfStreamException => "invalid WebP",
                        _ => ex.GetType().Name
                    };
                    return new TextureResult(null, $"Could not load {fileName} from GitHub ({reason}).");
                }
            }

            await WriteTextureAsync(path, texture).ConfigureAwait(false);
            return new TextureResult(path);
        }
        catch (Exception ex)
        {
            return new TextureResult(null, $"Could not cache {fileName} ({ex.GetType().Name}).");
        }
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
