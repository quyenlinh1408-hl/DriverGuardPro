using System;
using System.IO;
using System.Drawing;
using System.Management;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DriverGuardPrototype
{
    public class MainForm : Form
    {
        private const string CurrentVersion = "1.0.4";
        private const string GitHubApiUrl = "https://api.github.com/repos/quyenlinh1408-hl/DriverGuardPro/releases/latest";
        private NotifyIcon trayIcon;
        private ManagementEventWatcher? watcher;
        private string backupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DriverBackup");

        // Cac thanh phan giao dien (UI Controls)
        private Label lblTitle;
        private Label lblStatus;
        private Button btnCheckUpdate;
        private CheckBox chkBlockUpdate;
        private CheckBox chkAutoStart;
        private Button btnBackupNow;
        private Button btnScanErrors;
        private Button btnOpenFolder;
        private ComboBox cboLanguage = null!;
        private Label lblLogTitle = null!;
        private bool isEnglish = false;
        private RichTextBox txtLog;

        public MainForm()
        {
            InitializeUI();
            SetupSystemTray();
            
            if (!Directory.Exists(backupFolder))
            {
                Directory.CreateDirectory(backupFolder);
            }

            // Dong bo trang thai cac nut gat (CheckBox)
            chkBlockUpdate.Checked = IsDriverUpdateLocked();
            chkAutoStart.Checked = IsAutoStartEnabled();

            StartRealTimeMonitor();
            AppendLog("[HE THONG] Driver Guard da san sang bao ve thoi gian thuc.", Color.LightGreen);
        }

        private void InitializeUI()
        {
            // Cau hinh cua so chinh (Dark Mode hien dai)
            this.Text = "Driver Guard - He Thong Bao Ve Driver";
            this.Size = new Size(620, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 35);
            this.ForeColor = Color.White;
            this.Icon = SystemIcons.Shield;

            // Tieu de
            lblTitle = new Label
            {
                Text = "DRIVER GUARD PRO",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 180, 255),
                Location = new Point(20, 15),
                AutoSize = true
            };
                        
            btnCheckUpdate = new Button
            {
                Text = "Update App",
                Location = new Point(462, 16),
                Size = new Size(120, 32),
                BackColor = Color.FromArgb(55, 55, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCheckUpdate.Click += OnCheckUpdate;

            cboLanguage = new ComboBox
            {
                Location = new Point(352, 18),
                Size = new Size(100, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 55),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            cboLanguage.Items.AddRange(new object[] { "VietNam", "English" });
            cboLanguage.SelectedIndex = 0;
            cboLanguage.SelectedIndexChanged += (s, e) =>
            {
                isEnglish = cboLanguage.SelectedIndex == 1;
                ApplyLanguage();
            };


            lblStatus = new Label
            {
                Text = "Trang thai: Dang giam sat xung dot Real-time",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = Color.LightGreen,
                Location = new Point(22, 48),
                AutoSize = true
            };

            // Khu vuc tuy chon bao ve
            chkBlockUpdate = new CheckBox
            {
                Text = "Chan Windows Update tu dong ghi de Driver (Khuyen dung)",
                Font = new Font("Segoe UI", 10),
                Location = new Point(22, 85),
                AutoSize = true
            };
            chkBlockUpdate.Click += OnToggleLockUpdate;

            chkAutoStart = new CheckBox
            {
                Text = "Tu dong khoi dong cung Windows",
                Font = new Font("Segoe UI", 10),
                Location = new Point(22, 115),
                AutoSize = true
            };
            chkAutoStart.Click += OnToggleAutoStart;

            // Cac nut chuc nang chinh
            btnBackupNow = CreateStyledButton("Sao Luu Driver", new Point(22, 155), Color.FromArgb(0, 120, 215));
            btnBackupNow.Click += OnBackupNow;

            btnScanErrors = CreateStyledButton("Quet && Sua Loi", new Point(212, 155), Color.FromArgb(40, 167, 69));
            btnScanErrors.Click += OnScanAndHealAll; 

            btnOpenFolder = CreateStyledButton("Mo Kho Backup", new Point(402, 155), Color.FromArgb(75, 75, 85));
            btnOpenFolder.Click += (s, e) => { if (Directory.Exists(backupFolder)) Process.Start("explorer.exe", backupFolder); };

            // Khung hien thi nhat ky hoat dong (Log)
            lblLogTitle = new Label
            {
                Text = "Nhat ky hoat dong & Canh bao xung dot:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Silver,
                Location = new Point(22, 210),
                AutoSize = true
            };

            txtLog = new RichTextBox
            {
                Location = new Point(22, 232),
                Size = new Size(560, 190),
                BackColor = Color.FromArgb(20, 20, 24),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Consolas", 9.5f),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            // Them tat ca vao Form
            this.Controls.AddRange(new Control[] {
                lblTitle, btnCheckUpdate, cboLanguage, lblStatus, chkBlockUpdate, chkAutoStart,
                btnBackupNow, btnScanErrors, btnOpenFolder, lblLogTitle, txtLog
            });

            ApplyLanguage();

            // Khi bam nut X tren cua so -> Thu nho xuong khay he thong thay vi tat han
            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                    trayIcon.ShowBalloonTip(3000, "Driver Guard van dang chay", "Ung dung da thu nho xuong khay he thong de tiep tuc bao ve.", ToolTipIcon.Info);
                }
            };
        }

        private void ApplyLanguage()
        {
            if (isEnglish)
            {
                this.Text = $"Driver Guard Pro v{CurrentVersion} - Driver Protection System";
                lblStatus.Text = "Status: Monitoring hardware conflicts in Real-time";
                chkBlockUpdate.Text = "Block Windows Update from overwriting Drivers (Recommended)";
                chkAutoStart.Text = "Start automatically with Windows";
                btnBackupNow.Text = "Backup Drivers";
                btnScanErrors.Text = "Scan && Auto-Fix";
                btnOpenFolder.Text = "Open Backup Folder";
                btnCheckUpdate.Text = "Update App";
                lblLogTitle.Text = "Activity Log && Conflict Alerts:";

                if (trayIcon?.ContextMenuStrip != null && trayIcon.ContextMenuStrip.Items.Count >= 3)
                {
                    trayIcon.ContextMenuStrip.Items[0].Text = "Open Control Panel";
                    trayIcon.ContextMenuStrip.Items[2].Text = "Exit Application";
                }
                AppendLog("[SYSTEM] Language switched to English.", Color.Cyan);
            }
            else
            {
                this.Text = $"Driver Guard Pro v{CurrentVersion} - Hệ Thống Bảo Vệ Driver";
                lblStatus.Text = "Trạng thái: Đang giám sát xung đột Real-time";
                chkBlockUpdate.Text = "Chặn Windows Update tự động ghi đè Driver (Khuyên dùng)";
                chkAutoStart.Text = "Tự động khởi động cùng Windows";
                btnBackupNow.Text = "Sao Lưu Driver";
                btnScanErrors.Text = "Quét && Sửa Lỗi";
                btnOpenFolder.Text = "Mở Kho Backup";
                btnCheckUpdate.Text = "Cập Nhật App";
                lblLogTitle.Text = "Nhật ký hoạt động && Cảnh báo xung đột:";

                if (trayIcon?.ContextMenuStrip != null && trayIcon.ContextMenuStrip.Items.Count >= 3)
                {
                    trayIcon.ContextMenuStrip.Items[0].Text = "Mở bảng điều khiển";
                    trayIcon.ContextMenuStrip.Items[2].Text = "Thoát hẳn chương trình";
                }
                AppendLog("[HỆ THỐNG] Đã chuyển ngôn ngữ sang Tiếng Việt.", Color.Cyan);
            }
        }

        private Button CreateStyledButton(string text, Point location, Color bgColor)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = new Size(180, 38),
                BackColor = bgColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
        }

        private void SetupSystemTray()
        {
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Mo bang dieu khien", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Thoat han chuong trinh", null, (s, e) =>
            {
                watcher?.Stop();
                watcher?.Dispose();
                trayIcon.Visible = false;
                Environment.Exit(0);
            });

            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Text = "Driver Guard - Nhan dup de mo giao dien",
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; };
        }

        private void AppendLog(string message, Color color)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AppendLog(message, color)));
                return;
            }
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = color;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            txtLog.ScrollToCaret();
        }

        private void StartRealTimeMonitor()
        {
            try
            {
                WqlEventQuery query = new WqlEventQuery("__InstanceOperationEvent", TimeSpan.FromSeconds(2), "TargetInstance ISA 'Win32_PnPEntity'");
                watcher = new ManagementEventWatcher(query);
                watcher.EventArrived += OnHardwareChanged;
                watcher.Start();
            }
            catch (Exception ex)
            {
                AppendLog($"[LOI] Khong the bat giam sat (Hay chay quyen Admin): {ex.Message}", Color.Red);
            }
        }

        private void OnHardwareChanged(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            string eventType = e.NewEvent.ClassPath.ClassName;
            string deviceName = targetInstance["Name"]?.ToString() ?? "Thiet bi khong xac dinh";
            var errorCodeObj = targetInstance["ConfigManagerErrorCode"];
            int errorCode = errorCodeObj != null ? Convert.ToInt32(errorCodeObj) : 0;

            if (eventType == "__InstanceCreationEvent" || eventType == "__InstanceModificationEvent")
            {
                if (errorCode != 0)
                {
                    AppendLog($"[CANH BAO XUNG DOT] {deviceName} (Ma loi: Code {errorCode})", Color.OrangeRed);
                    trayIcon.ShowBalloonTip(4000, "Phat hien xung dot Driver!", $"Thiet bi {deviceName} bi loi Code {errorCode}. Dang tu phuc hoi...", ToolTipIcon.Warning);
                    RestoreDriverFromBackup();
                }
                else if (eventType == "__InstanceCreationEvent")
                {
                    AppendLog($"[KET NOI MOI] {deviceName} - Trang thai an toan.", Color.Cyan);
                }
            }
        }

        private void RestoreDriverFromBackup()
        {
            Task.Run(() =>
            {
                try
                {
                    AppendLog("[TU DONG SUA] Dang nap lai driver sach tu kho Backup...", Color.Yellow);
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "pnputil.exe",
                        Arguments = $"/add-driver \"{backupFolder}\\*.inf\" /subdirs /install",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (Process? process = Process.Start(psi))
                    {
                        process?.WaitForExit();
                        AppendLog("[HOAN TAT] Da khoi phuc driver on dinh thanh cong!", Color.LightGreen);
                    }
}
catch (Exception ex)
{
    AppendLog($"[LOI KHOI PHUC] {ex.Message}", Color.Red);
}
});
}

