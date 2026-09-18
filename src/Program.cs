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
            this.Text = "사카츠쿠 2013 (PS3) 한글패치 v1.0";
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
            SetStatus("게임 데이터 폴더 탐색 중...", 5);

            string dataDir = null;
            string[] candidates = new string[]
            {
                Path.Combine(rootPath, @"PS3_GAME\USRDIR\data"),
                Path.Combine(rootPath, @"USRDIR\data"),
                Path.Combine(rootPath, "data"),
                rootPath
            };

            foreach (var c in candidates)
            {
                if (File.Exists(Path.Combine(c, "font.par")))
                {
                    dataDir = c;
                    break;
                }
            }

            if (dataDir == null)
            {
                throw new Exception("선택한 폴더에서 사카츠쿠 게임 데이터(font.par)를 찾을 수 없습니다.\nPS3_GAME 폴더가 포함된 올바른 게임 폴더를 지정해주세요.");
            }

            SetStatus("패치 데이터 로드 중...", 10);
            Stream patchStream = null;
            string localPatch = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "korean.patch");
            if (File.Exists(localPatch))
            {
                patchStream = File.OpenRead(localPatch);
            }
            else
            {
                var asm = Assembly.GetExecutingAssembly();
                patchStream = asm.GetManifestResourceStream("korean.patch");
            }

            if (patchStream == null)
            {
                throw new Exception("패치 데이터(korean.patch)를 찾을 수 없습니다.");
            }

            using (patchStream)
            using (var zip = new ZipArchive(patchStream, ZipArchiveMode.Read))
            {
                var manifestEntry = zip.GetEntry("manifest.json");
                if (manifestEntry == null) throw new Exception("패치 매니페스트를 찾을 수 없습니다.");

                PatchManifest manifest;
                using (var reader = manifestEntry.Open())
                {
                    var serializer = new DataContractJsonSerializer(typeof(PatchManifest));
                    manifest = (PatchManifest)serializer.ReadObject(reader);
                }

                if (manifest.Format != "sakatsuku-xor-v1")
                {
                    throw new Exception("지원하지 않는 패치 형식입니다.");
                }

                SetStatus("원본 파일 무결성 검증 중...", 20);

                var pending = new List<PendingPatch>();
                int totalFiles = manifest.Files.Count;
                int currentFile = 0;
                int alreadyCount = 0;

                using (var sha = SHA256.Create())
                {
                    foreach (var item in manifest.Files)
                    {
                        currentFile++;
                        string dest = Path.Combine(dataDir, item.Path.Replace('/', '\\'));
                        if (!File.Exists(dest))
                        {
                            throw new Exception("원본 파일 누락: " + item.Path);
                        }

                        byte[] bytes = File.ReadAllBytes(dest);
                        string currentHash = HashBytes(sha, bytes);

                        if (string.Equals(currentHash, item.TargetSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyCount++;
                            continue;
                        }

                        if (!string.Equals(currentHash, item.OriginalSha256, StringComparison.OrdinalIgnoreCase) || bytes.Length != item.Size)
                        {
                            throw new Exception("원본 파일 버전 불일치: " + item.Path + "\n변조되지 않은 정품 원본 게임 파일을 사용해주세요.");
                        }

                        var payloadEntry = zip.GetEntry(item.Payload);
                        if (payloadEntry == null) throw new Exception("페이로드 누락: " + item.Payload);

                        byte[] delta;
                        using (var ms = new MemoryStream())
                        using (var ps = payloadEntry.Open())
                        {
                            ps.CopyTo(ms);
                            delta = ms.ToArray();
                        }

                        if (delta.Length != bytes.Length) throw new Exception("페이로드 길이 불일치: " + item.Path);

                        // In-memory XOR
                        for (int i = 0; i < bytes.Length; i++)
                        {
                            bytes[i] = (byte)(bytes[i] ^ delta[i]);
                        }

                        if (!string.Equals(HashBytes(sha, bytes), item.TargetSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception("패치 후 체크섬 검증 실패: " + item.Path);
                        }

                        pending.Add(new PendingPatch
                        {
                            Item = item,
                            DestPath = dest,
                            PatchedBytes = bytes
                        });

                        int percent = 20 + (int)((float)currentFile / totalFiles * 40);
                        SetStatus(string.Format("검증 중: {0} ({1}/{2})", Path.GetFileName(item.Path), currentFile, totalFiles), percent);
                    }

                    if (pending.Count == 0)
                    {
                        SetStatus("이미 모든 파일이 한글 패치되어 있습니다.", 100);
                        return;
                    }

                    // Backup originals
                    string parentDir = Path.GetDirectoryName(dataDir);
                    string backupDir = Path.Combine(parentDir, "KoreanPatch_Backup_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(backupDir);

                    SetStatus("원본 백업 중...", 65);
                    foreach (var p in pending)
                    {
                        string saved = Path.Combine(backupDir, p.Item.Path.Replace('/', '\\'));
                        Directory.CreateDirectory(Path.GetDirectoryName(saved));
                        File.Copy(p.DestPath, saved, false);
                    }

                    // Apply patched bytes with rollback safety
                    SetStatus("한글 패치 파일 적용 중...", 75);
                    var written = new List<PendingPatch>();
                    try
                    {
                        int done = 0;
                        foreach (var p in pending)
                        {
                            written.Add(p);
                            File.WriteAllBytes(p.DestPath, p.PatchedBytes);
                            done++;
                            int percent = 75 + (int)((float)done / pending.Count * 25);
                            SetStatus(string.Format("적용 중: {0} ({1}/{2})", Path.GetFileName(p.Item.Path), done, pending.Count), percent);
                        }
                    }
                    catch (Exception)
                    {
                        // Rollback
                        foreach (var w in written)
                        {
                            string saved = Path.Combine(backupDir, w.Item.Path.Replace('/', '\\'));
                            if (File.Exists(saved)) File.Copy(saved, w.DestPath, true);
                        }
                        throw;
                    }
                }

                SetStatus("패치 완료! 백업 폴더가 생성되었습니다.", 100);
            }
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
        static void Main(string[] args)
        {
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
        }
    }
}
