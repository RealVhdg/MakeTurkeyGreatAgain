using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MakeTurkeyGreatAgain.Installer
{
    public class SetupForm : Form
    {
        private TextBox txtInstallDir;
        private CheckBox chkDesktopShortcut;
        private CheckBox chkStartMenuShortcut;
        private CheckBox chkLaunchAfter;
        private Button btnInstall;
        private ProgressBar progressBar;
        private Label lblStatus;
        private bool isFinished = false;

        private readonly Color ColBg = Color.FromArgb(13, 17, 23);
        private readonly Color ColCard = Color.FromArgb(22, 27, 34);
        private readonly Color ColPrimary = Color.FromArgb(46, 204, 113);
        private readonly Color ColTextMuted = Color.FromArgb(139, 148, 158);

        public SetupForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Make Turkey Great Again - Kurulum";
            this.Size = new Size(540, 440);
            this.BackColor = ColBg;
            this.ForeColor = Color.White;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            Label lblTitle = new Label();
            lblTitle.Text = "MAKE TURKEY GREAT AGAIN";
            lblTitle.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 40;
            this.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "VLESS-Reality DPI Sansur Asma Istemcisi Kurulumu";
            lblSub.Font = new Font("Segoe UI", 9, FontStyle.Regular);
            lblSub.ForeColor = ColTextMuted;
            lblSub.TextAlign = ContentAlignment.MiddleCenter;
            lblSub.Dock = DockStyle.Top;
            lblSub.Height = 22;
            this.Controls.Add(lblSub);

            Panel pnlContent = new Panel();
            pnlContent.Dock = DockStyle.Fill;
            pnlContent.Padding = new Padding(25);
            this.Controls.Add(pnlContent);

            Label lblDir = new Label();
            lblDir.Text = "Hedef Kurulum Klasoru:";
            lblDir.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblDir.ForeColor = Color.FromArgb(88, 166, 255);
            lblDir.Location = new Point(25, 15);
            lblDir.Size = new Size(300, 18);
            pnlContent.Controls.Add(lblDir);

            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\MakeTurkeyGreatAgain");
            txtInstallDir = new TextBox();
            txtInstallDir.Text = defaultPath;
            txtInstallDir.Font = new Font("Consolas", 8.5f);
            txtInstallDir.Location = new Point(25, 36);
            txtInstallDir.Size = new Size(385, 24);
            txtInstallDir.BackColor = ColCard;
            txtInstallDir.ForeColor = Color.White;
            txtInstallDir.BorderStyle = BorderStyle.FixedSingle;
            pnlContent.Controls.Add(txtInstallDir);

            Button btnBrowse = new Button();
            btnBrowse.Text = "Sec...";
            btnBrowse.Font = new Font("Segoe UI", 8.5f);
            btnBrowse.Location = new Point(415, 35);
            btnBrowse.Size = new Size(80, 26);
            btnBrowse.BackColor = Color.FromArgb(33, 38, 45);
            btnBrowse.ForeColor = Color.White;
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(48, 54, 61);
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtInstallDir.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtInstallDir.Text = Path.Combine(fbd.SelectedPath, "MakeTurkeyGreatAgain");
                    }
                }
            };
            pnlContent.Controls.Add(btnBrowse);

            chkDesktopShortcut = new CheckBox();
            chkDesktopShortcut.Text = "Masaustune Kisayol Olustur";
            chkDesktopShortcut.Checked = true;
            chkDesktopShortcut.Font = new Font("Segoe UI", 9);
            chkDesktopShortcut.Location = new Point(25, 80);
            chkDesktopShortcut.Size = new Size(300, 24);
            pnlContent.Controls.Add(chkDesktopShortcut);

            chkStartMenuShortcut = new CheckBox();
            chkStartMenuShortcut.Text = "Baslat Menusune Kisayol Ekle";
            chkStartMenuShortcut.Checked = true;
            chkStartMenuShortcut.Font = new Font("Segoe UI", 9);
            chkStartMenuShortcut.Location = new Point(25, 110);
            chkStartMenuShortcut.Size = new Size(300, 24);
            pnlContent.Controls.Add(chkStartMenuShortcut);

            chkLaunchAfter = new CheckBox();
            chkLaunchAfter.Text = "Kurulum tamamlandiginda uygulamayi baslat";
            chkLaunchAfter.Checked = true;
            chkLaunchAfter.Font = new Font("Segoe UI", 9);
            chkLaunchAfter.Location = new Point(25, 140);
            chkLaunchAfter.Size = new Size(350, 24);
            pnlContent.Controls.Add(chkLaunchAfter);

            progressBar = new ProgressBar();
            progressBar.Location = new Point(25, 185);
            progressBar.Size = new Size(470, 16);
            progressBar.Style = ProgressBarStyle.Blocks;
            pnlContent.Controls.Add(progressBar);

            lblStatus = new Label();
            lblStatus.Text = "Kuruluma hazir.";
            lblStatus.Font = new Font("Segoe UI", 8.5f);
            lblStatus.ForeColor = ColTextMuted;
            lblStatus.Location = new Point(25, 208);
            lblStatus.Size = new Size(470, 20);
            pnlContent.Controls.Add(lblStatus);

            btnInstall = new Button();
            btnInstall.Text = "Hemen Kur";
            btnInstall.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnInstall.Location = new Point(25, 240);
            btnInstall.Size = new Size(470, 42);
            btnInstall.BackColor = ColPrimary;
            btnInstall.ForeColor = Color.Black;
            btnInstall.FlatStyle = FlatStyle.Flat;
            btnInstall.FlatAppearance.BorderSize = 0;
            btnInstall.Cursor = Cursors.Hand;
            btnInstall.Click += BtnInstall_Click;
            pnlContent.Controls.Add(btnInstall);
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            if (isFinished)
            {
                if (chkLaunchAfter.Checked)
                {
                    string exePath = Path.Combine(txtInstallDir.Text, "MakeTurkeyGreatAgain.exe");
                    if (File.Exists(exePath))
                    {
                        Process.Start(exePath);
                    }
                }
                this.Close();
                return;
            }

            btnInstall.Enabled = false;
            txtInstallDir.Enabled = false;
            lblStatus.Text = "Kurulum hazirlaniyor...";
            progressBar.Value = 10;

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync(txtInstallDir.Text);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            string targetDir = (string)e.Argument;

            // 1. Hedef klasoru hazirla
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 2. Gomulu payload.zip kaynagini disari cikar
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            string tempZip = Path.Combine(Path.GetTempPath(), "mtga_payload_" + Guid.NewGuid().ToString("N") + ".zip");

            using (Stream resourceStream = currentAssembly.GetManifestResourceStream("MakeTurkeyGreatAgain.payload.zip"))
            {
                if (resourceStream == null)
                {
                    // Eger gomulu degilse ayni klasordeki payload.zip veya bin klasorunu kontrol et
                    string localZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.zip");
                    if (File.Exists(localZip))
                    {
                        File.Copy(localZip, tempZip, true);
                    }
                    else
                    {
                        throw new FileNotFoundException("Kurulum paketi (payload.zip) bulunamadi!");
                    }
                }
                else
                {
                    using (FileStream fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
                    {
                        resourceStream.CopyTo(fileStream);
                    }
                }
            }

            // 3. Zip dosyasini hedef dizine ac
            ZipFile.ExtractToDirectory(tempZip, targetDir);

            try { File.Delete(tempZip); } catch { }

            // 4. Kisayollari olustur
            string exePath = Path.Combine(targetDir, "MakeTurkeyGreatAgain.exe");

            if (chkDesktopShortcut.Checked)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string lnkPath = Path.Combine(desktop, "Make Turkey Great Again.lnk");
                CreateShortcut(lnkPath, exePath, targetDir);
            }

            if (chkStartMenuShortcut.Checked)
            {
                string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Make Turkey Great Again");
                if (!Directory.Exists(startMenu)) Directory.CreateDirectory(startMenu);
                string lnkPath = Path.Combine(startMenu, "Make Turkey Great Again.lnk");
                CreateShortcut(lnkPath, exePath, targetDir);
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                lblStatus.Text = "Hata olustu: " + e.Error.Message;
                lblStatus.ForeColor = Color.FromArgb(231, 76, 60);
                btnInstall.Enabled = true;
                btnInstall.Text = "Tekrar Dene";
                return;
            }

            progressBar.Value = 100;
            lblStatus.Text = "Kurulum basariyla tamamlandi!";
            lblStatus.ForeColor = ColPrimary;
            btnInstall.Enabled = true;
            btnInstall.Text = "Bitir ve Baslat";
            isFinished = true;
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workDir)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return;
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type scType = sc.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { targetPath });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { workDir });
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "Make Turkey Great Again - VLESS Reality Client" });
                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }
}
