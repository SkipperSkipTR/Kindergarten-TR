using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ModInstaller
{
    public partial class MainWindow : Window
    {
        private string gameFolderPath = "";
        private string backupFolder = "";
        private string modDownloadUrl = "https://github.com/SkipperSkipTR/TGYH-TR/releases/download/";
        private string sevenZipEmbedded = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7za.exe"); // Embedded 7-Zip executable
        private string sevenZipDownloadUrl = "https://github.com/SkipperSkipTR/TGYH-TR/releases/download/Kurulum/7za.exe";
        private string gameVersion = "";

        public MainWindow()
        {
            InitializeComponent();
            InstallButton.IsEnabled = false;
            UninstallButton.IsEnabled = false;
            EnsureSevenZipExists();
        }

        private void EnsureSevenZipExists()
        {
            if (!File.Exists(sevenZipEmbedded))
            {
                MessageBox.Show("7za.exe bulunamadı. Şimdi indirilecek. Tamam tuşuna basın.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
                try
                {
                    WebClient webClient = new WebClient();
                    webClient.DownloadFile(sevenZipDownloadUrl, sevenZipEmbedded);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error downloading 7za.exe: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SelectGameFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Oyunun .exe Dosyası|Thank Goodness You're Here!.exe",
                Title = "Oyun Klasörünü Seçin"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                gameFolderPath = Path.GetDirectoryName(openFileDialog.FileName);
                DetectGameVersion();
                CheckModStatus();
            }
        }

        private void DetectGameVersion()
        {
            string[] versionFiles = Directory.GetFiles(gameFolderPath, "VERSION *.txt");
            if (versionFiles.Length == 0)
            {
                MessageBox.Show("Versiyon dosyası bulunamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string versionFile = versionFiles[0];
            string versionPattern = @"VERSION (\d+\.\d+\.\d+)";
            Match match = Regex.Match(Path.GetFileName(versionFile), versionPattern);

            if (match.Success)
            {
                gameVersion = match.Groups[1].Value;
                StatusText.Text = "Durum: Oyun versiyonu: " + gameVersion + "! İşlem bekleniyor...";
                InstallButton.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("Oyun versiyonu algılanamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DownloadAndInstallMod_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Durum: İndirme başlatılıyor...";
            if (string.IsNullOrEmpty(gameVersion))
            {
                MessageBox.Show("Oyun versiyonu algılanamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string modFileUrl = modDownloadUrl + gameVersion + "/mod.zip";
            string modFilePath = Path.Combine(Path.GetTempPath(), "mod.zip");

            WebClient webClient = new WebClient();
            webClient.DownloadProgressChanged += (s, ev) =>
            {
                Dispatcher.InvokeAsync(() => ProgressBar.Value = ev.ProgressPercentage);
            };
            webClient.DownloadFileCompleted += async (s, ev) =>
            {
                if (ev.Error == null)
                {
                    await BackupAndExtractAsync(modFilePath);
                }
                else
                {
                    MessageBox.Show("Yama dosyası indirilirken hata oluştu. Versiyonunuzla (" + gameVersion + ") uyumlu yama dosyası olmayabilir.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            webClient.DownloadFileAsync(new Uri(modFileUrl), modFilePath);
        }

        private async Task BackupAndExtractAsync(string modFilePath)
        {
            StatusText.Text = "Durum: Sıkıştırılmış dosyadan çıkarma işlemi yapılıyor...";
            backupFolder = Path.Combine(gameFolderPath, "Yedek");
            Directory.CreateDirectory(backupFolder);

            string tempExtractPath = Path.Combine(Path.GetTempPath(), "mod_extract");
            Directory.CreateDirectory(tempExtractPath);

            // Extract the zip file only once to the temporary folder
            await ExtractWithProgressAsync(modFilePath, tempExtractPath);

            // Backup existing files in the game folder
            foreach (string file in Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(tempExtractPath.Length + 1);
                string originalFile = Path.Combine(gameFolderPath, relativePath);
                string backupFile = Path.Combine(backupFolder, relativePath);

                if (File.Exists(originalFile))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backupFile));
                    File.Copy(originalFile, backupFile, true);
                }
            }

            // Copy the extracted files from the temporary folder to the game folder
            StatusText.Text = "Durum: Çıkarılan dosyalar kopyalanıyor...";
            foreach (string file in Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(tempExtractPath.Length + 1);
                string destinationFile = Path.Combine(gameFolderPath, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                File.Copy(file, destinationFile, true);
            }

            MessageBox.Show("Yama başarıyla yüklendi!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            CheckModStatus();
        }

        private async Task ExtractWithProgressAsync(string archivePath, string destinationPath)
        {
            await Task.Run(() =>
            {
                ProcessStartInfo extractProcess = new ProcessStartInfo
                {
                    FileName = sevenZipEmbedded,
                    Arguments = $"x \"{archivePath}\" -o\"{destinationPath}\" -y -bsp1", // Add -bsp1 flag
                    RedirectStandardOutput = true, // Read progress from StandardOutput
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = extractProcess, EnableRaisingEvents = true })
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            Match match = Regex.Match(e.Data, "([0-9]+)%");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int percentage))
                            {
                                Dispatcher.InvokeAsync(() => ProgressBar.Value = percentage);
                            }
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine(); // Start reading output in real-time
                    process.WaitForExit();
                }
            });
        }

        private void CheckModStatus()
        {
            
            string modFile = Path.Combine(gameFolderPath, "Yedek");
            if (Directory.Exists(modFile))
            {
                StatusText.Text = "Durum: Yama yüklü. İşlem bekleniyor...";
                InstallButton.IsEnabled = false;
                UninstallButton.IsEnabled = true;
            }
            else
            {
                StatusText.Text = "Durum: Yama yüklü değil. İşlem bekleniyor...";
                InstallButton.IsEnabled = true;
                UninstallButton.IsEnabled = false;
            }
        }

        private void UninstallMod_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Durum: Yama siliniyor...";
            backupFolder = Path.Combine(gameFolderPath, "Yedek");
            if (Directory.Exists(backupFolder))
            {
                foreach (string file in Directory.GetFiles(backupFolder, "*", SearchOption.AllDirectories))
                {
                    string relativePath = file.Substring(backupFolder.Length + 1);
                    string originalFile = Path.Combine(gameFolderPath, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(originalFile));
                    File.Copy(file, originalFile, true);
                }

                Directory.Delete(backupFolder, true);
                MessageBox.Show("Yama başarıyla silindi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Yedek dosyalar bulunamadı. Silme işlemi devam edemiyor.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            CheckModStatus();
        }
    }
}
