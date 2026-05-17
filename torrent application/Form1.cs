using System;
using System.IO;
using Torrent.Core.Models;
using Torrent.Core.Runtime;

namespace TorrentApplication;

public partial class Form1 : Form
{
    private TextBox _txtTrackerHost = null!;
    private TextBox _txtTrackerPort = null!;
    private TextBox _txtAdvertisedHost = null!;
    private TextBox _txtListenPort = null!;
    private Label _lblBoundPort = null!;

    private TextBox _txtSeedFile = null!;
    private Label _lblMeta = null!;

    private TextBox _txtMeta = null!;
    private TextBox _txtOutput = null!;
    private ProgressBar _progress = null!;
    private Label _lblProgress = null!;

    private TextBox _txtLog = null!;

    private TorrentNode? _node;
    private CancellationTokenSource? _downloadCts;
    private Task? _downloadTask;
    private bool _isClosingAfterCancel;

    public Form1()
    {
        InitializeComponent();
        BuildUi();
    }

    private void BuildUi()
    {
        Text = "torrent application";
        Width = 940;
        Height = 760;
        Font = new Font("Segoe UI", 10);

        GroupBox net = new() { Text = "Node + Tracker", Left = 10, Top = 10, Width = 900, Height = 130 };
        _txtTrackerHost = Tb("127.0.0.1", 15, 40, 140);
        _txtTrackerPort = Tb("7070", 165, 40, 90);
        _txtAdvertisedHost = Tb("127.0.0.1", 265, 40, 140);
        _txtListenPort = Tb("0", 415, 40, 90);
        Button startNode = Btn("Node Baslat", 515, 38, 130, async (_, _) => await StartNodeAsync(false));
        Button auto = Btn("Auto Port", 655, 38, 110, async (_, _) => await StartNodeAsync(true));
        _lblBoundPort = new Label { Left = 15, Top = 85, Width = 600, Text = "Bound Port: -" };

        net.Controls.AddRange(new Control[]
        {
            L("Tracker Host", 15, 20), _txtTrackerHost,
            L("Tracker Port", 165, 20), _txtTrackerPort,
            L("Advertised Host", 265, 20), _txtAdvertisedHost,
            L("Listen Port", 415, 20), _txtListenPort,
            startNode, auto, _lblBoundPort
        });

        TabControl tabs = new()
        {
            Left = 10,
            Top = 150,
            Width = 900,
            Height = 330
        };

        TabPage seederTab = new("Seeder");
        BuildSeederTab(seederTab);

        TabPage leecherTab = new("Leecher");
        BuildLeecherTab(leecherTab);

        tabs.TabPages.Add(seederTab);
        tabs.TabPages.Add(leecherTab);

        GroupBox log = new() { Text = "Log", Left = 10, Top = 490, Width = 900, Height = 220 };
        _txtLog = new TextBox
        {
            Left = 15,
            Top = 30,
            Width = 870,
            Height = 175,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true
        };
        log.Controls.Add(_txtLog);

        Controls.Add(net);
        Controls.Add(tabs);
        Controls.Add(log);
    }

    private void BuildSeederTab(Control parent)
    {
        GroupBox seed = new() { Text = "Dosya Paylas", Left = 10, Top = 10, Width = 860, Height = 260 };
        _txtSeedFile = Tb(string.Empty, 15, 45, 600);
        Button pick = Btn("Dosya Sec", 625, 43, 100, (_, _) => PickFile());
        Button startSeed = Btn("Seeding Baslat", 735, 43, 110, async (_, _) => await StartSeedingAsync());
        _lblMeta = new Label { Left = 15, Top = 95, Width = 830, Text = "Meta: -" };

        Button copyMetaToLeecher = Btn("Meta'yi Leecher'a Aktar", 15, 130, 230, (_, _) => CopyMetaToLeecher());

        seed.Controls.AddRange(new Control[]
        {
            L("Paylasilacak Dosya", 15, 25), _txtSeedFile, pick, startSeed, _lblMeta, copyMetaToLeecher
        });

        parent.Controls.Add(seed);
    }

    private void BuildLeecherTab(Control parent)
    {
        GroupBox download = new() { Text = "Dosya Indir", Left = 10, Top = 10, Width = 860, Height = 260 };
        _txtMeta = Tb(string.Empty, 15, 45, 600);
        Button pickMeta = Btn("Meta Sec", 625, 43, 100, (_, _) => PickMeta());

        _txtOutput = Tb(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), 15, 95, 600);
        Button pickOut = Btn("Klasor Sec", 625, 93, 100, (_, _) => PickOutput());

        Button startDownload = Btn("Indir", 735, 68, 110, async (_, _) => await StartDownloadAsync());

        _progress = new ProgressBar { Left = 15, Top = 145, Width = 710, Height = 24, Minimum = 0, Maximum = 100 };
        _lblProgress = new Label { Left = 735, Top = 148, Width = 110, Text = "%0" };

        download.Controls.AddRange(new Control[]
        {
            L(".ttmeta", 15, 25), _txtMeta, pickMeta,
            L("Hedef Klasor", 15, 75), _txtOutput, pickOut,
            startDownload, _progress, _lblProgress
        });

