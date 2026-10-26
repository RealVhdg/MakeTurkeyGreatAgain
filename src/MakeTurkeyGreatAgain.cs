using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MakeTurkeyGreatAgain
{
    public class ServerProfile
    {
        public static string DefaultRemoteIp = "205.237.110.254";
        public static int DefaultRemotePort = 443;
        public static string DefaultPublicKey = "IG6lxrx_xkGOoBuo3DC8sgQoIFmqVgznIr0qOuNO0w4";
        public static string DefaultShortId = "cd2d53510b4df2a8";
        public static string DefaultUuid = "e75ad2eb-6c1f-45ce-8023-f5f10875df66";
        public static string DefaultSni = "zsit.eu-cdn294.com";
        public static int LocalSocksPort = 10808;
        public static int LocalHttpPort = 10809;

        public static void LoadFromFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    string ip = ExtractJsonValue(content, "remote_server_ip");
                    if (!string.IsNullOrEmpty(ip)) DefaultRemoteIp = ip;

                    string pbk = ExtractJsonValue(content, "public_key");
                    if (!string.IsNullOrEmpty(pbk)) DefaultPublicKey = pbk;

                    string sid = ExtractJsonValue(content, "short_id");
                    if (!string.IsNullOrEmpty(sid)) DefaultShortId = sid;

                    string uuid = ExtractJsonValue(content, "uuid");
                    if (!string.IsNullOrEmpty(uuid)) DefaultUuid = uuid;

                    string sni = ExtractJsonValue(content, "sni");
                    if (!string.IsNullOrEmpty(sni)) DefaultSni = sni;
                }
            }
            catch { }
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string pattern = "\"" + key + "\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx == -1) return null;
            int colonIdx = json.IndexOf(':', idx);
            if (colonIdx == -1) return null;
            int startQuote = json.IndexOf('"', colonIdx);
            if (startQuote == -1) return null;
            int endQuote = json.IndexOf('"', startQuote + 1);
            if (endQuote == -1) return null;
            return json.Substring(startQuote + 1, endQuote - startQuote - 1).Trim();
        }
    }

    public class VlessConfig
    {
        public string Uuid { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string Flow { get; set; }
        public string Security { get; set; }
        public string Sni { get; set; }
        public string Fingerprint { get; set; }
        public string PublicKey { get; set; }
        public string ShortId { get; set; }
        public string Remark { get; set; }

        public static string AutoFixLink(string rawUri, string remoteIp)
        {
            if (string.IsNullOrEmpty(rawUri))
            {
                return string.Format(
                    "vless://{0}@{1}:443?encryption=none&flow=xtls-rprx-vision&security=reality&sni={2}&fp=chrome&pbk={3}&sid={4}#MTGA-Node",
                    ServerProfile.DefaultUuid,
                    !string.IsNullOrEmpty(remoteIp) ? remoteIp.Trim() : ServerProfile.DefaultRemoteIp,
                    ServerProfile.DefaultSni,
                    ServerProfile.DefaultPublicKey,
                    ServerProfile.DefaultShortId
                );
            }

            rawUri = rawUri.Trim();

            string targetIp = !string.IsNullOrEmpty(remoteIp) ? remoteIp.Trim() : ServerProfile.DefaultRemoteIp;
            rawUri = rawUri.Replace("@127.0.0.1", "@" + targetIp);
            rawUri = rawUri.Replace("@localhost", "@" + targetIp);

            if (rawUri.Contains("pbk=PUBLIC_KEY") || rawUri.Contains("pbk=&") || rawUri.EndsWith("pbk="))
            {
                rawUri = rawUri.Replace("pbk=PUBLIC_KEY", "pbk=" + ServerProfile.DefaultPublicKey);
            }
            else if (!rawUri.Contains("pbk="))
            {
                rawUri += "&pbk=" + ServerProfile.DefaultPublicKey;
            }

            if (rawUri.Contains("sid=SHORT_ID") || rawUri.Contains("sid=&") || rawUri.EndsWith("sid="))
            {
                rawUri = rawUri.Replace("sid=SHORT_ID", "sid=" + ServerProfile.DefaultShortId);
            }
            else if (!rawUri.Contains("sid="))
            {
                rawUri += "&sid=" + ServerProfile.DefaultShortId;
            }

            return rawUri;
        }

        public static VlessConfig Parse(string rawUri, string fallbackRemoteIp)
        {
            rawUri = AutoFixLink(rawUri, fallbackRemoteIp);

            if (!rawUri.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Gecersiz protokol! Link 'vless://' ile baslamalidir.");

            Uri uri = new Uri(rawUri);
            VlessConfig config = new VlessConfig();
            config.Uuid = !string.IsNullOrEmpty(uri.UserInfo) ? uri.UserInfo : ServerProfile.DefaultUuid;

            string host = uri.Host;
            if (host == "127.0.0.1" || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(host))
            {
                host = !string.IsNullOrEmpty(fallbackRemoteIp) ? fallbackRemoteIp.Trim() : ServerProfile.DefaultRemoteIp;
            }
            config.Host = host;
            config.Port = uri.Port > 0 ? uri.Port : 443;
            config.Remark = !string.IsNullOrEmpty(uri.Fragment) ? Uri.UnescapeDataString(uri.Fragment.TrimStart('#')) : "MTGA Node";

            string query = uri.Query.TrimStart('?');
            string[] pairs = query.Split('&');
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string p in pairs)
            {
                string[] kv = p.Split('=');
                if (kv.Length == 2)
                {
                    dict[kv[0]] = Uri.UnescapeDataString(kv[1]);
                }
            }

            config.Flow = dict.ContainsKey("flow") ? dict["flow"] : "xtls-rprx-vision";
            config.Security = dict.ContainsKey("security") ? dict["security"] : "reality";
            config.Sni = dict.ContainsKey("sni") && !string.IsNullOrEmpty(dict["sni"]) ? dict["sni"] : ServerProfile.DefaultSni;
            config.Fingerprint = dict.ContainsKey("fp") ? dict["fp"] : "chrome";

            string pbk = dict.ContainsKey("pbk") ? dict["pbk"] : "";
            if (string.IsNullOrEmpty(pbk) || pbk.Equals("PUBLIC_KEY", StringComparison.OrdinalIgnoreCase))
            {
                pbk = ServerProfile.DefaultPublicKey;
            }
            config.PublicKey = pbk;

            string sid = dict.ContainsKey("sid") ? dict["sid"] : "";
            if (string.IsNullOrEmpty(sid) || sid.Equals("SHORT_ID", StringComparison.OrdinalIgnoreCase))
            {
                sid = ServerProfile.DefaultShortId;
            }
            config.ShortId = sid;

            return config;
        }
    }

    public static class ProxyHelper
    {
        [DllImport("wininet.dll")]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
        public const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        public const int INTERNET_OPTION_REFRESH = 37;

        public static void SetSystemProxy(bool enable, string proxyServer)
        {
            try
            {
                using (RegistryKey registry = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true))
                {
                    if (registry != null)
                    {
                        registry.SetValue("ProxyEnable", enable ? 1 : 0);
                        if (enable && !string.IsNullOrEmpty(proxyServer))
                        {
                            registry.SetValue("ProxyServer", proxyServer);
                        }
                    }
                }
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
                InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
            }
            catch { }
        }
    }

    public class MainForm : Form
    {
        private TextBox txtRemoteIp;
        private TextBox txtUri;
        private Button btnConnect;
        private Label lblStatus;
        private Label lblRemoteServer;
        private Label lblSni;
        private Label lblMode;
        private Panel pnlStatusBadge;
        private CheckBox chkTunMode;
        private Process xrayProcess;
        private bool isConnected = false;
        private bool isExplicitExit = false;
        private Timer pulseTimer;

        // System Tray (Sistem Tepsisi) Bileşenleri
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private MenuItem menuConnect;

        private readonly Color ColBg = Color.FromArgb(13, 17, 23);
        private readonly Color ColCard = Color.FromArgb(22, 27, 34);
        private readonly Color ColPrimary = Color.FromArgb(46, 204, 113);
        private readonly Color ColTun = Color.FromArgb(52, 152, 219);
        private readonly Color ColDanger = Color.FromArgb(231, 76, 60);
        private readonly Color ColTextMuted = Color.FromArgb(139, 148, 158);

        public MainForm()
        {
            string profilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_profile.json");
            ServerProfile.LoadFromFile(profilePath);
            InitializeComponent();
            InitializeTray();
        }

        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private Icon CreateColoredIcon(Color c)
        {
            using (Bitmap b = new Bitmap(16, 16))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush br = new SolidBrush(c))
                {
                    g.FillEllipse(br, 1, 1, 14, 14);
                }
                using (Pen p = new Pen(Color.FromArgb(180, 255, 255, 255), 1.2f))
                {
                    g.DrawEllipse(p, 1, 1, 14, 14);
                }
                IntPtr hIcon = b.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        private void InitializeTray()
        {
            trayMenu = new ContextMenu();

            MenuItem menuShow = new MenuItem("Goster / Gizle", (s, e) => { ToggleWindowState(); });
            menuShow.DefaultItem = true;
            trayMenu.MenuItems.Add(menuShow);

            trayMenu.MenuItems.Add("-");

            menuConnect = new MenuItem("Baglan", (s, e) => { BtnConnect_Click(null, null); });
            trayMenu.MenuItems.Add(menuConnect);

            trayMenu.MenuItems.Add("-");

            MenuItem menuExit = new MenuItem("Tamamen Cikis", (s, e) =>
            {
                isExplicitExit = true;
                this.Close();
            });
            trayMenu.MenuItems.Add(menuExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Make Turkey Great Again (MTGA)";
            trayIcon.Icon = CreateColoredIcon(ColDanger);
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => { ToggleWindowState(); };
        }

        private void ToggleWindowState()
        {
            if (this.Visible && this.WindowState != FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
                this.ShowInTaskbar = false;
            }
            else
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.BringToFront();
                this.Activate();
            }
        }

        private void UpdateTrayIconState()
        {
            if (isConnected)
            {
                trayIcon.Icon = CreateColoredIcon(chkTunMode.Checked ? ColTun : ColPrimary);
                menuConnect.Text = "Baglantiyi Kes";
                trayIcon.Text = "MTGA: Bagli (" + (chkTunMode.Checked ? "TUN Modu" : "Sistem Proxy") + ")";
            }
            else
            {
                trayIcon.Icon = CreateColoredIcon(ColDanger);
                menuConnect.Text = "Baglan";
                trayIcon.Text = "MTGA: Baglanti Yok";
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Make Turkey Great Again - VLESS Reality Client";
            this.Size = new Size(560, 780);
            this.BackColor = ColBg;
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Header Title
            Label lblHeader = new Label();
            lblHeader.Text = "MAKE TURKEY GREAT AGAIN";
            lblHeader.Font = new Font("Segoe UI", 15, FontStyle.Bold);
            lblHeader.ForeColor = Color.White;
            lblHeader.TextAlign = ContentAlignment.MiddleCenter;
            lblHeader.Dock = DockStyle.Top;
            lblHeader.Height = 40;
            this.Controls.Add(lblHeader);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "VLESS-Reality DPI Sansur Asma & Wintun Surucusu";
            lblSubtitle.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            lblSubtitle.ForeColor = ColTextMuted;
            lblSubtitle.TextAlign = ContentAlignment.MiddleCenter;
            lblSubtitle.Dock = DockStyle.Top;
            lblSubtitle.Height = 22;
            this.Controls.Add(lblSubtitle);

            // Container Panel
            Panel pnlMain = new Panel();
            pnlMain.Dock = DockStyle.Fill;
            pnlMain.Padding = new Padding(20);
            this.Controls.Add(pnlMain);

            // Status Badge
            pnlStatusBadge = new Panel();
            pnlStatusBadge.Size = new Size(490, 38);
            pnlStatusBadge.Location = new Point(25, 10);
            pnlStatusBadge.BackColor = Color.FromArgb(25, 231, 76, 60);
            pnlStatusBadge.Paint += (s, e) =>
            {
                Color borderCol = isConnected ? (chkTunMode.Checked ? ColTun : ColPrimary) : ColDanger;
                using (Pen p = new Pen(borderCol, 1.5f))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawRectangle(p, 1, 1, pnlStatusBadge.Width - 2, pnlStatusBadge.Height - 2);
                }
            };

            lblStatus = new Label();
            lblStatus.Text = "DURUM: BAGLANTI KESILDI";
            lblStatus.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblStatus.ForeColor = ColDanger;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblStatus.Dock = DockStyle.Fill;
            pnlStatusBadge.Controls.Add(lblStatus);
            pnlMain.Controls.Add(pnlStatusBadge);

            // Circular Connect Button
            btnConnect = new Button();
            btnConnect.Size = new Size(150, 150);
            btnConnect.Location = new Point(195, 60);
            btnConnect.FlatStyle = FlatStyle.Flat;
            btnConnect.FlatAppearance.BorderSize = 0;
            btnConnect.BackColor = ColCard;
            btnConnect.Cursor = Cursors.Hand;
            btnConnect.Paint += BtnConnect_Paint;
            btnConnect.Click += BtnConnect_Click;
            pnlMain.Controls.Add(btnConnect);

            // TUN Mode Container
            Panel pnlTunToggle = new Panel();
            pnlTunToggle.Size = new Size(490, 42);
            pnlTunToggle.Location = new Point(25, 222);
            pnlTunToggle.BackColor = Color.FromArgb(20, 25, 32);
            pnlTunToggle.Paint += (s, e) =>
            {
                Color c = chkTunMode.Checked ? ColTun : Color.FromArgb(48, 54, 61);
                using (Pen p = new Pen(c, 1.2f))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, pnlTunToggle.Width - 1, pnlTunToggle.Height - 1);
                }
            };

            chkTunMode = new CheckBox();
            chkTunMode.Text = "TUN Modu (L3 Sanal Ag Adaptoru - Tum Cihaz UDP/TCP Trafigi)";
            chkTunMode.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            chkTunMode.ForeColor = ColTun;
            chkTunMode.Location = new Point(12, 9);
            chkTunMode.Size = new Size(465, 24);
            chkTunMode.Cursor = Cursors.Hand;
            chkTunMode.CheckedChanged += ChkTunMode_CheckedChanged;
            pnlTunToggle.Controls.Add(chkTunMode);
            pnlMain.Controls.Add(pnlTunToggle);

            // Remote IP Box
            Label lblIpTitle = new Label();
            lblIpTitle.Text = "UZAK SUNUCU IP ADRESI:";
            lblIpTitle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblIpTitle.ForeColor = Color.FromArgb(88, 166, 255);
            lblIpTitle.Location = new Point(25, 274);
            lblIpTitle.Size = new Size(250, 18);
            pnlMain.Controls.Add(lblIpTitle);

            txtRemoteIp = new TextBox();
            txtRemoteIp.Text = ServerProfile.DefaultRemoteIp;
            txtRemoteIp.Location = new Point(25, 294);
            txtRemoteIp.Size = new Size(490, 26);
            txtRemoteIp.BackColor = ColCard;
            txtRemoteIp.ForeColor = Color.White;
            txtRemoteIp.Font = new Font("Consolas", 10f, FontStyle.Bold);
            txtRemoteIp.BorderStyle = BorderStyle.FixedSingle;
            txtRemoteIp.TextChanged += (s, e) => { UpdateUriFromRemoteIp(); };
            pnlMain.Controls.Add(txtRemoteIp);

            // Metrics Box
            Panel pnlMetrics = new Panel();
            pnlMetrics.Size = new Size(490, 72);
            pnlMetrics.Location = new Point(25, 332);
            pnlMetrics.BackColor = ColCard;
            pnlMetrics.Paint += (s, e) =>
            {
                using (Pen p = new Pen(Color.FromArgb(48, 54, 61), 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, pnlMetrics.Width - 1, pnlMetrics.Height - 1);
                }
            };

            lblRemoteServer = new Label();
            lblRemoteServer.Text = "Hedef: " + ServerProfile.DefaultRemoteIp + ":443";
            lblRemoteServer.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblRemoteServer.ForeColor = Color.FromArgb(88, 166, 255);
            lblRemoteServer.Location = new Point(15, 14);
            lblRemoteServer.Size = new Size(260, 20);
            pnlMetrics.Controls.Add(lblRemoteServer);

            lblSni = new Label();
            lblSni.Text = "Maske: " + ServerProfile.DefaultSni + " (Chrome uTLS)";
            lblSni.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblSni.ForeColor = ColTextMuted;
            lblSni.Location = new Point(15, 38);
            lblSni.Size = new Size(260, 20);
            pnlMetrics.Controls.Add(lblSni);

            lblMode = new Label();
            lblMode.Text = "Mod: Standart Sistem Proxy";
            lblMode.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblMode.ForeColor = ColPrimary;
            lblMode.TextAlign = ContentAlignment.MiddleRight;
            lblMode.Location = new Point(265, 24);
            lblMode.Size = new Size(210, 25);
            pnlMetrics.Controls.Add(lblMode);

            pnlMain.Controls.Add(pnlMetrics);

            // VLESS Link Input Section
            Label lblInputHeader = new Label();
            lblInputHeader.Text = "VLESS BAGLANTI PROFILI:";
            lblInputHeader.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblInputHeader.ForeColor = ColTextMuted;
            lblInputHeader.Location = new Point(25, 416);
            lblInputHeader.Size = new Size(200, 20);
            pnlMain.Controls.Add(lblInputHeader);

            Button btnPaste = new Button();
            btnPaste.Text = "Panodan Yapistir";
            btnPaste.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            btnPaste.BackColor = Color.FromArgb(33, 38, 45);
            btnPaste.ForeColor = Color.White;
            btnPaste.FlatStyle = FlatStyle.Flat;
            btnPaste.FlatAppearance.BorderColor = Color.FromArgb(48, 54, 61);
            btnPaste.Location = new Point(375, 412);
            btnPaste.Size = new Size(140, 26);
            btnPaste.Cursor = Cursors.Hand;
            btnPaste.Click += (s, e) =>
            {
                if (Clipboard.ContainsText())
                {
                    string clip = Clipboard.GetText().Trim();
                    txtUri.Text = VlessConfig.AutoFixLink(clip, txtRemoteIp.Text);
                }
            };
            pnlMain.Controls.Add(btnPaste);

            Button btnFix = new Button();
            btnFix.Text = "Profili Onar";
            btnFix.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            btnFix.BackColor = Color.FromArgb(33, 38, 45);
            btnFix.ForeColor = ColPrimary;
            btnFix.FlatStyle = FlatStyle.Flat;
            btnFix.FlatAppearance.BorderColor = Color.FromArgb(48, 54, 61);
            btnFix.Location = new Point(245, 412);
            btnFix.Size = new Size(120, 26);
            btnFix.Cursor = Cursors.Hand;
            btnFix.Click += (s, e) =>
            {
                txtUri.Text = VlessConfig.AutoFixLink(txtUri.Text, txtRemoteIp.Text);
            };
            pnlMain.Controls.Add(btnFix);

            txtUri = new TextBox();
            txtUri.Multiline = true;
            txtUri.ScrollBars = ScrollBars.Vertical;
            txtUri.Location = new Point(25, 444);
            txtUri.Size = new Size(490, 80);
            txtUri.BackColor = ColCard;
            txtUri.ForeColor = Color.FromArgb(201, 209, 217);
            txtUri.Font = new Font("Consolas", 8f);
            txtUri.BorderStyle = BorderStyle.FixedSingle;
            txtUri.Text = VlessConfig.AutoFixLink("", ServerProfile.DefaultRemoteIp);
            pnlMain.Controls.Add(txtUri);

            // Footer note
            Label lblFooter = new Label();
            lblFooter.Text = "Pencereyi kapattiginizda veya kuculttugunuzde uygulama sistem tepsisinde (saat yaninda) arka planda calismaya devam eder.";
            lblFooter.Font = new Font("Segoe UI", 8, FontStyle.Regular);
            lblFooter.ForeColor = ColTextMuted;
            lblFooter.TextAlign = ContentAlignment.MiddleCenter;
            lblFooter.Location = new Point(25, 538);
            lblFooter.Size = new Size(490, 40);
            pnlMain.Controls.Add(lblFooter);

            pulseTimer = new Timer();
            pulseTimer.Interval = 50;
            pulseTimer.Tick += (s, e) => { btnConnect.Invalidate(); };

            this.Resize += MainForm_Resize;
            this.FormClosing += MainForm_FormClosing;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                this.ShowInTaskbar = false;
                trayIcon.ShowBalloonTip(1500, "Make Turkey Great Again", "Uygulama sistem tepsisine kucultuldu. Baglanti arka planda devam ediyor.", ToolTipIcon.Info);
            }
        }

        private void ChkTunMode_CheckedChanged(object sender, EventArgs e)
        {
            if (chkTunMode.Checked)
            {
                lblMode.Text = "Mod: TUN (Tum Sistem)";
                lblMode.ForeColor = ColTun;
                if (!IsAdministrator())
                {
                    DialogResult res = MessageBox.Show(
                        "TUN Modu (Sanal Ag Adaptoru), isletim sisteminde Wintun surucusunu baslatabilmek icin Yonetici Yetkisi gerektirir.\n\nUygulama Yonetici Olarak yeniden baslatilsin mi?",
                        "Yonetici Yetkisi Gerekli",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (res == DialogResult.Yes)
                    {
                        try
                        {
                            isExplicitExit = true;
                            ProcessStartInfo adminPsi = new ProcessStartInfo();
                            adminPsi.FileName = Application.ExecutablePath;
                            adminPsi.Verb = "runas";
                            Process.Start(adminPsi);
                            Application.Exit();
                        }
                        catch
                        {
                            MessageBox.Show("Yonetici izni saglanmadi. Standart Proxy Modu secili kaliyor.", "Bilgi");
                            chkTunMode.Checked = false;
                        }
                    }
                    else
                    {
                        chkTunMode.Checked = false;
                    }
                }
            }
            else
            {
                lblMode.Text = "Mod: Standart Sistem Proxy";
                lblMode.ForeColor = ColPrimary;
            }
            pnlStatusBadge.Invalidate();
            UpdateTrayIconState();
        }

        private void UpdateUriFromRemoteIp()
        {
            try
            {
                if (!string.IsNullOrEmpty(txtUri.Text))
                {
                    txtUri.Text = VlessConfig.AutoFixLink(txtUri.Text, txtRemoteIp.Text);
                    lblRemoteServer.Text = "Hedef: " + txtRemoteIp.Text.Trim() + ":443";
                }
            }
            catch { }
        }

        private void BtnConnect_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int w = btnConnect.Width;
            int h = btnConnect.Height;
            Color ringColor = isConnected ? (chkTunMode.Checked ? ColTun : ColPrimary) : Color.FromArgb(48, 54, 61);

            using (Pen p = new Pen(ringColor, isConnected ? 4f : 2.5f))
            {
                e.Graphics.DrawEllipse(p, 6, 6, w - 12, h - 12);
            }

            using (Pen iconPen = new Pen(ringColor, 4f))
            {
                iconPen.StartCap = LineCap.Round;
                iconPen.EndCap = LineCap.Round;
                e.Graphics.DrawLine(iconPen, w / 2, 38, w / 2, 60);
                e.Graphics.DrawArc(iconPen, 46, 44, 58, 58, -60, 300);
            }

            string text = isConnected ? "DURDUR" : "BAGLAN";
            using (Font f = new Font("Segoe UI", 10, FontStyle.Bold))
            using (Brush b = new SolidBrush(ringColor))
            {
                SizeF size = e.Graphics.MeasureString(text, f);
                e.Graphics.DrawString(text, f, b, (w - size.Width) / 2, 115);
            }
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (isConnected)
            {
                Disconnect();
            }
            else
            {
                Connect();
            }
        }

        private void Connect()
        {
            try
            {
                string remoteIp = txtRemoteIp.Text.Trim();
                if (string.IsNullOrEmpty(remoteIp) || remoteIp == "127.0.0.1" || remoteIp == "localhost")
                {
                    MessageBox.Show("Gecerli bir uzak sunucu IP adresi tanimlayin.", "Uyari", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtRemoteIp.Focus();
                    return;
                }

                VlessConfig config = VlessConfig.Parse(txtUri.Text, remoteIp);
                txtUri.Text = VlessConfig.AutoFixLink(txtUri.Text, remoteIp);

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string coreDir = Path.Combine(baseDir, "core");
                string xrayExe = Path.Combine(coreDir, "xray.exe");

                if (!File.Exists(xrayExe))
                {
                    if (File.Exists(Path.Combine(baseDir, "xray.exe")))
                    {
                        xrayExe = Path.Combine(baseDir, "xray.exe");
                        coreDir = baseDir;
                    }
                    else
                    {
                        MessageBox.Show("xray.exe bulunamadi: " + xrayExe, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                bool isTun = chkTunMode.Checked;

                string configJson = GenerateConfigJson(config, isTun);
                string configPath = Path.Combine(coreDir, "active_config.json");
                File.WriteAllText(configPath, configJson, new UTF8Encoding(false));

                ProcessStartInfo testPsi = new ProcessStartInfo();
                testPsi.FileName = xrayExe;
                testPsi.Arguments = "run -test -c \"" + configPath + "\"";
                testPsi.WorkingDirectory = coreDir;
                testPsi.CreateNoWindow = true;
                testPsi.UseShellExecute = false;
                testPsi.RedirectStandardOutput = true;
                testPsi.RedirectStandardError = true;

                using (Process testProcess = Process.Start(testPsi))
                {
                    testProcess.WaitForExit(4000);
                    string output = testProcess.StandardOutput.ReadToEnd() + testProcess.StandardError.ReadToEnd();
                    if (testProcess.ExitCode != 0)
                    {
                        MessageBox.Show("Xray yapilandirma testi basarisiz:\n\n" + output, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                KillExistingXray();

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = xrayExe;
                psi.Arguments = "run -c \"" + configPath + "\"";
                psi.WorkingDirectory = coreDir;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                xrayProcess = Process.Start(psi);

                System.Threading.Thread.Sleep(900);
                if (xrayProcess.HasExited)
                {
                    MessageBox.Show("Xray motoru baslatilamadi. TUN Modu icin Yonetici Yetkisi gereklidir.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ProxyHelper.SetSystemProxy(true, "http=127.0.0.1:10809;https=127.0.0.1:10809;socks=127.0.0.1:10808");

                isConnected = true;
                if (isTun)
                {
                    lblStatus.Text = "DURUM: GUVENLI TUNEL AKTIF (TUN MODU - WINTUN)";
                    lblStatus.ForeColor = ColTun;
                    pnlStatusBadge.BackColor = Color.FromArgb(25, 52, 152, 219);
                    lblMode.Text = "Mod: Wintun L3 Adaptor";
                    lblMode.ForeColor = ColTun;
                }
                else
                {
                    lblStatus.Text = "DURUM: GUVENLI TUNEL AKTIF (SISTEM PROXY)";
                    lblStatus.ForeColor = ColPrimary;
                    pnlStatusBadge.BackColor = Color.FromArgb(25, 46, 204, 113);
                    lblMode.Text = "Mod: Standart Sistem Proxy";
                    lblMode.ForeColor = ColPrimary;
                }

                lblRemoteServer.Text = "Hedef: " + config.Host + ":" + config.Port;
                lblSni.Text = "Maske: " + config.Sni + " (Chrome uTLS)";
                pulseTimer.Start();
                btnConnect.Invalidate();
                pnlStatusBadge.Invalidate();
                UpdateTrayIconState();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Baglanti hatasi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Disconnect()
        {
            try
            {
                KillExistingXray();
                ProxyHelper.SetSystemProxy(false, "");

                isConnected = false;
                pulseTimer.Stop();
                lblStatus.Text = "DURUM: BAGLANTI KESILDI";
                lblStatus.ForeColor = ColDanger;
                pnlStatusBadge.BackColor = Color.FromArgb(25, 231, 76, 60);
                lblMode.Text = chkTunMode.Checked ? "Mod: TUN (Tum Sistem)" : "Mod: Standart Sistem Proxy";
                lblMode.ForeColor = chkTunMode.Checked ? ColTun : ColPrimary;
                btnConnect.Invalidate();
                pnlStatusBadge.Invalidate();
                UpdateTrayIconState();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Baglanti kesilirken hata: " + ex.Message);
            }
        }

        private void KillExistingXray()
        {
            try
            {
                if (xrayProcess != null && !xrayProcess.HasExited)
                {
                    xrayProcess.Kill();
                    xrayProcess.Dispose();
                    xrayProcess = null;
                }
            }
            catch { }

            try
            {
                Process[] procs = Process.GetProcessesByName("xray");
                foreach (Process p in procs)
                {
                    try { p.Kill(); } catch { }
                }
            }
            catch { }
        }

        private string GenerateConfigJson(VlessConfig c, bool enableTun)
        {
            string inbounds = "    { \"tag\": \"socks-in\", \"port\": 10808, \"listen\": \"127.0.0.1\", \"protocol\": \"socks\", \"settings\": { \"auth\": \"noauth\", \"udp\": true }, \"sniffing\": { \"enabled\": true, \"destOverride\": [\"http\", \"tls\", \"quic\"] } },\n" +
                              "    { \"tag\": \"http-in\", \"port\": 10809, \"listen\": \"127.0.0.1\", \"protocol\": \"http\" }";

            if (enableTun)
            {
                inbounds += ",\n" +
                    "    {\n" +
                    "      \"tag\": \"tun-in\",\n" +
                    "      \"protocol\": \"tun\",\n" +
                    "      \"settings\": {\n" +
                    "        \"name\": \"wintun\",\n" +
                    "        \"mtu\": 1500,\n" +
                    "        \"gateway\": [\"10.0.0.1/16\"],\n" +
                    "        \"dns\": [\"1.1.1.1\", \"8.8.8.8\"],\n" +
                    "        \"autoSystemRoutingTable\": [\"0.0.0.0/0\"],\n" +
                    "        \"autoOutboundsInterface\": \"auto\"\n" +
                    "      },\n" +
                    "      \"sniffing\": {\n" +
                    "        \"enabled\": true,\n" +
                    "        \"destOverride\": [\"http\", \"tls\", \"quic\"]\n" +
                    "      }\n" +
                    "    }";
            }

            return "{\n" +
                "  \"log\": { \"loglevel\": \"warning\" },\n" +
                "  \"inbounds\": [\n" +
                inbounds + "\n" +
                "  ],\n" +
                "  \"outbounds\": [\n" +
                "    {\n" +
                "      \"tag\": \"proxy\",\n" +
                "      \"protocol\": \"vless\",\n" +
                "      \"settings\": {\n" +
                "        \"vnext\": [\n" +
                "          {\n" +
                "            \"address\": \"" + c.Host + "\",\n" +
                "            \"port\": " + c.Port + ",\n" +
                "            \"users\": [\n" +
                "              {\n" +
                "                \"id\": \"" + c.Uuid + "\",\n" +
                "                \"encryption\": \"none\",\n" +
                "                \"flow\": \"" + c.Flow + "\",\n" +
                "                \"level\": 0\n" +
                "              }\n" +
                "            ]\n" +
                "          }\n" +
                "        ]\n" +
                "      },\n" +
                "      \"streamSettings\": {\n" +
                "        \"network\": \"tcp\",\n" +
                "        \"security\": \"reality\",\n" +
                "        \"realitySettings\": {\n" +
                "          \"show\": false,\n" +
                "          \"serverName\": \"" + c.Sni + "\",\n" +
                "          \"fingerprint\": \"" + c.Fingerprint + "\",\n" +
                "          \"publicKey\": \"" + c.PublicKey + "\",\n" +
                "          \"shortId\": \"" + c.ShortId + "\",\n" +
                "          \"spiderX\": \"\"\n" +
                "        }\n" +
                "      }\n" +
                "    },\n" +
                "    { \"tag\": \"direct\", \"protocol\": \"freedom\" },\n" +
                "    { \"tag\": \"block\", \"protocol\": \"blackhole\" }\n" +
                "  ],\n" +
                "  \"routing\": {\n" +
                "    \"domainStrategy\": \"IPIfNonMatch\",\n" +
                "    \"rules\": [\n" +
                "      { \"type\": \"field\", \"ip\": [\"geoip:private\"], \"outboundTag\": \"direct\" },\n" +
                "      { \"type\": \"field\", \"outboundTag\": \"proxy\", \"network\": \"tcp,udp\" }\n" +
                "    ]\n" +
                "  }\n" +
                "}";
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Kullanıcı pencereyi 'X' ile kapattıysa ve doğrudan çıkış demediyse sistem tepsisine gizle
            if (!isExplicitExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
                this.ShowInTaskbar = false;
                trayIcon.ShowBalloonTip(1500, "Make Turkey Great Again", "Uygulama arka planda calisiyor. Tamamen kapatmak icin simgeye sag tiklayip 'Tamamen Cikis' secenegini kullanin.", ToolTipIcon.Info);
            }
            else
            {
                Disconnect();
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
            }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
