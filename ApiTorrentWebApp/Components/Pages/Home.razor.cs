using Blazor.DownloadFileFast.Interfaces;
using Microsoft.AspNetCore.Components.Forms;
using MonoTorrent;
using MonoTorrent.Client;

namespace ApiTorrentWebApp.Components.Pages
{
    public partial class Home(IBlazorDownloadFileService blazorDownloadFileService)
    {
        private double progress;
        private string? status;
        async Task Download(InputFileChangeEventArgs e)
        {
            status = null;
            progress = 0;
            await using var torrentStream = e.File.OpenReadStream();
            var tms = new MemoryStream();
            await torrentStream.CopyToAsync(tms);
            tms.Position = 0;
            var torrent = await Torrent.LoadAsync(tms);

            var tempPath = Guid.NewGuid().ToString();
            Directory.CreateDirectory(tempPath);

            var engine = new ClientEngine(new EngineSettingsBuilder
            {
                DiskCacheBytes = 0
            }.ToSettings());

            var manager = await engine.AddStreamingAsync(torrent, tempPath);
            try
            {
                manager.TorrentStateChanged += Manager_TorrentStateChanged;
                manager.PieceHashed += ManagerOnPieceHashed;
                await manager.StartAsync();

                var file = manager.Files[0];
                await using var stream = await manager.StreamProvider.CreateStreamAsync(
                    file,
                    false
                );
                using var output = new MemoryStream();
                await stream.CopyToAsync(output);
                await blazorDownloadFileService.DownloadFileAsync(Path.GetFileName(file.Path), output.ToArray());
            }
            catch (Exception ex)
            {
                status = ex.Message;
            }
            finally
            {
                manager.TorrentStateChanged -= Manager_TorrentStateChanged;
                manager.PieceHashed -= ManagerOnPieceHashed;
                try
                {
                    Directory.Delete(tempPath, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        private void ManagerOnPieceHashed(object? sender, PieceHashedEventArgs e)
        {
            progress = e.TorrentManager.Progress;
            InvokeAsync(StateHasChanged);
        }

        private void Manager_TorrentStateChanged(object? sender, TorrentStateChangedEventArgs e)
        {
            status = e.NewState.ToString();
        }
    }
}