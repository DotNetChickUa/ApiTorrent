using MonoTorrent;
using MonoTorrent.Client;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors();
app.MapPost("/download", async (HttpRequest request, HttpResponse response) =>
{
    using var torrentStream = new MemoryStream();
    await request.Body.CopyToAsync(torrentStream);
    torrentStream.Position = 0;
    var torrent = await Torrent.LoadAsync(torrentStream);

    var tempPath = Guid.NewGuid().ToString();
    Directory.CreateDirectory(tempPath);

    try
    {
        var engine = new ClientEngine(new EngineSettingsBuilder
        {
            DiskCacheBytes = 0
        }.ToSettings());

        var manager = await engine.AddStreamingAsync(torrent, tempPath);
        await manager.StartAsync();
        
        var file = manager.Files[0];

        await using var stream = await manager.StreamProvider.CreateStreamAsync(
            file,
            false
        );

        response.ContentType = "application/octet-stream";
        response.Headers.ContentDisposition = $"attachment; filename=\"{file.Path}\"";
        response.ContentLength = file.Length;

        await stream.CopyToAsync(response.Body);
    }
    finally
    {
        try
        {
            Directory.Delete(tempPath, recursive: true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
});

app.Run();