private void OnBackupNow(object? sender, EventArgs e)
{
    btnBackupNow.Enabled = false;
    AppendLog("[SAO LUU] Dang trich xuat toan bo driver ben thu 3...", Color.Yellow);
    Task.Run(() =>
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = $"/export-driver * \"{backupFolder}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process? process = Process.Start(psi))
            {
                process?.WaitForExit();
                AppendLog("[SAO LUU XONG] Toan bo driver da duoc luu an toan vao kho Backup!", Color.LightGreen);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[LOI SAO LUU] {ex.Message}", Color.Red);
        }
        finally
        {
            this.Invoke(new Action(() => btnBackupNow.Enabled = true));
        }
    });
}

private void OnScanErrors(object? sender, EventArgs e)
{
    AppendLog("[QUET HE THONG] Dang kiem tra toan bo thiet bi...", Color.White);
    Task.Run(() =>
    {
        int errorCount = 0;
        ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
        foreach (ManagementObject device in searcher.Get())
        {
            var err = device["ConfigManagerErrorCode"];
            if (err != null && Convert.ToInt32(err) != 0)
            {
                errorCount++;
                AppendLog($" -> [LOI] {device["Name"]} (Code {err})", Color.OrangeRed);
            }
        }
        if (errorCount == 0)
            AppendLog("[KET QUA] Tuyet voi! Khong co driver nao bi xung dot.", Color.LightGreen);
        else
            AppendLog($"[KET QUA] Phat hien {errorCount} thiet bi dang gap su co!", Color.OrangeRed);
    });
}

