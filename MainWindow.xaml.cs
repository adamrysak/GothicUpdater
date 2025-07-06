using Google.Apis.Drive.v3;
using Google.Apis.Download;
using Google.Apis.Services;

using System.IO;
using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO.Compression;
using System.Configuration;

using InitialSettingsLoad;
using ScriptInstall;
using PluginInstall;
using GothicIni;






namespace GothicUpdater
{
    public static class FolderPicker
    {
        public static string PickFolder()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                CheckFileExists = false,
                FileName = "Select folder"
            };

            if (dialog.ShowDialog() == true)
            {
                return System.IO.Path.GetDirectoryName(dialog.FileName);
            }

            return null;
        }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        static string ApplicationName = "GothicUpdater";
        private DriveService service;
        public event PropertyChangedEventHandler PropertyChanged;
        public string rootShareID = "1onSWrKizPuGAu8TGWnuSa4gM0JEemGe5";

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        private bool _pluginsLoaded = false;
        private IList<Google.Apis.Drive.v3.Data.File> _cachedPluginFiles = null;
        private PluginInstaller _pluginInstaller;
        private GothicIniLoader _gothicIniLoader;
        private InitialSettingsLoader _initialSettingsLoader;
        public string scriptVer { get; set; }
        public string czechVer { get; set; }
        
        private string _newScriptVer;
        public string NewScriptVer
        {
            get => _newScriptVer;
            set
            {
                _newScriptVer = value;
                OnPropertyChanged();
            }
        }

        private string _newCzechVer;
        public string NewCzechVer
        {
            get => _newCzechVer;
            set
            {
                _newCzechVer = value;
                OnPropertyChanged();
            }
        }
        public string scriptFileId { get; set; }





        public MainWindow()
        {
            _initialSettingsLoader = new InitialSettingsLoader();
            scriptVer = _initialSettingsLoader.GeneralSettings["LastScriptVer"];
            czechVer = _initialSettingsLoader.GeneralSettings["LastCzechVer"];
            InitializeComponent();
            DataContext = this;

            Loaded += MainWindow_Loaded; // Asynchronous logic moved here
            InitializeGoogleDriveService();
            _pluginInstaller = new PluginInstaller(service);
            string loadedPath = _initialSettingsLoader.PathSettings["NewBalanceFolderPath"];
            if (!string.IsNullOrEmpty(loadedPath) && validateGameDirectory(loadedPath))
            {
                SetPathToDependencies(loadedPath);
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            
            scriptFileId = await FindLatestScripts();
            

            
        }
        
        private void SetPathToDependencies(string path)
        {
            FolderPathTextBox.Text = path;
            _pluginInstaller.SetBaseDirectory(path);
            _gothicIniLoader = new GothicIniLoader(path);
        }

        public static string[] SplitBySlash(string input)
        {
            if (string.IsNullOrEmpty(input))
                return Array.Empty<string>();

            return input.Split('/');
        }

        private bool validateGameDirectory(string path)
        {
            string[] requiredFolders = { "_work", "Miles", "Data", "system" };
            string requiredFile = Path.Combine(path, "Data", "AB_scripts.vdf");
            bool allFoldersExist = requiredFolders.All(folder =>
                Directory.Exists(Path.Combine(path, folder)));
            bool fileExists = File.Exists(requiredFile);
            return allFoldersExist && fileExists;
        }




        // Set the target directory when browsing
        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            while (true)
            {
                string folder = FolderPicker.PickFolder();
                if (!string.IsNullOrEmpty(folder))
                {
                    
                    string selectedPath = folder;

                    // Validate required subfolders and file
                    string[] requiredFolders = { "_work", "Miles", "Data", "system" };
                    string requiredFile = Path.Combine(selectedPath, "Data", "AB_scripts.vdf");

                    bool allFoldersExist = requiredFolders.All(folder =>
                        Directory.Exists(Path.Combine(selectedPath, folder)));

                    bool fileExists = File.Exists(requiredFile);

                    if (allFoldersExist && fileExists)
                    {
                            
                        _initialSettingsLoader.SaveOrUpdateSetting("Paths", "NewBalanceFolderPath", selectedPath);
                        SetPathToDependencies(selectedPath);
                        break; // exit the loop
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            "Toto není validní složka NB.\n\n",
                            "Invalid Folder",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                        // Loop will continue and reopen dialog
                    }
                }
                else
                {
                    break; // user canceled
                }
                
            }
        }

        private static readonly char[] separator = new[] { ',' };

        private bool ValidateOrSelectDirectory()
        {
            if (!string.IsNullOrEmpty(FolderPathTextBox.Text) && Directory.Exists(FolderPathTextBox.Text))
                return true;

            var result = System.Windows.MessageBox.Show("Vyberte prosím adresář s NB",
                                      "Directory Required",
                                      MessageBoxButton.OKCancel,
                                      MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                

                string folder = FolderPicker.PickFolder();
                if (!string.IsNullOrEmpty(folder))
                {
                    FolderPathTextBox.Text = folder;
                    _pluginInstaller.SetBaseDirectory(folder);
                    return true;
                }
            }
            return false;
        }

        private string GetFolderIdByPath(string[] pathSegments)
        {
            string currentParentId = rootShareID;

            foreach (var raw in pathSegments)
            {
                // Escape single quotes in folder names
                var segment = raw.Replace("'", "\\'");

                var request = service.Files.List();
                request.Q =
                    $"mimeType = 'application/vnd.google-apps.folder' " +
                    $"and name = '{segment}' " +
                    $"and '{currentParentId}' in parents";
                request.Fields = "files(id, name)";
                // ← search both My Drive and any Shared Drives
                request.SupportsAllDrives = true;
                request.IncludeItemsFromAllDrives = true;

                var result = request.Execute();
                if (result.Files == null || result.Files.Count == 0)
                    throw new DirectoryNotFoundException(
                        $"Folder '{raw}' not found under parent ID '{currentParentId}'");

                // descend into the first match
                currentParentId = result.Files[0].Id;
            }

            return currentParentId;
        }

        private void SwitchView_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrEmpty(FolderPathTextBox.Text) || !Directory.Exists(FolderPathTextBox.Text))
            {
                var result = System.Windows.MessageBox.Show("Vyberte prosím adresář s NB",
                                          "Directory Required",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Warning);

                if (result == MessageBoxResult.OK)
                {
                    BrowseFolder_Click(null, null);
                }

                if (string.IsNullOrEmpty(FolderPathTextBox.Text)) return;
            }
            // Hide all panels first
            updatePanel.Visibility = Visibility.Collapsed;
            pluginPanel.Visibility = Visibility.Collapsed;
            iniSettingsPanel.Visibility = Visibility.Collapsed;

            // Determine which button was clicked and show the corresponding panel
            var button = (System.Windows.Controls.Button)sender;

            if (button.Name.Equals("UpdateButton"))
            {
                updatePanel.Visibility = Visibility.Visible;
            }
            else if (button.Name.Equals("PluginButton"))
            {
                pluginPanel.Visibility = Visibility.Visible;
                // Reset view state when switching to plugins
                PluginsListBox.Visibility = Visibility.Visible;
                PluginLoadingGrid.Visibility = Visibility.Collapsed;
                LoadPluginsButton_Click(null, null);
            }
            else if (button.Name.Equals("IniSettingsButton"))
            {
                UpdateSwitchCheckboxes();

                // Add the button to the panel (if not already added)
                var applyButton = new System.Windows.Controls.Button
                {
                    Content = "Apply INI Changes",
                    Margin = new Thickness(left: 200, top: 100, right: 200, bottom: 0),
                    Padding = new Thickness(10),
                };

                applyButton.Click += ApplyIniChanges_Click;

                // Make sure the button is added to the iniSettingsPanel if not already present
                if (!iniSettingsPanel.Children.Contains(applyButton))
                {
                    iniSettingsPanel.Children.Add(applyButton);
                }

                // Make sure the INI settings panel is visible now
                iniSettingsPanel.Visibility = Visibility.Visible;
            }
        }

        private void InitializeGoogleDriveService()
        {
            try
            {
                // Read your key from App.config or environment
                string apiKey = ConfigurationManager.AppSettings["GoogleApiKey"];
                service = new DriveService(new BaseClientService.Initializer
                {
                    ApiKey = apiKey,
                    ApplicationName = ApplicationName,
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to initialize Google Drive: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error
                );
            }
        }



        private async Task<IList<Google.Apis.Drive.v3.Data.File>> ListPluginFiles()
        {
            try
            {
                // Use path resolution instead of nesting folder queries
                var pluginyFolderId = GetFolderIdByPath(SplitBySlash("DODATKY A PLUGINY/PLUGINY A PATCHE"));
                if (pluginyFolderId == null)
                {
                    System.Windows.MessageBox.Show("Folder path not found: GOTHIC 2: NEW BALANCE/DODATKY A PLUGINY/PLUGINY",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                // List files in PLUGINY folder, prefilter with 'name contains .vdf'
                var filesRequest = service.Files.List();
                filesRequest.Q = $"'{pluginyFolderId}' in parents and mimeType != 'application/vnd.google-apps.folder' and name contains '.vdf'";
                filesRequest.Fields = "files(id, name, size, modifiedTime)";
                var filesResult = await filesRequest.ExecuteAsync();

                // Post-filter to ensure filename ends with ".vdf"
                var vdfFiles = filesResult.Files
                    .Where(f => f.Name.EndsWith(".vdf", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return vdfFiles;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error listing plugin files: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }


        private async Task<IList<Google.Apis.Drive.v3.Data.File>> GetLatestRenderPluginInfo()
        {
            try
            {
                // Define the folder path and resolve to folder ID
                string folderPath = "DODATKY A PLUGINY/GD3D11 (DX11)";
                var folderSegments = folderPath.Split('/');
                string gd3d11Id = GetFolderIdByPath(folderSegments);
                if (gd3d11Id == null)
                {
                    System.Windows.MessageBox.Show("Folder path not found: " + folderPath,
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                // List all non-folder files in 'GD3D11 (DX11)'
                var fileRequest = service.Files.List();
                fileRequest.Q = $"'{gd3d11Id}' in parents and mimeType != 'application/vnd.google-apps.folder'";
                fileRequest.Fields = "files(id, name, size, modifiedTime)";
                var fileResult = await fileRequest.ExecuteAsync();

                if (fileResult.Files == null || fileResult.Files.Count == 0)
                {
                    System.Windows.MessageBox.Show("No files found in 'GD3D11 (DX11)'.",
                        "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return new List<Google.Apis.Drive.v3.Data.File>();
                }

                return fileResult.Files;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error retrieving render plugin info: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }


        public void ExtractRender(string zipFilePath)
        {
            string extractPath = Path.Combine(FolderPathTextBox.Text, "system");
            List<string> extractedFiles = new List<string>();

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue; // skip folders

                        string destinationPath = Path.Combine(extractPath, entry.FullName);

                        // Make sure the directory exists
                        string destinationDir = Path.GetDirectoryName(destinationPath);
                        if (!Directory.Exists(destinationDir))
                            Directory.CreateDirectory(destinationDir);

                        // Now safe to extract
                        entry.ExtractToFile(destinationPath, true);

                        // Only save files that are at root (no slash)
                        if (!entry.FullName.Contains("/"))
                        {
                            extractedFiles.Add(entry.Name);
                        }
                    }


                }
                string combinedFileNames = string.Join(",", extractedFiles);
                _initialSettingsLoader.SaveOrUpdateSetting("Paths", "Render", combinedFileNames);
                _initialSettingsLoader.PathSettings["Render"] = combinedFileNames;

                File.Delete(zipFilePath); // Delete the zip after extraction

                
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error extracting render file: {ex.Message}", "Extraction Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        


        private async void LoadPluginsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Skip loading if already done
                if (_pluginsLoaded)
                {
                    return;
                }

                // Show loading state
                PluginsListBox.Visibility = Visibility.Collapsed;
                PluginLoadingGrid.Visibility = Visibility.Visible;

                // Use cached files or load new ones
                var pluginFiles = _cachedPluginFiles ?? await ListPluginFiles();
                var renderPluginFiles = await GetLatestRenderPluginInfo();

                if ((pluginFiles != null && pluginFiles.Count > 0) ||
                    (renderPluginFiles != null && renderPluginFiles.Count > 0))
                {
                    _cachedPluginFiles = pluginFiles;
                    _pluginsLoaded = true;

                    // Map regular plugin files
                    var pluginItems = pluginFiles.Select(f => new PluginItem
                    {
                        Name = f.Name,
                        Size = f.Size.HasValue ? (f.Size.Value / 1024) + " KB" : "N/A",
                        Modified = f.ModifiedTimeDateTimeOffset?.ToString("dd.MM.yyyy HH:mm") ?? "N/A",
                        FileId = f.Id,
                        IsDownloaded = _pluginInstaller.IsPluginDownloaded(f.Name)
                        
                    }).ToList();

                    // Map render plugin files separately (assuming RenderPluginItem is a different class)
                    var renderPluginItems = renderPluginFiles.Select(f => new PluginItem
                    {
                        Name = f.Name,
                        Size = f.Size.HasValue ? (f.Size.Value / 1024) + " KB" : "N/A",
                        Modified = f.ModifiedTimeDateTimeOffset?.ToString("dd.MM.yyyy HH:mm") ?? "N/A",
                        FileId = f.Id,
                        IsDownloaded = _pluginInstaller.IsRenderDownloaded(f.Name),
                        IsRenderPlugin = true,
                    }).ToList();

                    // Merge the two lists into one ObservableCollection
                    var allPluginItems = renderPluginItems.Concat(pluginItems).ToList();
                    // Bind only standard plugins for now
                    PluginsListBox.ItemsSource = new ObservableCollection<PluginItem>(allPluginItems);

                    // You can bind renderPluginItems to a different ListBox here if needed
                    // RenderPluginsListBox.ItemsSource = new ObservableCollection<RenderPluginItem>(renderPluginItems);
                }
                else if (!_pluginsLoaded) // Only show message if first load fails
                {
                    System.Windows.MessageBox.Show("No plugin files found in the specified folders.",
                        "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (!_pluginsLoaded) // Only show error if first load fails
                {
                    System.Windows.MessageBox.Show($"Error loading plugins: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                // Hide loading and show results
                PluginLoadingGrid.Visibility = Visibility.Collapsed;
                PluginsListBox.Visibility = Visibility.Visible;
            }
        }



        private async void DownloadPlugin_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateOrSelectDirectory())
                return;

            var button = (System.Windows.Controls.Button)sender;
            string fileId = button.Tag.ToString();
            var pluginItem = (PluginItem)button.DataContext;

            string fileName = pluginItem.Name;

            try
            {
                

                pluginItem.IsDownloading = true;

                bool success = await _pluginInstaller.DownloadPlugin(fileId, fileName, (percentage, bytesDownloaded, status) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        pluginItem.DownloadProgress = (int)percentage;

                        if (status == DownloadStatus.Downloading)
                        {
                            DownloadProgressBar.IsIndeterminate = false;
                            //DownloadStatusText.Text = $"Downloading... {percentage:0.0}%";
                        }
                        else if (status == DownloadStatus.Completed)
                        {
                            DownloadProgressBar.Value = 100;
                            //DownloadStatusText.Text = "Download complete!";
                        }
                    });
                });

                pluginItem.IsDownloading = false;

                if (success)
                {
                    if (pluginItem.IsRenderPlugin)
                    {
                        string zipFilePath = Path.Combine(_pluginInstaller.GetPluginsPath(),fileName);
                        ExtractRender(zipFilePath);

                    }
                    pluginItem.IsDownloaded = true;
                    pluginItem.DownloadProgress = 0;
                }
                else
                {
                    System.Windows.MessageBox.Show($"Failed to download {fileName}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error downloading file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeletePlugin_Click(object sender, RoutedEventArgs e)
        {
            var button = (System.Windows.Controls.Button)sender;
            var plugin = (PluginItem)button.DataContext;
            string pluginPath = Path.Combine(_pluginInstaller.GetPluginsPath(), plugin.Name);

            if (plugin.IsRenderPlugin)
            {
                // Handle render plugin deletion
                string renderPath = _pluginInstaller.GetRenderPath();

                if (_initialSettingsLoader.PathSettings.TryGetValue("Render", out string renderFiles))
                {
                    var files = renderFiles.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var file in files)
                    {
                        string filePath = Path.Combine(renderPath, file.Trim());
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                }

                // Delete the GD3D11 folder inside renderPath
                string gd3d11Folder = Path.Combine(renderPath, "GD3D11");
                if (Directory.Exists(gd3d11Folder))
                {
                    Directory.Delete(gd3d11Folder, true); // true => recursive delete
                }

                // Update UI
                plugin.IsDownloaded = false;
                PluginsListBox.Items.Refresh();
            }
            else if (File.Exists(pluginPath))
            {
                File.Delete(pluginPath);
                plugin.IsDownloaded = false;
                PluginsListBox.Items.Refresh(); // Refresh the UI
            }
        }

        

        



        private async void DownloadScripts_Click(object sender, RoutedEventArgs e)
        {
            DownloadFileButton.IsEnabled = false;
            DownloadProgressBar.Value = 0;
            
            string savePath = string.Empty;
            bool downloadSuccess = false;
            // Set download path


            try
            {
                
                if (string.IsNullOrEmpty(scriptFileId)) return;

                if (string.IsNullOrEmpty(FolderPathTextBox.Text))
                {
                    var result = System.Windows.MessageBox.Show("Vyberte složku s NB",
                          "Directory Required",
                          MessageBoxButton.OK,
                          MessageBoxImage.Warning);

                    if (result == MessageBoxResult.OK)
                    {
                        BrowseFolder_Click(null, null);
                    }

                    if (string.IsNullOrEmpty(FolderPathTextBox.Text)) return;


                }

                // Set download path
                savePath = Path.Combine(FolderPathTextBox.Text, "Skripty.zip");

                // Download the file
                await DownloadScripts(scriptFileId, savePath);


                downloadSuccess = true;
            }
            catch (Exception ex)
            {
                
                System.Windows.MessageBox.Show($"Download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try
                {
                    if (downloadSuccess && File.Exists(savePath))
                    {
                        // Extract to same directory as the ZIP file
                        string extractDir = Path.GetDirectoryName(savePath);

                       string extractedZipFolder = ScriptOperations.ExtractZip(savePath, extractDir);

                        ScriptOperations.MoveFiles(extractDir,extractedZipFolder);

                        // Only delete the ZIP file (extracted files are already moved)
                        ScriptOperations.CleanUp(extractDir,savePath);

                        
                        System.Windows.MessageBox.Show("Skripty nainstalovány!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    
                    System.Windows.MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    DownloadFileButton.IsEnabled = true;
                }
            }
        }




        

        





        private async Task<string> FindLatestScripts()
        {
            try
            {
                

                // Find subfolders matching 'Skripty [date]'
                var skriptyFolderRequest = service.Files.List();
                skriptyFolderRequest.Q = $"name contains 'Skripty ' and '{rootShareID}' in parents and mimeType='application/vnd.google-apps.folder'";
                skriptyFolderRequest.Fields = "files(id, name, createdTime, modifiedTime)";
                var skriptyFolderResult = await skriptyFolderRequest.ExecuteAsync();

                if (skriptyFolderResult.Files.Count == 0)
                {
                    System.Windows.MessageBox.Show("No 'Skripty [date]' folder found.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                var latestSkriptyFolder = skriptyFolderResult.Files
                    .OrderByDescending(f => f.CreatedTimeDateTimeOffset)
                    .First();
                string skriptyFolderId = latestSkriptyFolder.Id;

                // Extract new script version from folder name
                var nameParts = latestSkriptyFolder.Name.Split(' ');
                if (nameParts.Length >= 2)
                    NewScriptVer = nameParts.Last();

                // Find files in the newest 'Skripty' folder
                var fileRequest = service.Files.List();
                fileRequest.Q = $"'{skriptyFolderId}' in parents and mimeType != 'application/vnd.google-apps.folder'";
                fileRequest.Fields = "files(id, name, createdTime, size, modifiedTime)";
                var fileResult = await fileRequest.ExecuteAsync();

                if (fileResult.Files.Count == 0)
                {
                    System.Windows.MessageBox.Show($"No files found in '{latestSkriptyFolder.Name}'.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                var latestFile = fileResult.Files
                    .OrderByDescending(f => f.CreatedTimeDateTimeOffset)
                    .First();

                // Extract new Czech version from file date
                
                NewCzechVer = latestFile.ModifiedTimeDateTimeOffset?.ToString("d.M.yyyy");

                return latestFile.Id;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error searching files: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private async Task DownloadScripts(string fileId, string savePath)
        {
            try
            {
                var getRequest = service.Files.Get(fileId);
                getRequest.Fields = "id,name,size"; // Explicitly request size field
                var file = await getRequest.ExecuteAsync();
                long totalSize = file.Size ?? 0;
                

                Dispatcher.Invoke(() =>
                {
                    
                });

                var request = service.Files.Get(fileId);
                request.MediaDownloader.ChunkSize = 64 * 1024; // 64KB chunks

                request.MediaDownloader.ProgressChanged += (progress) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (progress.Status == DownloadStatus.Downloading)
                        {
                            if (totalSize > 0)
                            {
                                double percentage = (double)progress.BytesDownloaded / totalSize * 100;
                                DownloadProgressBar.Value = percentage;
                                
                            }
                            else
                            {
                                
                            }
                        }
                        else if (progress.Status == DownloadStatus.Completed)
                        {
                            DownloadProgressBar.Value = 100;
                            _initialSettingsLoader.SaveOrUpdateSetting("General","LastScriptVer",NewScriptVer);
                            _initialSettingsLoader.SaveOrUpdateSetting("General", "LastCzechVer", NewCzechVer);
                            
                        }
                    });
                };

                using (var fileStream = new FileStream(savePath, FileMode.Create))
                {
                    await request.DownloadAsync(fileStream);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    DownloadProgressBar.Value = 0;
                    
                });
                throw;
            }
        }
        private void UpdateSwitchCheckboxes()
        {
            iniSettingsPanel.Children.Clear();

            iniSettingsPanel.Children.Add(new TextBlock
            {
                Text = "Ini nastavení",
                FontSize = 24,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 30)
            });

            if (_gothicIniLoader?.switches == null || _gothicIniLoader.switches.Count == 0)
                return;

            foreach (var sw in _gothicIniLoader.switches)
            {
                var grid = new Grid
                {
                    Margin = new Thickness(4)
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // switch width
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // text

                var toggle = new ToggleButton
                {
                    Style = (Style)FindResource("MobileToggleSwitch"),
                    IsChecked = sw.IsChecked,
                    Tag = sw // keep reference
                };

                toggle.Checked += (s, e) => ((SwitchConfig)((ToggleButton)s).Tag).IsChecked = true;
                toggle.Unchecked += (s, e) => ((SwitchConfig)((ToggleButton)s).Tag).IsChecked = false;

                Grid.SetColumn(toggle, 0);
                grid.Children.Add(toggle);

                var textBlock = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(sw.Description) ? sw.Key : $"{sw.Key} ({sw.Description})",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20, 0, 0, 0),
                    FontFamily = new System.Windows.Media.FontFamily("Arial"),
                    
                };

                Grid.SetColumn(textBlock, 1);
                grid.Children.Add(textBlock);

                iniSettingsPanel.Children.Add(grid);
            }

        }

        private void ApplyIniChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_gothicIniLoader != null)
            {
                bool[] switchStates = _gothicIniLoader.switches.Select(s => s.IsChecked).ToArray();
                _gothicIniLoader.ApplySwitches(switchStates);
                System.Windows.MessageBox.Show("Ini nastaveno.");
            }
        }
    }
}