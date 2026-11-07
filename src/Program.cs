using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SakatsukuPatcher
{
    [DataContract]
    public class PatchManifest
    {
        [DataMember(Name = "format")]
        public string Format { get; set; }

        [DataMember(Name = "files")]
        public List<PatchItem> Files { get; set; }
    }

    [DataContract]
    public class PatchItem
    {
        [DataMember(Name = "path")]
        public string Path { get; set; }

        [DataMember(Name = "size")]
        public long Size { get; set; }

        [DataMember(Name = "original_sha256")]
        public string OriginalSha256 { get; set; }

        [DataMember(Name = "target_sha256")]
        public string TargetSha256 { get; set; }

        [DataMember(Name = "payload")]
        public string Payload { get; set; }
    }

    public class MainForm : Form
    {
        private TextBox txtPath;
        private Button btnBrowse;
        private Button btnApply;
        private ProgressBar progressBar;
        private Label lblStatus;
        private Label lblInfo;

        public MainForm(string defaultPath)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
            {
                txtPath.Text = defaultPath;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "사카츠쿠 2013 (PS3) 한글패치 v0.9";
            this.Size = new Size(540, 260);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("맑은 고딕", 9.5f, FontStyle.Regular);

            lblInfo = new Label()
            {
                Text = "사카츠쿠 2013 게임 폴더(PS3_GAME 포함 폴더)를 선택해주세요.",
                Location = new Point(20, 20),
                Size = new Size(480, 25)
            };

            txtPath = new TextBox()
            {
                Location = new Point(20, 50),
                Size = new Size(380, 25)
            };

            btnBrowse = new Button()
            {
                Text = "찾아보기...",
                Location = new Point(410, 48),
                Size = new Size(90, 28)
            };
            btnBrowse.Click += BtnBrowse_Click;

            progressBar = new ProgressBar()
            {
                Location = new Point(20, 95),
                Size = new Size(480, 22),
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };

            lblStatus = new Label()
            {
                Text = "준비 완료",
                Location = new Point(20, 125),
                Size = new Size(480, 25),
                ForeColor = Color.DarkSlateGray
            };

            btnApply = new Button()
            {
                Text = "한글패치 적용하기",
                Location = new Point(170, 160),
                Size = new Size(180, 38),
                Font = new Font("맑은 고딕", 10.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(41, 128, 185),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Click += BtnApply_Click;

            this.Controls.Add(lblInfo);
            this.Controls.Add(txtPath);
            this.Controls.Add(btnBrowse);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblStatus);
            this.Controls.Add(btnApply);
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "사카츠쿠 게임 폴더(PS3_GAME 폴더가 포함된 폴더)를 선택하세요.";
                if (!string.IsNullOrEmpty(txtPath.Text) && Directory.Exists(txtPath.Text))
                {
                    dlg.SelectedPath = txtPath.Text;
                }
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtPath.Text = dlg.SelectedPath;
                }
            }
        }

        private async void BtnApply_Click(object sender, EventArgs e)
        {
            string gamePath = txtPath.Text.Trim();
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                MessageBox.Show(this, "유효한 게임 폴더를 먼저 선택해주세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // RPCS3 running check
            if (Process.GetProcessesByName("rpcs3").Length > 0)
            {
                MessageBox.Show(this, "RPCS3 에뮬레이터가 현재 실행 중입니다.\nRPCS3를 완전히 종료한 후 다시 시도해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnBrowse.Enabled = false;
            btnApply.Enabled = false;
            txtPath.Enabled = false;

            try
            {
                await Task.Run(() => PerformPatch(gamePath));
                MessageBox.Show(this, "사카츠쿠 2013 한글패치가 성공적으로 적용되었습니다!\nRPCS3에서 게임을 실행하세요.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "패치 적용 중 오류가 발생했습니다:\n" + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "오류 발생: " + ex.Message;
                lblStatus.ForeColor = Color.Red;
            }
            finally
            {
                btnBrowse.Enabled = true;
                btnApply.Enabled = true;
                txtPath.Enabled = true;
            }
        }

        private void SetStatus(string text, int percent)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SetStatus(text, percent)));
                return;
            }
            lblStatus.Text = text;
            if (percent >= 0 && percent <= 100)
            {
                progressBar.Value = percent;
            }
        }

        private void PerformPatch(string rootPath)
        {
            PatchEngine.Run(rootPath, false, SetStatus);
        }

        private static string HashBytes(SHA256 sha, byte[] bytes)
        {
            byte[] hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }

        private class PendingPatch
        {
            public PatchItem Item { get; set; }
            public string DestPath { get; set; }
            public byte[] PatchedBytes { get; set; }
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 2 && (args[0] == "--verify" || args[0] == "--apply")) {
                try { PatchEngine.Run(args[1], args[0] == "--verify", (s,p) => Console.WriteLine(s)); return 0; }
                catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string defaultPath = args.Length > 0 ? args[0] : null;
            if (string.IsNullOrEmpty(defaultPath))
            {
                // Auto detect if placed inside game folder
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string parentDir = Path.GetDirectoryName(baseDir.TrimEnd('\\', '/'));
                if (Directory.Exists(Path.Combine(baseDir, @"PS3_GAME\USRDIR\data"))) defaultPath = baseDir;
                else if (parentDir != null && Directory.Exists(Path.Combine(parentDir, @"PS3_GAME\USRDIR\data"))) defaultPath = parentDir;
            }

            Application.Run(new MainForm(defaultPath));
            return 0;
        }
    }
}