        parent.Controls.Add(download);
    }

    private async Task StartNodeAsync(bool forceAuto)
    {
        if (_downloadTask is { IsCompleted: false })
        {
            MessageBox.Show("Indirme devam ederken node yeniden baslatilamaz.");
            return;
        }

        if (_node != null)
        {
            await _node.DisposeAsync();
            _node = null;
        }

        _node = new TorrentNode(Log);
        int trackerPort = ParsePort(_txtTrackerPort.Text, 7070);
        int listen = forceAuto ? 0 : ParsePort(_txtListenPort.Text, 0);
        PortAllocationResult r = _node.Start(_txtTrackerHost.Text.Trim(), trackerPort, listen, _txtAdvertisedHost.Text.Trim());
        if (!r.Success)
        {
            Log($"Node baslatilamadi: {r.Error}");
            if (MessageBox.Show("Port kullanimda. Auto port denensin mi?", "Port Cakismasi", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                await StartNodeAsync(true);
            }

            return;
        }

        _txtListenPort.Text = r.BoundPort.ToString();
        _lblBoundPort.Text = $"Bound Port: {r.BoundPort} {(r.IsAutoAssigned ? "(Auto)" : "(Manual)")}";
        Log($"Node aktif. Port={r.BoundPort}");
    }

    private async Task StartSeedingAsync()
    {
        if (_node == null)
        {
            MessageBox.Show("Once node baslatin.");
            return;
        }

        string file = _txtSeedFile.Text.Trim();
        if (!File.Exists(file))
        {
            MessageBox.Show("Dosya bulunamadi.");
            return;
        }

        try
        {
            string meta = await _node.StartSeedingAsync(file);
            _lblMeta.Text = $"Meta: {meta}";
            _txtMeta.Text = meta;
            Log("Seeding basladi.");
        }
        catch (Exception ex)
        {
            Log($"Seeding hatasi: {ex.Message}");
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StartDownloadAsync()
    {
        if (_node == null)
        {
            MessageBox.Show("Once node baslatin.");
            return;
        }

        if (_downloadTask is { IsCompleted: false })
        {
            MessageBox.Show("Indirme zaten devam ediyor.");
            return;
        }

        string metaPath = _txtMeta.Text.Trim();
        if (!File.Exists(metaPath))
        {
            MessageBox.Show("Metadata bulunamadi.");
            return;
        }

        string outputDir = _txtOutput.Text.Trim();
        Directory.CreateDirectory(outputDir);

        try
        {
            Progress<DownloadProgress> progress = new(p =>
            {
                int percent = (int)Math.Clamp(p.Percent, 0, 100);
                _progress.Value = percent;
                _lblProgress.Text = $"%{percent}";
                Log($"{p.CompletedPieces}/{p.TotalPieces} - {p.Status}");
            });

            _downloadCts = new CancellationTokenSource();
            _downloadTask = _node.DownloadAsync(metaPath, outputDir, progress, 4, TimeSpan.FromSeconds(10), _downloadCts.Token);

            await _downloadTask;
            Log("Indirme tamamlandi.");
            MessageBox.Show("Indirme tamamlandi.");
        }
        catch (OperationCanceledException)
        {
            Log("Indirme durduruldu. Sonraki acilista kaldigi yerden devam edecek.");
        }
        catch (Exception ex)
        {
            Log($"Indirme hatasi: {ex.Message}");
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
            _downloadTask = null;
        }
    }

    private void PickFile()
    {
        using OpenFileDialog d = new();
        if (d.ShowDialog() == DialogResult.OK)
        {
            _txtSeedFile.Text = d.FileName;
        }
    }

    private void PickMeta()
    {
        using OpenFileDialog d = new() { Filter = "Torrent metadata (*.ttmeta)|*.ttmeta|Tum dosyalar (*.*)|*.*" };
        if (d.ShowDialog() == DialogResult.OK)
        {
            _txtMeta.Text = d.FileName;
        }
    }

    private void PickOutput()
    {
        using FolderBrowserDialog d = new();
        if (d.ShowDialog() == DialogResult.OK)
        {
            _txtOutput.Text = d.SelectedPath;
        }
    }

    private void CopyMetaToLeecher()
    {
        string current = _lblMeta.Text.Replace("Meta: ", string.Empty, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(current) && File.Exists(current))
        {
            _txtMeta.Text = current;
            Log("Meta yolu Leecher sekmesine aktarildi.");
        }
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(Log), message);
            return;
        }

        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static Label L(string t, int x, int y) => new() { Text = t, Left = x, Top = y, AutoSize = true };
    private static TextBox Tb(string t, int x, int y, int w) => new() { Text = t, Left = x, Top = y, Width = w };

    private static Button Btn(string t, int x, int y, int w, EventHandler h)
    {
        Button b = new() { Text = t, Left = x, Top = y, Width = w, Height = 32 };
        b.Click += h;
        return b;
    }

    private static int ParsePort(string value, int fallback)
        => int.TryParse(value, out int port) && port >= 0 && port <= 65535 ? port : fallback;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_isClosingAfterCancel && _downloadTask is { IsCompleted: false })
        {
            e.Cancel = true;
            _downloadCts?.Cancel();
            Log("Uygulama kapatilirken indirme iptal ediliyor...");
            _ = CloseAfterCancellationAsync();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        if (_node != null)
        {
            await _node.DisposeAsync();
            _node = null;
        }
    }

    private async Task CloseAfterCancellationAsync()
    {
        try
        {
            if (_downloadTask != null)
            {
                await _downloadTask;
            }
        }
        catch
        {
        }

        _isClosingAfterCancel = true;
        if (IsHandleCreated)
        {
            BeginInvoke(new Action(Close));
        }
    }
}