private void OnToggleLockUpdate(object? sender, EventArgs e)
{
    try
    {
        bool targetState = chkBlockUpdate.Checked;
        using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"))
        {
            key.SetValue("ExcludeWUDriversInQualityUpdate", targetState ? 1 : 0, RegistryValueKind.DWord);
        }
        AppendLog(targetState ? "[CHINH SACH] Da KHOA Windows Update ghi de driver." : "[CHINH SACH] Da MO KHOA cap nhat driver.", Color.Cyan);
    }
    catch (Exception ex)
    {
        AppendLog($"[LOI QUYEN] Khong the sua Registry: {ex.Message}", Color.Red);
    }
}

private bool IsDriverUpdateLocked()
{
    try
    {
        using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"))
        {
            object? val = key?.GetValue("ExcludeWUDriversInQualityUpdate");
            return val != null && Convert.ToInt32(val) == 1;
        }
    }
    catch { return false; }
}

private void OnToggleAutoStart(object? sender, EventArgs e)
{
    try
    {
        string appName = "DriverGuardPro";
        string exePath = Application.ExecutablePath;
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)!)
        {
            if (chkAutoStart.Checked)
                key.SetValue(appName, $"\"{exePath}\"");
            else
                key.DeleteValue(appName, false);
        }
        AppendLog(chkAutoStart.Checked ? "[HE THONG] Da bat tu dong khoi dong cung Windows." : "[HE THONG] Da tat tu dong khoi dong.", Color.Cyan);
    }
    catch (Exception ex)
    {
        AppendLog($"[LOI] Khong the cai dat khoi dong cung Windows: {ex.Message}", Color.Red);
    }
}

