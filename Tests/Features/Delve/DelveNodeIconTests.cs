namespace ClickIt.Tests.Features.Delve;

[TestClass]
public sealed class DelveNodeIconTests
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ClickIt.Tests", Guid.NewGuid().ToString("N"));

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
        File.ReadAllBytes(result.Path!).Should().Equal(artwork);
        handler.RequestUri.Should().Be(new Uri(
            "https://raw.githubusercontent.com/Barragek0/ClickIt/main/Features/Delve/Textures/Currency.webp"));

        using var offlineHandler = new TextureHandler(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        using var offline = new HttpClient(offlineHandler);
        var cached = await DelveNodeIcon.LoadTextureAsync(icon.AssetName!, () => _folder, _ => null, offline);
        cached.Should().Be(result);
        offlineHandler.RequestUri.Should().BeNull();
    }

    [TestMethod]
    [TestCategory("Regression")]
    [DataRow(404, "HTTP 404")]
    [DataRow(200, "invalid WebP")]
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

    private static byte[] GetArtwork()
    {
        using Stream resource = typeof(DelveNodeIcon).Assembly.GetManifestResourceStream("ClickIt.Delve.Textures.Currency.webp")!;
        using var data = new MemoryStream();
        resource.CopyTo(data);
        return data.ToArray();
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
