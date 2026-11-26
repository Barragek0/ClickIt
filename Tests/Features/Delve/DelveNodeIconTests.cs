namespace ClickIt.Tests.Features.Delve;

[TestClass]
public sealed class DelveNodeIconTests
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ClickIt.Tests", Guid.NewGuid().ToString("N"));

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow("BossProtoVaal", "The Crystal King's Throne", "Aul")]
    [DataRow("BossAbyss", "The Lich's Tomb", "Lich")]
    [DataRow("BossVaalCity", "The Grand Architect's Temple", "Architect")]
    [DataRow("azuriteveiniii.dds", "Azurite Fissure", "Azurite Tier 3")]
    [DataRow("Minion", "Grim Copse", "Minion or Aura Items")]
    [DataRow("Fossil", "Crystal Spire", "Crystal Spire")]
    [DataRow("Unknown", "Forgotten Chamber", "Forgotten Chamber")]
    public void GetTooltip_UsesIconLabelOrTheSpecificEncounterName(string asset, string encounter, string expected)
        => DelveNodeIcon.Resolve(asset).GetTooltip(encounter).Should().Be(expected);

    [TestCleanup]
    public void CleanUp()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task LoadTextureAsync_UsesEmbeddedArtworkWithoutContactingGitHub()
    {
        byte[] artwork = GetArtwork();
        using var handler = new TextureHandler(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var http = new HttpClient(handler);

        var result = await DelveNodeIcon.LoadTextureAsync("Currency", () => _folder, resource =>
        {
            resource.Should().Be("ClickIt.Delve.Textures.Currency.webp");
            return new MemoryStream(artwork);
        }, http);

        result.Error.Should().BeNull();
        result.Path.Should().Be(Path.Combine(_folder, "DelveTextures", "Currency.webp"));
        File.ReadAllBytes(result.Path!).Should().Equal(artwork);
        handler.RequestUri.Should().BeNull();
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow("missing")]
    [DataRow("unreadable")]
    [DataRow("invalid")]
    public async Task LoadTextureAsync_FallsBackToCanonicalGitHubFileAndCachesIt(string embedded)
    {
        DelveNodeIcon icon = DelveNodeIcon.Resolve("currency.dds");
        byte[] artwork = GetArtwork();
        using var handler = new TextureHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(artwork)
        });
        using var http = new HttpClient(handler);

        var result = await DelveNodeIcon.LoadTextureAsync(icon.AssetName!, () => _folder, _ => embedded switch
        {
            "missing" => null,
            "unreadable" => throw new IOException("Resource unavailable"),
            _ => new MemoryStream(new byte[20])
        }, http);

        result.Error.Should().BeNull();
        result.Path.Should().Be(Path.Combine(_folder, "DelveTextures", "Currency.webp"));
        File.ReadAllBytes(result.Path!).Should().Equal(artwork);
        handler.RequestUri.Should().Be(new Uri(
            "https://raw.githubusercontent.com/Barragek0/ClickIt/main/Features/Delve/Textures/Currency.webp"));

        using var offlineHandler = new TextureHandler(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var offline = new HttpClient(offlineHandler);
        var cached = await DelveNodeIcon.LoadTextureAsync(icon.AssetName!, () => _folder, _ => null, offline);
        cached.Should().Be(result);
        offlineHandler.RequestUri!.Host.Should().Be("api.github.com");
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(404, "HTTP 404")]
    [DataRow(200, "invalid or changed texture")]
    public async Task LoadTextureAsync_ReturnsAnErrorWithoutCachingFailedDownloads(int status, string reason)
    {
        using var handler = new TextureHandler(new HttpResponseMessage((System.Net.HttpStatusCode)status)
        {
            Content = new ByteArrayContent(new byte[20])
        });
        using var http = new HttpClient(handler);

        var result = await DelveNodeIcon.LoadTextureAsync("Currency", () => _folder, _ => null, http);

        result.Path.Should().BeNull();
        result.Error.Should().Be($"Could not load Currency.webp from GitHub ({reason}).");
        Directory.Exists(_folder).Should().BeFalse();
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadTextureAsync_OnlyDownloadsAgainIfTheGitHubHashDiffers(bool stale)
    {
        byte[] artwork = GetArtwork();
        string folder = Path.Combine(_folder, "DelveTextures");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Currency.webp");
        File.WriteAllBytes(path, stale ? GetArtwork("Maps") : artwork);
        const string index = """[{"name":"Currency.webp","sha":"a2b201aed231b85103ffef06851df8da5c2d3a68"}]""";
        using var handler = new HashCheckHandler(index, artwork);
        using var http = new HttpClient(handler);

        var result = await DelveNodeIcon.LoadTextureAsync("Currency", () => _folder, _ => null, http);

        handler.HashChecks.Should().Be(1);
        handler.Downloads.Should().Be(stale ? 1 : 0);
        result.Error.Should().BeNull();
        result.Path.Should().Be(path);
        File.ReadAllBytes(path).Should().Equal(artwork);
    }

    private static byte[] GetArtwork(string name = "Currency")
    {
        using Stream resource = typeof(DelveNodeIcon).Assembly.GetManifestResourceStream($"ClickIt.Delve.Textures.{name}.webp")!;
        using var data = new MemoryStream();
        resource.CopyTo(data);
        return data.ToArray();
    }

    private sealed class HashCheckHandler(string index, byte[] artwork) : HttpMessageHandler
    {
        internal int HashChecks { get; private set; }
        internal int Downloads { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == "api.github.com")
            {
                HashChecks++;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(index) });
            }

            Downloads++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(artwork) });
        }
    }

    private sealed class TextureHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        internal Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                response.Dispose();
            base.Dispose(disposing);
        }
    }
}