private bool IsAutoStartEnabled()
{
    try
    {
        using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
        {
            return key?.GetValue("DriverGuardPro") != null;
        }
    }
    catch { return false; }
}
private void OnScanAndHealAll(object? sender, EventArgs e)
{
    btnScanErrors.Enabled = false;
    AppendLog("=== BAT DAU QUY TRINH QUET & SUA LOI TU DONG ===", Color.Cyan);

    Task.Run(async () =>
    {
        try
        {
            AppendLog("[1/4] Dang toi uu cau hinh nguon & don sach Registry xung dot...", Color.Yellow);
            using (RegistryKey? powerKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", true))
            {
                powerKey?.SetValue("HiberbootEnabled", 0, RegistryValueKind.DWord);
            }

            string[] classGuids = {
                "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}",
                "{4d36e972-e325-11ce-bfc1-08002be10318}"
            };
            foreach (string guid in classGuids)
            {
                using (RegistryKey? classKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Control\Class\{guid}", true))
                {
                    if (classKey != null)
                    {
                        if (classKey.GetValue("UpperFilters") != null) classKey.DeleteValue("UpperFilters", false);
                        if (classKey.GetValue("LowerFilters") != null) classKey.DeleteValue("LowerFilters", false);
                    }
                }
            }

            string psCommand = "Get-PnpDevice -Class Net,Bluetooth -ErrorAction SilentlyContinue | ForEach-Object { " +
                               "$path = 'HKLM:\\SYSTEM\\CurrentControlSet\\Enum\\' + $_.InstanceId + '\\Device Parameters'; " +
                               "if (Test-Path $path) { Set-ItemProperty -Path $path -Name 'EnhancedPowerManagementEnabled' -Value 0 -ErrorAction SilentlyContinue } }";
            RunHiddenCommand("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"");

            AppendLog("[2/4] Dang quet phan cung va phuc hoi tu kho du phong...", Color.Yellow);
            RunHiddenCommand("pnputil.exe", "/scan-devices");
            if (Directory.Exists(backupFolder) && Directory.GetDirectories(backupFolder).Length > 0)
            {
                RunHiddenCommand("pnputil.exe", $"/add-driver \"{backupFolder}\\*.inf\" /subdirs /install /force");
            }

            var initialErrors = GetBrokenDevices();
            foreach (var dev in initialErrors)
            {
                if (!string.IsNullOrEmpty(dev.DeviceId))
                {
                    RunHiddenCommand("pnputil.exe", $"/disable-device \"{dev.DeviceId}\"");
                    await Task.Delay(1200);
                    RunHiddenCommand("pnputil.exe", $"/enable-device \"{dev.DeviceId}\"");
                }
            }
            RunHiddenCommand("net.exe", "stop bthserv");
            RunHiddenCommand("net.exe", "start bthserv");
            await Task.Delay(2000);

            var remainingErrors = GetBrokenDevices();
            if (remainingErrors.Count > 0)
            {
                AppendLog($"[3/4] Phat hien {remainingErrors.Count} thiet bi thieu/loi driver. Dang tu dong tai ban chuan...", Color.OrangeRed);

                if (await IsInternetAvailableAsync())
                {
                    foreach (var dev in remainingErrors)
                    {
                        if (string.IsNullOrEmpty(dev.HardwareId)) continue;
                        AppendLog($" -> Dang tai & cai dat ngam cho: {dev.Name}...", Color.Cyan);
                        bool ok = await DownloadAndInstallFromCatalogAsync(dev.HardwareId, dev.DeviceId);
                        AppendLog(ok
                            ? $" -> Da nap xong driver cho {dev.Name}."
                            : $" -> Khong tim thay goi tu dong cho {dev.Name}.",
                            ok ? Color.LightGreen : Color.Yellow);
                    }
                    RunHiddenCommand("pnputil.exe", "/scan-devices");
                    await Task.Delay(2000);
                }
                else
                {
                    AppendLog("[CANH BAO MANG] May dang mat Internet nen khong the tai them driver moi.", Color.Red);
                }
            }
            else
            {
                AppendLog("[3/4] Khong can tai them driver ngoai.", Color.LightGreen);
            }

            AppendLog("[4/4] Dang quet kiem tra trang thai an toan toan he thong...", Color.Yellow);
            var finalCheck = GetBrokenDevices();
            if (finalCheck.Count == 0)
            {
                AppendLog("[AN TOAN 100%] Toan bo driver tren may da hoat dong on dinh va sach loi!", Color.LightGreen);
            }
            else
            {
                foreach (var errDev in finalCheck)
                {
                    AppendLog($"[LUU Y] {errDev.Name} van con ma loi Code {errDev.ErrorCode} (Hay khoi dong lai may 1 lan de hoan tat).", Color.OrangeRed);
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[LOI HE THONG] {ex.Message}", Color.Red);
        }
        finally
        {
            this.Invoke(new Action(() => btnScanErrors.Enabled = true));
        }
    });
}

private System.Collections.Generic.List<(string Name, string HardwareId, string DeviceId, int ErrorCode)> GetBrokenDevices()
{
    var list = new System.Collections.Generic.List<(string Name, string HardwareId, string DeviceId, int ErrorCode)>();
    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity"))
    {
        foreach (ManagementObject dev in searcher.Get())
        {
            var errObj = dev["ConfigManagerErrorCode"];
            int errCode = errObj != null ? Convert.ToInt32(errObj) : 0;
            string name = dev["Name"]?.ToString() ?? "Unknown Device";

            if ((errCode != 0 && errCode != 22) || name.Contains("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                string devId = dev["DeviceID"]?.ToString() ?? "";
                string[]? hwIds = dev["HardwareID"] as string[];
                string targetHwId = (hwIds != null && hwIds.Length > 0) ? hwIds[0] : "";
                list.Add((name, targetHwId, devId, errCode));
            }
        }
    }
    return list;
}

private async Task<bool> DownloadAndInstallFromCatalogAsync(string hardwareId, string deviceId)
{
    try
    {
        using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        string searchUrl = $"https://microsoft.com{Uri.EscapeDataString(hardwareId)}";
        string searchHtml = await client.GetStringAsync(searchUrl);

        Match idMatch = Regex.Match(searchHtml, @"id=""([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})_link""", RegexOptions.IgnoreCase);
        if (!idMatch.Success) return false;

        string updateId = idMatch.Groups[1].Value;
        string postPayload = $"updateIDs={Uri.EscapeDataString($"[{{\"size\":0,\"languages\":\"\",\"uidInfo\":\"{updateId}\",\"updateID\":\"{updateId}\"}}]")}&updateIDsBlockedForImport=&wsusApiPresent=&contentSyncApiPresent=&sku=&serverName=&ssl=&portNumber=&version=";
        var content = new StringContent(postPayload, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

        HttpResponseMessage dialogResponse = await client.PostAsync("https://microsoft.com", content);
        string dialogHtml = await dialogResponse.Content.ReadAsStringAsync();

        Match cabUrlMatch = Regex.Match(dialogHtml, @"https?://[^\s'""]+\.cab", RegexOptions.IgnoreCase);
        if (!cabUrlMatch.Success) return false;

        string tempDir = Path.Combine(Path.GetTempPath(), "DriverGuard_Temp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string cabFilePath = Path.Combine(tempDir, "driver.cab");
        string extractDir = Path.Combine(tempDir, "Extracted");
        Directory.CreateDirectory(extractDir);

        byte[] cabBytes = await client.GetByteArrayAsync(cabUrlMatch.Value);
        await File.WriteAllBytesAsync(cabFilePath, cabBytes);

        RunHiddenCommand("expand.exe", $"-F:* \"{cabFilePath}\" \"{extractDir}\"");
        RunHiddenCommand("pnputil.exe", $"/add-driver \"{extractDir}\\*.inf\" /subdirs /install /force");

        string savedFolder = Path.Combine(backupFolder, $"AutoSaved_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(savedFolder);
        foreach (string file in Directory.GetFiles(extractDir, "*.*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(savedFolder, Path.GetFileName(file)), true);
        }

        if (!string.IsNullOrEmpty(deviceId))
        {
            RunHiddenCommand("pnputil.exe", $"/restart-device \"{deviceId}\"");
        }

        try { Directory.Delete(tempDir, true); } catch { }
        return true;
    }
    catch
    {
        return false;
    }
}

private async Task<bool> IsInternetAvailableAsync()
{
    try
    {
        using HttpClient pingClient = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        using var response = await pingClient.GetAsync("https://microsoft.com");
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}

private void RunHiddenCommand(string fileName, string arguments)
{
    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    using (Process? process = Process.Start(psi))
    {
        process?.WaitForExit();
    }
}

private void DetectAndDownloadMissingDrivers()
{
    AppendLog("[HO TRO TAI DRIVER] Dang kiem tra thiet bi bi mat hoac thieu driver...", Color.Cyan);

    // 1. Ep Windows quet lai toan bo phan cung vat ly (goi lai card Wifi/Bluetooth bi an)
    RunHiddenCommand("pnputil.exe", "/scan-devices");
    System.Threading.Thread.Sleep(2000);

    // 2. Lay thong tin Hang san xuat va Model laptop cua nguoi dung
    string manufacturer = "Unknown";
    string model = "Unknown";
    ManagementObjectSearcher sysSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
    foreach (ManagementObject sys in sysSearcher.Get())
    {
        manufacturer = sys["Manufacturer"]?.ToString()?.Trim() ?? "Unknown";
        model = sys["Model"]?.ToString()?.Trim() ?? "Unknown";
    }

    // 3. Quet tim cac thiet bi dang thieu driver (Code 28) hoac chua nhan dien (Unknown Device)
    bool hasMissingDriver = false;
    string missingHardwareId = "";
    string missingDeviceName = "";

    ManagementObjectSearcher devSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");
    foreach (ManagementObject dev in devSearcher.Get())
    {
        string name = dev["Name"]?.ToString() ?? "";
        var errObj = dev["ConfigManagerErrorCode"];
        int errCode = errObj != null ? Convert.ToInt32(errObj) : 0;

        // Code 28 = Chua cai driver | Hoac ten thiet bi la Unknown / Network Controller / Bluetooth
        if (errCode == 28 || name.Contains("Unknown", StringComparison.OrdinalIgnoreCase) || 
           (errCode != 0 && (name.Contains("Wireless", StringComparison.OrdinalIgnoreCase) || 
                             name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) || 
                             name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Network", StringComparison.OrdinalIgnoreCase))))
        {
            hasMissingDriver = true;
            missingDeviceName = name;
            string[]? hwIds = dev["HardwareID"] as string[];
            if (hwIds != null && hwIds.Length > 0)
            {
                missingHardwareId = hwIds[0]; // Lay ma phan cung chinh xac nhat (VD: PCI\VEN_8086&DEV_...)
            }
            AppendLog($" -> Phat hien thieu driver: {name} (ID: {missingHardwareId})", Color.OrangeRed);
            break;
        }
    }

    if (!hasMissingDriver)
    {
        AppendLog("[AN TOAN] Tat ca thiet bi phan cung deu da co driver day du!", Color.LightGreen);
        return;
    }

    // 4. Hien thi hop thoai hoi nguoi dung co muon mo trang tai driver chinh hang khong
    this.Invoke(new Action(() =>
    {
        string msg = $"Phat hien thiet bi '{missingDeviceName}' dang bi mat driver!\n\n" +
                     $"Thong tin may: {manufacturer} - {model}\n" +
                     $"Ma phan cung: {missingHardwareId}\n\n" +
                     $"Ban co muon mo trang ho tro chinh hang (hoac kho Microsoft Update) de tai lai driver chuan khong?\n" +
                     $"(Luu y: Neu laptop dang mat Wifi, hay cam day mang LAN hoac chia se mang qua cap USB/Bluetooth tu dien thoai truoc khi bam Yes).";

        DialogResult result = MessageBox.Show(msg, "Ho Tro Tai Lai Driver Chuan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
        {
            OpenOfficialDriverPage(manufacturer, model, missingHardwareId);
        }
    }));
}

private void OpenOfficialDriverPage(string manufacturer, string model, string hardwareId)
{
    string url = "";
    string mfgLower = manufacturer.ToLower();

    // Uu tien mo trang ho tro chinh hang theo ten hang laptop
    if (mfgLower.Contains("hp") || mfgLower.Contains("hewlett"))
        url = $"https://hp.com";
    else if (mfgLower.Contains("dell"))
        url = $"https://dell.com";
    else if (mfgLower.Contains("asus"))
        url = $"https://asus.com";
    else if (mfgLower.Contains("lenovo"))
        url = $"https://lenovo.com";
    else if (mfgLower.Contains("acer"))
        url = $"https://acer.com";
    else if (mfgLower.Contains("msi"))
        url = $"https://msi.com";
    else if (!string.IsNullOrEmpty(hardwareId))
    {
        // Neu khong xac dinh duoc hang laptop -> Tra cuu thang Hardware ID tren kho Microsoft Update Catalog
        string encodedId = Uri.EscapeDataString(hardwareId);
        url = $"https://microsoft.com{encodedId}";
    }
    else
    {
        url = "https://microsoft.com";
    }

    try
    {
        // Copy san Model may vao Clipboard de nguoi dung chi can dan (Ctrl+V) vao o tim kiem tren web
        Clipboard.SetText(model);
        AppendLog($"[DA COPY MODEL] Da sao chep '{model}' vao bo nho tam. Hay dan (Ctrl+V) tren trang web vua mo!", Color.LightGreen);
        
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
    catch (Exception ex)
    {
        AppendLog($"[LOI MO TRINH DUYET] {ex.Message}", Color.Red);
    }
}
private async void OnCheckUpdate(object? sender, EventArgs e)
{
    AppendLog($"[CAP NHAT] Dang kiem tra phien ban moi (Hien tai: v{CurrentVersion})...", Color.Cyan);

    try
    {
        using HttpClient client = new HttpClient();
        // GitHub API bat buoc phai co User-Agent
        client.DefaultRequestHeaders.Add("User-Agent", "DriverGuardPro-Updater");

        string jsonResponse = await client.GetStringAsync(GitHubApiUrl);
        using JsonDocument doc = JsonDocument.Parse(jsonResponse);
        JsonElement root = doc.RootElement;

        // Lay ten tag phien ban tren GitHub (VD: "v1.1.0" -> cat chu "v" thanh "1.1.0")
        string latestTag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? CurrentVersion;
        string releaseNotes = root.GetProperty("body").GetString() ?? "Cai thien hieu nang va sua loi.";

        if (Version.TryParse(latestTag, out Version? latestVer) && 
            Version.TryParse(CurrentVersion, out Version? currentVer) && 
            latestVer > currentVer)
        {
            // Tim link tai file .exe trong danh sach assets cua Release
            string? downloadUrl = null;
            foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
            {
                string? fileName = asset.GetProperty("name").GetString();
                if (fileName != null && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                AppendLog("[CAP NHAT] Co ban moi nhung chua tim thay file .exe dinh kem.", Color.Yellow);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                $"Da co phien ban moi: v{latestTag} (Ban dang dung: v{CurrentVersion})\n\n" +
                $"Noi dung cap nhat:\n{releaseNotes}\n\n" +
                $"Ban co muon tai va cap nhat ngay bay gio khong?",
                "Cap Nhat Phan Mem",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information
            );

            if (confirm == DialogResult.Yes)
            {
                await DownloadAndApplyUpdate(client, downloadUrl);
            }
        }
        else
        {
            AppendLog($"[CAP NHAT] Ban dang su dung phien ban moi nhat (v{CurrentVersion})!", Color.LightGreen);
            MessageBox.Show($"Ban dang dung phien ban moi nhat (v{CurrentVersion}).", "Thong bao", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
    catch (Exception ex)
    {
        AppendLog($"[LOI CAP NHAT] Khong the ket noi may chu cap nhat: {ex.Message}", Color.Red);
    }
}

private async Task DownloadAndApplyUpdate(HttpClient client, string downloadUrl)
{
    try
    {
        AppendLog("[CAP NHAT] Dang tai xuong phien ban moi, vui long khong tat ung dung...", Color.Yellow);

        string currentExePath = Application.ExecutablePath;
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string newExePath = Path.Combine(appDir, "DriverGuard_Update.exe");
        string batPath = Path.Combine(appDir, "update.bat");

        // Tai file .exe moi ve thu muc hien tai
        byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(newExePath, fileBytes);

        AppendLog("[CAP NHAT] Tai xong! Dang khoi dong lai de ap dung ban moi...", Color.LightGreen);

        // Tao script .bat de doi app tat -> xoa ban cu -> doi ten ban moi -> mo lai app
        string batScript = $@"
@echo off
timeout /t 2 /nobreak > nul
del /f /q ""{currentExePath}""
move /y ""{newExePath}"" ""{currentExePath}""
start """" ""{currentExePath}""
del ""%~f0""
";
        await File.WriteAllTextAsync(batPath, batScript);

        // Chay file .bat ngam va thoat ung dung hien tai
        Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        watcher?.Stop();
        watcher?.Dispose();
        trayIcon.Visible = false;
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        AppendLog($"[LOI CAP NHAT] Qua trinh tai that bai: {ex.Message}", Color.Red);
    }
}
}
}