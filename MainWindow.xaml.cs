using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;

namespace ModInstaller
{
    public partial class MainWindow : Window
    {
        private string gameFolderPath = "";
        private string backupFolder = "";
        private string modDownloadUrl = "https://github.com/SkipperSkipTR/TGYH-TR/releases/download/";
        private string sevenZipEmbedded = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7za.exe"); // Embedded 7-Zip executable
        private string sevenZipDownloadUrl = "https://www.7-zip.org/a/7za920.zip";
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
                    string tempZipPath = Path.Combine(Path.GetTempPath(), "7za.zip");
                    WebClient webClient = new WebClient();
                    webClient.DownloadFile(sevenZipDownloadUrl, tempZipPath);

                    ProcessStartInfo extractProcess = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"Expand-Archive -Path '{tempZipPath}' -DestinationPath '{AppDomain.CurrentDomain.BaseDirectory}' -Force",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (Process process = Process.Start(extractProcess))
                    {
                        process.WaitForExit();
                    }

                    if (!File.Exists(sevenZipEmbedded))
                    {
                        MessageBox.Show("7za.exe çıktısı alınamadı. Lütfen manuel olarak indirin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("7za.exe indirilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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
                InstallButton.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("Oyun versiyonu algılanamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DownloadAndInstallMod_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(gameVersion))
            {
                MessageBox.Show("Oyun versiyonu algılanamadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string modFileUrl = modDownloadUrl + gameVersion + "/mod.zip";
            string modFilePath = Path.Combine(Path.GetTempPath(), "mod.zip");

            try
            {
                WebClient webClient = new WebClient();
                webClient.DownloadFile(modFileUrl, modFilePath);
                BackupAndExtract(modFilePath);
            }
            catch (WebException ex)
            {
                MessageBox.Show("Mod dosyasını indirirken bir hata oluştu. Oyunun bu sürümü için yama yapılmamış olabilir.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackupAndExtract(string modFilePath)
        {
            backupFolder = Path.Combine(gameFolderPath, "Yedek");
            Directory.CreateDirectory(backupFolder);

            string tempExtractPath = Path.Combine(Path.GetTempPath(), "mod_extract");
            Directory.CreateDirectory(tempExtractPath);

            ProcessStartInfo extractProcess = new ProcessStartInfo
            {
                FileName = sevenZipEmbedded,
                Arguments = "x \"" + modFilePath + "\" -o\"" + tempExtractPath + "\" -y",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(extractProcess))
            {
                process.WaitForExit();
            }

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

            ProcessStartInfo installProcess = new ProcessStartInfo
            {
                FileName = sevenZipEmbedded,
                Arguments = "x \"" + modFilePath + "\" -o\"" + gameFolderPath + "\" -y",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(installProcess))
            {
                process.WaitForExit();
            }

            MessageBox.Show("Yama başarıyla yüklendi!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            CheckModStatus();
        }

        private void CheckModStatus()
        {
            string modFile = Path.Combine(gameFolderPath, "Yedek");
            if (Directory.Exists(modFile))
            {
                InstallButton.IsEnabled = false;
                UninstallButton.IsEnabled = true;
            }
            else
            {
                InstallButton.IsEnabled = true;
                UninstallButton.IsEnabled = false;
            }
        }

        private void UninstallMod_Click(object sender, RoutedEventArgs e)
        {
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
