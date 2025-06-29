using Google.Apis.Drive.v3;
using Google.Apis.Download;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PluginInstall
{

    public class PluginItem : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Size { get; set; }
        public string Modified { get; set; }
        public string FileId { get; set; }

        public bool IsRenderPlugin { get; set; }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); }
        }

        private bool _isDownloaded;
        public bool IsDownloaded
        {
            get => _isDownloaded;
            set { _isDownloaded = value; OnPropertyChanged(); }
        }

        private int _downloadProgress;
        public int DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    



    public class PluginInstaller
    {
        private readonly DriveService _driveService;
        private string _baseDirectory;

        public PluginInstaller(DriveService driveService)
        {
            _driveService = driveService;
        }

        public void SetBaseDirectory(string basePath)
        {
            _baseDirectory = basePath;
            // Ensure plugins directory exists
            Directory.CreateDirectory(GetPluginsPath());
        }

        public string GetPluginsPath()
        {
            return Path.Combine(_baseDirectory, "Data", "Plugins");
        }
        public string GetRenderPath()
        {
            return Path.Combine(_baseDirectory, "system");
        }

        public bool IsPluginDownloaded(string fileName)
        {
            string savePath = Path.Combine(GetPluginsPath(), fileName);
            return File.Exists(savePath);
        }
        public bool IsRenderDownloaded(string fileName)
        {
            string renderDir = Path.Combine(GetRenderPath(), "GD3D11");
            return Directory.Exists(renderDir);
        }
        public string GetPluginVisibility(string fileName)
        {
            return IsPluginDownloaded(fileName) ? "Visible" : "Collapsed";
        }
        public string GetRenderVisibility(string fileName)
        {
            return IsRenderDownloaded(fileName) ? "Visible" : "Collapsed";
        }

        public async Task<bool> DownloadPlugin(string fileId, string fileName, Action<double, long, DownloadStatus>? onProgress = null)
        {
            if (string.IsNullOrEmpty(_baseDirectory))
            {
                throw new DirectoryNotFoundException("Base directory not set");
            }

            string pluginsPath = GetPluginsPath();
            string savePath = Path.Combine(pluginsPath, fileName);

            try
            {
                var request = _driveService.Files.Get(fileId);
                request.Fields = "id,name,size";
                var file = await request.ExecuteAsync();
                long totalSize = file.Size ?? 0;

                var downloadRequest = _driveService.Files.Get(fileId);
                downloadRequest.MediaDownloader.ChunkSize = 64 * 1024;

                downloadRequest.MediaDownloader.ProgressChanged += (progress) =>
                {
                    onProgress?.Invoke(progress.BytesDownloaded / (double)totalSize * 100, progress.BytesDownloaded, progress.Status);
                };

                using (var fileStream = new FileStream(savePath, FileMode.Create))
                {
                    await downloadRequest.DownloadAsync(fileStream);
                }
                return true;
            }
            catch
            {
                if (File.Exists(savePath))
                    File.Delete(savePath);
                return false;
            }
        }

        
    }
}

