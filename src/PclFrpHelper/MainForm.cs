using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PclFrpHelper
{
    // ---------------- Main window ----------------

    internal class MainForm : Form
    {
        private const string Ua = "PCL-FRP-Helper/0.2";
        private readonly string _baseDir = AppDomain.CurrentDomain.BaseDirectory;
        private string BinDir { get { return Path.Combine(_baseDir, "bin"); } }
        private string ConfigPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL-FRP", "config.ini"); } }

        private Process _proc;
        private string _publicAddress = "";
        private readonly List<Tunnel> _tunnels = new List<Tunnel>();

        private GlTabStrip _tabs;
        private Panel _pages;
        private Panel _pageOf, _pageSk, _pageFrp;

        private GlTextBox _ofToken, _ofLog;
        private GlList _ofList;
        private Label _ofStatus;

        private GlTextBox _skToken, _skTunnelId, _skLog;
        private GlList _skList;
        private Label _skStatus, _skInfo;

        private GlTextBox _frpServer, _frpPort, _frpToken, _frpLocal, _frpRemote, _frpLog;
        private GlButton _frpType;
        private Label _frpStatus;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }

        private bool _dragging;
        private Point _dragOffset;

        public MainForm()
        {
            Text = "PCL FRP 联机助手";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(800, 660);
            MinimumSize = new Size(740, 580);
            DoubleBuffered = true;
            Font = Glass.F(9f);
            BackColor = Glass.PageBg;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            BuildChrome();
            BuildPages();

            LoadConfig();
            _tabs.SelectedIndex = 2;
            _tabs.SelectedIndex = 0;
            ShowPage(0);
        }

        private void BuildChrome()
        {
            var titleBar = new GlSurface { Dock = DockStyle.Top, Height = 48 };
            titleBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragOffset = e.Location; } };
            titleBar.MouseMove += (s, e) => { if (_dragging) { var sp = titleBar.PointToScreen(e.Location); Location = new Point(sp.X - _dragOffset.X - 24, sp.Y - _dragOffset.Y - 8); } };
            titleBar.MouseUp += (s, e) => _dragging = false;
            titleBar.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var logo = new Rectangle(22, 16, 16, 16);
                using (var p = Glass.Round(logo, 8))
                using (var b = new LinearGradientBrush(logo, Color.FromArgb(110, 190, 255), Glass.Accent, LinearGradientMode.Vertical))
                    g.FillPath(b, p);
                TextRenderer.DrawText(g, "PCL FRP 联机助手", Glass.F(10.5f, FontStyle.Bold), new Rectangle(48, 0, 340, titleBar.Height), Glass.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };

            var btnClose = new GlButton { Text = "✕", Width = 34, Height = 34, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(ClientSize.Width - 48, 7) };
            btnClose.Click += (s, e) => Close();
            var btnMin = new GlButton { Text = "—", Width = 34, Height = 34, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(ClientSize.Width - 88, 7) };
            btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            Controls.Add(btnMin);
            Controls.Add(btnClose);
            Controls.Add(titleBar);

            _tabs = new GlTabStrip { Location = new Point(24, 56), Width = 420 };
            _tabs.SetTabs("OpenFrp", "樱花FRP", "公版 FRP");
            _tabs.SelectedIndexChanged += (s, e) => ShowPage(_tabs.SelectedIndex);
            Controls.Add(_tabs);

            _pages = new GlSurface { Location = new Point(0, 100), Width = ClientSize.Width, Height = ClientSize.Height - 100, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(_pages);
        }

        private void ShowPage(int index)
        {
            SuspendLayout();
            _pageOf.Visible = index == 0;
            _pageSk.Visible = index == 1;
            _pageFrp.Visible = index == 2;
            ResumeLayout(true);
            _pages.Invalidate(true);
        }

        private void BuildPages()
        {
            _pageOf = MakePage(); _pageSk = MakePage(); _pageFrp = MakePage();
            _pages.Controls.Add(_pageOf); _pages.Controls.Add(_pageSk); _pages.Controls.Add(_pageFrp);
            BuildOpenFrpPage(_pageOf);
            BuildSakuraPage(_pageSk);
            BuildVanillaPage(_pageFrp);
        }

        private Panel MakePage() { return new GlSurface { Dock = DockStyle.Fill }; }

        private Label Caption(string text, int x, int y, int w = 60)
        {
            return new GlLabel { Text = text, Location = new Point(x, y), Size = new Size(w, 22), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Glass.SubText, Font = Glass.F(9f) };
        }

        // ---------- pages ----------

        private void BuildOpenFrpPage(Panel page)
        {
            page.Controls.Add(Caption("用户密钥", 24, 18, 62));
            _ofToken = new GlTextBox { Location = new Point(90, 14), Width = 330 };
            page.Controls.Add(_ofToken);

            var b1 = new GlButton { Text = "刷新隧道", Location = new Point(430, 14), Width = 96 };
            b1.Click += async (s, e) => await RefreshOpenFrpAsync();
            var b2 = new GlButton { Text = "获取密钥", Location = new Point(534, 14), Width = 96 };
            b2.Click += (s, e) => OpenUrl("https://console.openfrp.net/usercenter");
            var b3 = new GlButton { Text = "控制台", Location = new Point(638, 14), Width = 80 };
            b3.Click += (s, e) => OpenUrl("https://console.openfrp.net/");
            page.Controls.Add(b1); page.Controls.Add(b2); page.Controls.Add(b3);

            _ofList = new GlList { Location = new Point(24, 56), Width = 752, Height = 170, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _ofList.Headers = new[] { "节点", "隧道", "类型", "公网地址" };
            _ofList.Weights = new[] { 1.6f, 1f, 0.6f, 2.2f };
            page.Controls.Add(_ofList);

            var s1 = new GlButton { Text = "启动隧道", Location = new Point(24, 240), Width = 104, Primary = true };
            s1.Click += async (s, e) => await StartOpenFrpAsync();
            var s2 = new GlButton { Text = "停止", Location = new Point(136, 240), Width = 76 };
            s2.Click += (s, e) => StopProcess();
            var s3 = new GlButton { Text = "复制地址", Location = new Point(220, 240), Width = 96 };
            s3.Click += (s, e) => CopyAddress();
            var s4 = new GlButton { Text = "测本地端口", Location = new Point(324, 240), Width = 108 };
            s4.Click += (s, e) => CheckLocalPort(25565);
            page.Controls.Add(s1); page.Controls.Add(s2); page.Controls.Add(s3); page.Controls.Add(s4);

            _ofStatus = new GlLabel { Text = "未连接", Location = new Point(24, 282), Size = new Size(752, 20), ForeColor = Glass.SubText };
            page.Controls.Add(_ofStatus);

            _ofLog = new GlTextBox { Location = new Point(24, 306), Width = 752, Height = 236, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _ofLog.MultilineBox = true; _ofLog.ReadOnlyBox = true; _ofLog.ScrollBarsBox = ScrollBars.Vertical;
            page.Controls.Add(_ofLog);
        }

        private void BuildSakuraPage(Panel page)
        {
            page.Controls.Add(Caption("访问密钥", 24, 18, 62));
            _skToken = new GlTextBox { Location = new Point(90, 14), Width = 330 };
            page.Controls.Add(_skToken);

            var b1 = new GlButton { Text = "读取账号", Location = new Point(430, 14), Width = 96 };
            b1.Click += async (s, e) => await LoadSakuraAsync();
            var b2 = new GlButton { Text = "获取密钥", Location = new Point(534, 14), Width = 96 };
            b2.Click += (s, e) => OpenUrl("https://www.natfrp.com/user/");
            var b3 = new GlButton { Text = "控制台", Location = new Point(638, 14), Width = 80 };
            b3.Click += (s, e) => OpenUrl("https://www.natfrp.com/tunnel/");
            page.Controls.Add(b1); page.Controls.Add(b2); page.Controls.Add(b3);

            _skInfo = new GlLabel { Text = "填访问密钥后点「读取账号」。", Location = new Point(24, 50), Size = new Size(752, 22), ForeColor = Glass.SubText };
            page.Controls.Add(_skInfo);

            _skList = new GlList { Location = new Point(24, 78), Width = 752, Height = 150, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _skList.Headers = new[] { "ID", "名称", "类型", "节点 / 地址" };
            _skList.Weights = new[] { 1f, 1.6f, 0.8f, 2.4f };
            _skList.SelectionChanged += (s, e) => { if (_skList.SelectedTag != null) _skTunnelId.BoxText = Convert.ToString(_skList.SelectedTag); };
            page.Controls.Add(_skList);

            page.Controls.Add(Caption("隧道ID", 24, 244, 50));
            _skTunnelId = new GlTextBox { Location = new Point(82, 240), Width = 110 };
            page.Controls.Add(_skTunnelId);

            var s1 = new GlButton { Text = "启动隧道", Location = new Point(202, 240), Width = 104, Primary = true };
            s1.Click += async (s, e) => await StartSakuraAsync();
            var s2 = new GlButton { Text = "停止", Location = new Point(314, 240), Width = 76 };
            s2.Click += (s, e) => StopProcess();
            var s3 = new GlButton { Text = "下载 frpc", Location = new Point(398, 240), Width = 100 };
            s3.Click += async (s, e) => await Task.Run(() => EnsureSakuraFrpc(true));
            page.Controls.Add(s1); page.Controls.Add(s2); page.Controls.Add(s3);

            _skStatus = new GlLabel { Text = "未连接", Location = new Point(24, 282), Size = new Size(752, 20), ForeColor = Glass.SubText };
            page.Controls.Add(_skStatus);

            _skLog = new GlTextBox { Location = new Point(24, 306), Width = 752, Height = 236, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _skLog.MultilineBox = true; _skLog.ReadOnlyBox = true; _skLog.ScrollBarsBox = ScrollBars.Vertical;
            page.Controls.Add(_skLog);
        }

        private void BuildVanillaPage(Panel page)
        {
            page.Controls.Add(Caption("服务器地址", 24, 18, 72));
            _frpServer = new GlTextBox { Location = new Point(102, 14), Width = 260 };
            page.Controls.Add(_frpServer);

            page.Controls.Add(Caption("端口", 376, 18, 36));
            _frpPort = new GlTextBox { Location = new Point(416, 14), Width = 90, BoxText = "7000" };
            page.Controls.Add(_frpPort);

            page.Controls.Add(Caption("token", 24, 58, 60));
            _frpToken = new GlTextBox { Location = new Point(102, 54), Width = 404 };
            page.Controls.Add(_frpToken);

            page.Controls.Add(Caption("本地端口", 24, 98, 72));
            _frpLocal = new GlTextBox { Location = new Point(102, 94), Width = 90, BoxText = "25565" };
            page.Controls.Add(_frpLocal);

            page.Controls.Add(Caption("远程端口", 208, 98, 72));
            _frpRemote = new GlTextBox { Location = new Point(284, 94), Width = 90, BoxText = "25565" };
            page.Controls.Add(_frpRemote);

            page.Controls.Add(Caption("协议", 394, 98, 40));
            _frpType = new GlButton { Text = "TCP", Location = new Point(438, 94), Width = 74 };
            _frpType.Click += (s, e) => _frpType.Text = _frpType.Text == "TCP" ? "UDP" : "TCP";
            page.Controls.Add(_frpType);

            var s1 = new GlButton { Text = "启动", Location = new Point(24, 142), Width = 90, Primary = true };
            s1.Click += async (s, e) => await StartVanillaAsync();
            var s2 = new GlButton { Text = "停止", Location = new Point(122, 142), Width = 76 };
            s2.Click += (s, e) => StopProcess();
            var s3 = new GlButton { Text = "下载 frpc", Location = new Point(206, 142), Width = 100 };
            s3.Click += async (s, e) => await Task.Run(() => EnsureVanillaFrpc(true));
            page.Controls.Add(s1); page.Controls.Add(s2); page.Controls.Add(s3);

            _frpStatus = new GlLabel { Text = "未连接", Location = new Point(24, 184), Size = new Size(752, 20), ForeColor = Glass.SubText };
            page.Controls.Add(_frpStatus);

            _frpLog = new GlTextBox { Location = new Point(24, 208), Width = 752, Height = 320, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _frpLog.MultilineBox = true; _frpLog.ReadOnlyBox = true; _frpLog.ScrollBarsBox = ScrollBars.Vertical;
            page.Controls.Add(_frpLog);
        }

        // ---------- OpenFrp logic ----------

        private async Task RefreshOpenFrpAsync()
        {
            var token = _ofToken.BoxText.Trim();
            if (token.Length == 0) { MessageBox.Show("请先填写 OpenFrp 用户密钥（控制台 → 个人中心 → 用户密钥）。", "提示"); return; }
            SaveConfig();
            _ofStatus.Text = "正在获取隧道列表…";
            try
            {
                var json = await Task.Run(() => HttpGet("https://api.openfrp.net/api?action=getallproxies&user=" + Uri.EscapeDataString(token)));
                var ser = new JavaScriptSerializer();
                var root = (Dictionary<string, object>)ser.DeserializeObject(json);
                _tunnels.Clear();
                _ofList.Clear();
                if (root.ContainsKey("data") && root["data"] is object[])
                {
                    foreach (var n in (object[])root["data"])
                    {
                        var node = (Dictionary<string, object>)n;
                        var nodeName = node.ContainsKey("node") ? Convert.ToString(node["node"]) : "";
                        if (!(node.ContainsKey("proxies") && node["proxies"] is object[])) continue;
                        foreach (var p in (object[])node["proxies"])
                        {
                            var px = (Dictionary<string, object>)p;
                            var t = new Tunnel
                            {
                                Node = nodeName,
                                Id = px.ContainsKey("id") ? Convert.ToString(px["id"]) : "",
                                Name = px.ContainsKey("name") ? Convert.ToString(px["name"]) : "",
                                Type = px.ContainsKey("type") ? Convert.ToString(px["type"]) : "",
                                Local = px.ContainsKey("local") ? Convert.ToString(px["local"]) : "",
                                Remote = px.ContainsKey("remote") ? Convert.ToString(px["remote"]) : ""
                            };
                            _tunnels.Add(t);
                            _ofList.Add(new[] { t.Node, t.Name, t.Type, t.Remote }, t);
                        }
                    }
                }
                _ofStatus.Text = "共 " + _tunnels.Count + " 条隧道" + (root.ContainsKey("message") ? "（" + root["message"] + "）" : "");
                AppendLog(_ofLog, "已刷新隧道列表，共 " + _tunnels.Count + " 条");
            }
            catch (Exception ex)
            {
                _ofStatus.Text = "获取失败";
                AppendLog(_ofLog, "获取隧道失败：" + ex.Message);
            }
        }

        private async Task StartOpenFrpAsync()
        {
            var t = _ofList.SelectedTag as Tunnel;
            if (t == null) { MessageBox.Show("请先在列表里选择一条隧道。", "提示"); return; }
            if (t.Type != "tcp" && t.Type != "udp") { MessageBox.Show("这条隧道类型是 " + t.Type + "，不是 tcp/udp，无法用于 Minecraft。", "提示"); return; }
            var token = _ofToken.BoxText.Trim();
            var exe = await Task.Run(() => EnsureOpenFrpFrpc());
            if (exe == null) return;
            StopProcess();
            _publicAddress = t.Remote;
            StartProcess(exe, "-u \"" + token + "\" -p " + t.Id, _ofLog, _ofStatus);
            AppendLog(_ofLog, "启动隧道 " + t.Name + "（" + t.Node + "），公网地址：" + t.Remote);
        }

        private string EnsureOpenFrpFrpc()
        {
            var dir = Path.Combine(BinDir, "openfrp");
            var exe = Path.Combine(dir, "frpc.exe");
            if (File.Exists(exe)) return exe;
            try
            {
                Directory.CreateDirectory(dir);
                var json = HttpGet("https://api.openfrp.net/commonQuery/get?key=software");
                var ser = new JavaScriptSerializer();
                var root = (Dictionary<string, object>)ser.DeserializeObject(json);
                var data = (Dictionary<string, object>)root["data"];
                var latest = Convert.ToString(data["latest_full"]);
                string src = "";
                if (data["source"] is object[] && ((object[])data["source"]).Length > 0)
                    src = Convert.ToString(((Dictionary<string, object>)((object[])data["source"])[0])["value"]);
                if (src.Length == 0) throw new Exception("没有拿到下载源");
                var url = src.TrimEnd('/') + "/" + latest + "/frpc_windows_amd64.zip";
                AppendLog(_ofLog, "正在下载 OpenFrp frpc：" + url);
                var tmp = Path.Combine(Path.GetTempPath(), "pcl-frp-openfrp.zip");
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", Ua);
                    wc.DownloadFile(url, tmp);
                }
                using (var zip = ZipFile.OpenRead(tmp))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            entry.ExtractToFile(exe, true);
                            break;
                        }
                    }
                }
                File.Delete(tmp);
                if (!File.Exists(exe)) throw new Exception("压缩包里没有找到 exe");
                AppendLog(_ofLog, "OpenFrp frpc 已就绪：" + exe);
                return exe;
            }
            catch (Exception ex)
            {
                MessageBox.Show("下载 OpenFrp frpc 失败：" + ex.Message + "\r\n\r\n也可以手动把 frpc.exe 放到：\r\n" + dir, "下载失败");
                return null;
            }
        }

        // ---------- Vanilla ----------

        private async Task StartVanillaAsync()
        {
            var exe = await Task.Run(() => EnsureVanillaFrpc(false));
            if (exe == null) return;
            var proto = _frpType.Text.ToLower();
            var toml = "serverAddr = \"" + _frpServer.BoxText.Trim() + "\"\r\n" +
                       "serverPort = " + _frpPort.BoxText.Trim() + "\r\n" +
                       (_frpToken.BoxText.Trim().Length > 0 ? "auth.method = \"token\"\r\nauth.token = \"" + _frpToken.BoxText.Trim() + "\"\r\n" : "") +
                       "\r\n[[proxies]]\r\n" +
                       "name = \"mc\"\r\n" +
                       "type = \"" + proto + "\"\r\n" +
                       "localIP = \"127.0.0.1\"\r\n" +
                       "localPort = " + _frpLocal.BoxText.Trim() + "\r\n" +
                       "remotePort = " + _frpRemote.BoxText.Trim() + "\r\n";
            var cfg = Path.Combine(BinDir, "frpc.toml");
            Directory.CreateDirectory(BinDir);
            File.WriteAllText(cfg, toml, new UTF8Encoding(false));
            StopProcess();
            StartProcess(exe, "-c \"" + cfg + "\"", _frpLog, _frpStatus);
            AppendLog(_frpLog, "已启动公版 FRP，服务器 " + _frpServer.BoxText.Trim() + ":" + _frpPort.BoxText.Trim());
            SaveConfig();
        }

        private string EnsureVanillaFrpc(bool force)
        {
            var exe = Path.Combine(BinDir, "frpc.exe");
            if (File.Exists(exe) && !force) return exe;
            try
            {
                Directory.CreateDirectory(BinDir);
                var url = "https://github.com/fatedier/frp/releases/download/v0.61.1/frp_0.61.1_windows_amd64.zip";
                var tmp = Path.Combine(Path.GetTempPath(), "pcl-frp-vanilla.zip");
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", Ua);
                    wc.DownloadFile(url, tmp);
                }
                using (var zip = ZipFile.OpenRead(tmp))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Name.Equals("frpc.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            entry.ExtractToFile(exe, true);
                            break;
                        }
                    }
                }
                File.Delete(tmp);
                return File.Exists(exe) ? exe : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("下载 frpc 失败：" + ex.Message + "\r\n\r\n请手动把 frpc.exe 放到：" + BinDir, "下载失败");
                return null;
            }
        }

        // ---------- Sakura ----------

        private async Task LoadSakuraAsync()
        {
            var key = _skToken.BoxText.Trim();
            if (key.Length == 0) { MessageBox.Show("请填写樱花FRP 访问密钥（面板 → 用户信息 → 访问密钥）。", "提示"); return; }
            _skStatus.Text = "正在读取…";
            try
            {
                var infoJson = await Task.Run(() => SakuraGet("/v4/user/info", key));
                var tunnelJson = await Task.Run(() => SakuraGet("/v4/tunnels", key));
                var ser = new JavaScriptSerializer();
                var info = (Dictionary<string, object>)ser.DeserializeObject(infoJson);
                var name = info.ContainsKey("name") ? Convert.ToString(info["name"]) : "";
                var group = "";
                if (info.ContainsKey("group") && info["group"] is Dictionary<string, object>)
                    group = Convert.ToString(((Dictionary<string, object>)info["group"])["name"]);
                var traffic = "";
                if (info.ContainsKey("traffic") && info["traffic"] is object[])
                {
                    var arr = (object[])info["traffic"];
                    if (arr.Length >= 2) traffic = "流量 已用 " + Convert.ToInt64(arr[0]) / 1024 / 1024 / 1024 + " GB / 共 " + Convert.ToInt64(arr[1]) / 1024 / 1024 / 1024 + " GB";
                }
                var speed = info.ContainsKey("speed") ? Convert.ToString(info["speed"]) : "";
                _skInfo.Text = "账号：" + name + "（" + group + "）   " + speed + "   " + traffic;

                _skList.Clear();
                if (tunnelJson.Trim() != "[]")
                {
                    var arr = (object[])ser.DeserializeObject(tunnelJson);
                    foreach (var t in arr)
                    {
                        var d = (Dictionary<string, object>)t;
                        var id = d.ContainsKey("id") ? Convert.ToString(d["id"]) : "";
                        var nm = d.ContainsKey("name") ? Convert.ToString(d["name"]) : "";
                        var tp = d.ContainsKey("type") ? Convert.ToString(d["type"]) : "";
                        var node = d.ContainsKey("node") ? Convert.ToString(d["node"]) : "";
                        var remote = d.ContainsKey("remote") ? Convert.ToString(d["remote"]) : "";
                        _skList.Add(new[] { id, nm, tp, node + " " + remote }, id);
                    }
                    _skStatus.Text = "共 " + _skList.RowCount + " 条隧道";
                }
                else
                {
                    _skStatus.Text = "账号已连接，但这个账号下还没有隧道。请去面板创建一条 TCP 隧道（本地 25565），再点「读取账号」。";
                }
            }
            catch (Exception ex)
            {
                _skStatus.Text = "读取失败：" + ex.Message;
            }
        }

        private async Task StartSakuraAsync()
        {
            var key = _skToken.BoxText.Trim();
            var id = _skTunnelId.BoxText.Trim();
            if (id.Length == 0 && _skList.SelectedTag != null) id = Convert.ToString(_skList.SelectedTag);
            if (key.Length == 0 || id.Length == 0) { MessageBox.Show("请填写访问密钥，并在列表里选中一条隧道（或直接填隧道ID）。", "提示"); return; }
            var exe = await Task.Run(() => EnsureSakuraFrpc(false));
            if (exe == null) return;
            StopProcess();
            StartProcess(exe, "-f \"" + key + ":" + id + "\" --disable_log_color", _skLog, _skStatus);
            AppendLog(_skLog, "启动樱花隧道 " + id);
        }

        private string EnsureSakuraFrpc(bool force)
        {
            var dir = Path.Combine(BinDir, "sakura");
            var exe = Path.Combine(dir, "frpc.exe");
            if (File.Exists(exe) && !force) return exe;
            try
            {
                Directory.CreateDirectory(dir);
                const string url = "https://nya.globalslb.net/natfrp/client/frpc/0.51.0-sakura-15/frpc_windows_amd64.exe";
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", Ua);
                    wc.DownloadFile(url, exe);
                }
                return exe;
            }
            catch (Exception ex)
            {
                MessageBox.Show("下载樱花 frpc 失败：" + ex.Message + "\r\n\r\n可手动把 frpc 放到：" + dir, "下载失败");
                return null;
            }
        }

        private static string SakuraGet(string path, string key)
        {
            var req = (HttpWebRequest)WebRequest.Create("https://api.natfrp.com" + path);
            req.UserAgent = Ua;
            req.Headers.Add("Authorization", "Bearer " + key);
            req.Timeout = 20000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        // ---------- process / utils ----------

        private void StartProcess(string exe, string args, GlTextBox log, Label status)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendLog(log, e.Data); };
                p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendLog(log, e.Data); };
                p.Exited += (s, e) => { try { BeginInvoke((Action)(() => { status.Text = "已停止"; })); } catch { } };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                _proc = p;
                status.Text = "运行中（PID " + p.Id + "）";
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动失败：" + ex.Message, "错误");
            }
        }

        private void StopProcess()
        {
            try
            {
                if (_proc != null && !_proc.HasExited)
                {
                    _proc.Kill();
                    _proc.WaitForExit(3000);
                }
            }
            catch { }
            _proc = null;
        }

        private void CopyAddress()
        {
            if (_publicAddress.Length == 0) { MessageBox.Show("还没有拿到公网地址。", "提示"); return; }
            Clipboard.SetText(_publicAddress);
            MessageBox.Show("已复制：" + _publicAddress, "提示");
        }

        private void CheckLocalPort(int port)
        {
            try
            {
                var c = new System.Net.Sockets.TcpClient();
                var ar = c.BeginConnect("127.0.0.1", port, null, null);
                var ok = ar.AsyncWaitHandle.WaitOne(800);
                c.Close();
                MessageBox.Show(ok ? "本地 127.0.0.1:" + port + " 正在监听，可以开穿透。" : "本地 127.0.0.1:" + port + " 没有监听，先在游戏里「对局域网开放」。", "检测");
            }
            catch { }
        }

        private static void AppendLog(GlTextBox box, string line)
        {
            if (box == null) return;
            line = System.Text.RegularExpressions.Regex.Replace(line ?? "", "\u001b\\[[0-9;]*[A-Za-z]", "").Trim();
            if (line.Length == 0) return;
            try
            {
                box.Inner.BeginInvoke((Action)(() =>
                {
                    box.Inner.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + "\r\n");
                }));
            }
            catch { }
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(url); } catch { }
        }

        private static string HttpGet(string url)
        {
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", Ua);
                wc.Encoding = Encoding.UTF8;
                return wc.DownloadString(url);
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                foreach (var line in File.ReadAllLines(ConfigPath))
                {
                    var i = line.IndexOf('=');
                    if (i <= 0) continue;
                    var k = line.Substring(0, i); var v = line.Substring(i + 1);
                    switch (k)
                    {
                        case "of_token": _ofToken.BoxText = v; break;
                        case "sk_token": _skToken.BoxText = v; break;
                        case "frp_server": _frpServer.BoxText = v; break;
                        case "frp_port": _frpPort.BoxText = v; break;
                        case "frp_token": _frpToken.BoxText = v; break;
                        case "frp_local": _frpLocal.BoxText = v; break;
                        case "frp_remote": _frpRemote.BoxText = v; break;
                    }
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                var sb = new StringBuilder();
                sb.AppendLine("of_token=" + _ofToken.BoxText.Trim());
                sb.AppendLine("sk_token=" + _skToken.BoxText.Trim());
                sb.AppendLine("frp_server=" + _frpServer.BoxText.Trim());
                sb.AppendLine("frp_port=" + _frpPort.BoxText.Trim());
                sb.AppendLine("frp_token=" + _frpToken.BoxText.Trim());
                sb.AppendLine("frp_local=" + _frpLocal.BoxText.Trim());
                sb.AppendLine("frp_remote=" + _frpRemote.BoxText.Trim());
                File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        // ---------- painting ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Glass.PaintScene(g, ClientRectangle);
            using (var p = Glass.Round(new Rectangle(0, 0, Width - 1, Height - 1), 20))
            using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1.4f))
                g.DrawPath(pen, p);
            base.OnPaint(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            try
            {
                using (var p = Glass.Round(new Rectangle(0, 0, Width, Height), 20))
                    Region = new Region(p);
            }
            catch { }
            if (_pages != null)
            {
                _pages.Width = ClientSize.Width;
                _pages.Height = ClientSize.Height - 100;
            }
        }

        private static void Blob(Graphics g, int x, int y, int size, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(x, y, size, size);
                using (var b = new PathGradientBrush(path))
                {
                    b.CenterColor = color;
                    b.SurroundColors = new[] { Color.FromArgb(0, color) };
                    g.FillPath(b, path);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopProcess();
            SaveConfig();
            base.OnFormClosing(e);
        }
    }}
