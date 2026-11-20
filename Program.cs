using System.Drawing.Drawing2D;
using System.Media;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace JuegoDeSaltos;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        GameSettings.Load();
        Sprites.Load();
        AudioEngine.PrepareDefaults();
        Application.Run(new GameForm());
    }
}

internal static class Sprites
{
    public static Bitmap? Stand, Crouch, Air, Up, Head;

    public static void Load()
    {
        Stand = Read("payaso_parado.png");
        Crouch = Read("payaso_agachado.png");
        Air = Read("payaso_salto.png");
        Up = Read("payaso_brazos_arriba.png");
        Head = Read("payaso_cabeza.png");
    }

    private static Bitmap? Read(string name)
    {
        try
        {
            using var stream = typeof(Sprites).Assembly.GetManifestResourceStream(name);
            if (stream == null) return null;
            using var img = Image.FromStream(stream);
            return new Bitmap(img);
        }
        catch { return null; }
    }
}

internal sealed class GameSettings
{
    public int DurationSeconds { get; set; } = 60;
    public string Player1Name { get; set; } = "EQUIPO ROJO";
    public string Player2Name { get; set; } = "EQUIPO AZUL";
    public string Player1Key { get; set; } = "A";
    public string Player2Key { get; set; } = "L";
    public string Player1Image { get; set; } = "";
    public string Player2Image { get; set; } = "";
    public string Player1Background { get; set; } = "";
    public string Player2Background { get; set; } = "";
    public string MusicFile { get; set; } = "";
    public string JumpSoundFile { get; set; } = "";
    public bool MusicEnabled { get; set; } = true;
    public int MusicVolume { get; set; } = 55;
    public int EffectsVolume { get; set; } = 80;

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JuegoDeSaltos");
    private static readonly string FilePath = Path.Combine(Folder, "config.json");
    public static GameSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Current = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath)) ?? new();
            if (Current.Player1Key == "Z" && Current.Player2Key == "X") { Current.Player1Key = "A"; Current.Player2Key = "L"; }
        }
        catch { Current = new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        Current = this;
    }
}

internal static class AudioEngine
{
    private static SoundPlayer? jumpPlayer;
    private static string? musicAlias;
    private static readonly string AudioFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JuegoDeSaltos", "Audio");

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern int mciSendString(string command, string? returnValue, int returnLength, IntPtr callback);

    public static void PrepareDefaults()
    {
        Directory.CreateDirectory(AudioFolder);
        var jump = Path.Combine(AudioFolder, "salto-default.wav");
        var music = Path.Combine(AudioFolder, "musica-default.wav");
        if (!File.Exists(jump)) WriteTone(jump, 660, 0.13);
        if (!File.Exists(music)) WriteArcadeMusic(music);
        if (string.IsNullOrWhiteSpace(GameSettings.Current.JumpSoundFile))
            GameSettings.Current.JumpSoundFile = jump;
        if (string.IsNullOrWhiteSpace(GameSettings.Current.MusicFile))
            GameSettings.Current.MusicFile = music;
        GameSettings.Current.Save();
        LoadJump();
        if (GameSettings.Current.MusicEnabled) PlayMusic();
    }

    public static void LoadJump()
    {
        try
        {
            jumpPlayer?.Stop();
            jumpPlayer?.Dispose();
            jumpPlayer = File.Exists(GameSettings.Current.JumpSoundFile)
                ? new SoundPlayer(GameSettings.Current.JumpSoundFile) : null;
            jumpPlayer?.Load();
        }
        catch { jumpPlayer = null; }
    }

    public static void PlayJump()
    {
        try
        {
            var path = GameSettings.Current.JumpSoundFile;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            if (Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase))
            {
                jumpPlayer?.Play();
            }
            else
            {
                mciSendString("close saltofx", null, 0, IntPtr.Zero);
                mciSendString($"open \"{path}\" alias saltofx", null, 0, IntPtr.Zero);
                mciSendString($"setaudio saltofx volume to {GameSettings.Current.EffectsVolume * 10}", null, 0, IntPtr.Zero);
                mciSendString("play saltofx", null, 0, IntPtr.Zero);
            }
        }
        catch { }
    }

    public static void PlayMusic()
    {
        StopMusic();
        try
        {
            var path = GameSettings.Current.MusicFile;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            musicAlias = "juegomusic";
            mciSendString($"open \"{path}\" alias {musicAlias}", null, 0, IntPtr.Zero);
            mciSendString($"setaudio {musicAlias} volume to {GameSettings.Current.MusicVolume * 10}", null, 0, IntPtr.Zero);
            mciSendString($"play {musicAlias} repeat", null, 0, IntPtr.Zero);
        }
        catch { }
    }

    public static void StopMusic()
    {
        if (musicAlias is null) return;
        mciSendString($"stop {musicAlias}", null, 0, IntPtr.Zero);
        mciSendString($"close {musicAlias}", null, 0, IntPtr.Zero);
        musicAlias = null;
    }

    private static void WriteTone(string path, double frequency, double seconds)
    {
        const int rate = 22050;
        int count = (int)(rate * seconds);
        using var writer = new BinaryWriter(File.Create(path));
        WriteWavHeader(writer, count, rate);
        for (int i = 0; i < count; i++)
        {
            double envelope = Math.Min(1, i / 250.0) * Math.Max(0, 1 - i / (double)count);
            short sample = (short)(Math.Sin(2 * Math.PI * frequency * i / rate) * envelope * 17000);
            writer.Write(sample);
        }
    }

    private static void WriteArcadeMusic(string path)
    {
        const int rate = 22050;
        const int seconds = 12;
        int count = rate * seconds;
        double[] notes = { 523.25, 659.25, 783.99, 659.25, 587.33, 698.46, 880, 698.46,
                           523.25, 659.25, 783.99, 1046.5, 880, 783.99, 659.25, 587.33 };
        using var writer = new BinaryWriter(File.Create(path));
        WriteWavHeader(writer, count, rate);
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)rate;
            int beat = (int)(t / 0.375) % notes.Length;
            double local = t % 0.375;
            double env = Math.Min(1, local * 22) * Math.Min(1, (0.375 - local) * 9);
            double melody = Math.Sin(2 * Math.PI * notes[beat] * t);
            double bass = Math.Sin(2 * Math.PI * notes[beat] / 2 * t) * 0.35;
            double pulse = Math.Sin(2 * Math.PI * 110 * t) * (local < 0.06 ? 0.20 : 0);
            short sample = (short)(Math.Clamp((melody * 0.55 + bass + pulse) * env, -1, 1) * 10500);
            writer.Write(sample);
        }
    }

    private static void WriteWavHeader(BinaryWriter w, int samples, int rate)
    {
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + samples * 2);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate);
        w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(samples * 2);
    }
}

internal sealed class GameForm : Form
{
    // Lienzo de referencia: la maqueta de 1672 x 941. Todo se dibuja en estas unidades y se escala a la ventana.
    private const float RefW = 1672f, RefH = 941f;
    private static readonly FontFamily TitleFont = LoadFamily("Impact");
    private static readonly FontFamily BodyFont = LoadFamily("Arial");

    private readonly System.Windows.Forms.Timer clock = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    private int remaining;
    private int redJumps, blueJumps;
    private float redJump, blueJump;
    private bool running, redDown, blueDown;
    private Image? redClown, blueClown, redBackground, blueBackground;
    private Bitmap? sceneCache;
    private Size cacheSize;

    public GameForm()
    {
        Text = "JUEGO DE SALTOS - 2 JUGADORES";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(900, 540);
        BackColor = Color.Black;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        KeyPreview = true;
        remaining = GameSettings.Current.DurationSeconds;
        LoadImages();
        clock.Tick += (_, _) => { if (running && remaining > 0) { remaining--; Invalidate(); } if (remaining <= 0) Finish(); };
        animation.Tick += (_, _) => { AdvanceJump(ref redJump); AdvanceJump(ref blueJump); Invalidate(); };
        animation.Start();
        KeyDown += HandleKey;
        Shown += (_, _) => StartRound();
    }

    private static FontFamily LoadFamily(string name)
    {
        try { return new FontFamily(name); } catch { return FontFamily.GenericSansSerif; }
    }

    private static void AdvanceJump(ref float j)
    {
        if (j > 0f) { j += 0.04f; if (j >= 1f) j = 0f; }
    }

    private void LoadImages()
    {
        redClown?.Dispose(); blueClown?.Dispose(); redBackground?.Dispose(); blueBackground?.Dispose();
        redClown = LoadImage(GameSettings.Current.Player1Image);
        blueClown = LoadImage(GameSettings.Current.Player2Image);
        redBackground = LoadImage(GameSettings.Current.Player1Background);
        blueBackground = LoadImage(GameSettings.Current.Player2Background);
        InvalidateScene();
    }

    private static Image? LoadImage(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            using var img = Image.FromFile(path);
            return new Bitmap(img); // copia en memoria: no bloquea el archivo y conserva la transparencia
        }
        catch { return null; }
    }

    private void InvalidateScene()
    {
        sceneCache?.Dispose();
        sceneCache = null;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        InvalidateScene();
        Invalidate();
    }

    // ---------- Teclas ----------
    internal static Keys ParseKey(string? value, Keys fallback)
    {
        var v = (value ?? "").Trim().ToUpperInvariant();
        if (v.Length == 0) return fallback;
        if (v == "ESPACIO") return Keys.Space;
        if (v.Length == 1 && char.IsDigit(v[0])) return Keys.D0 + (v[0] - '0');
        return Enum.TryParse<Keys>(v, true, out var k) ? k : fallback;
    }

    private static string KeyLabel(Keys k)
    {
        if (k == Keys.Space) return "ESPACIO";
        if (k >= Keys.D0 && k <= Keys.D9) return ((int)k - (int)Keys.D0).ToString();
        return k.ToString().ToUpperInvariant();
    }

    private static Keys RedKey => ParseKey(GameSettings.Current.Player1Key, Keys.A);
    private static Keys BlueKey => ParseKey(GameSettings.Current.Player2Key, Keys.L);

    private void HandleKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.M) { e.Handled = true; OpenSettings(); return; }
        if (!running) return;
        if (e.KeyCode == RedKey)
        {
            e.Handled = true;
            if (!redDown) { redDown = true; redJumps++; redJump = redJump > 0f ? 0.14f : 0.001f; AudioEngine.PlayJump(); }
        }
        if (e.KeyCode == BlueKey)
        {
            e.Handled = true;
            if (!blueDown) { blueDown = true; blueJumps++; blueJump = blueJump > 0f ? 0.14f : 0.001f; AudioEngine.PlayJump(); }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode == RedKey) redDown = false;
        if (e.KeyCode == BlueKey) blueDown = false;
        if (e.KeyCode == Keys.Enter && !running) StartRound();
    }

    // ---------- Lógica de la partida ----------
    private void StartRound()
    {
        redJumps = blueJumps = 0;
        redJump = blueJump = 0f;
        redDown = blueDown = false;
        remaining = GameSettings.Current.DurationSeconds;
        running = true;
        clock.Start();
        Invalidate();
    }

    private void Finish()
    {
        if (!running) return;
        running = false;
        clock.Stop();
        Invalidate();
    }

    private void OpenSettings()
    {
        bool wasRunning = running;
        running = false;
        clock.Stop();
        using var dialog = new SettingsForm();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadImages();
            AudioEngine.LoadJump();
            AudioEngine.PlayMusic();
            remaining = GameSettings.Current.DurationSeconds;
            redJumps = blueJumps = 0;
        }
        redDown = blueDown = false;
        if (wasRunning && remaining > 0) { running = true; clock.Start(); }
        InvalidateScene();
        Invalidate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        clock.Dispose(); animation.Dispose();
        AudioEngine.StopMusic();
        redClown?.Dispose(); blueClown?.Dispose(); redBackground?.Dispose(); blueBackground?.Dispose();
        sceneCache?.Dispose();
        base.OnFormClosed(e);
    }

    // ---------- Dibujo ----------
    private static (float s, float vw, float vh) View(Size size)
    {
        float s = Math.Min(size.Width / RefW, size.Height / RefH);
        return (s, size.Width / s, size.Height / s);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width < 50 || ClientSize.Height < 50) return;
        var g = e.Graphics;
        var (s, vw, vh) = View(ClientSize);
        if (sceneCache == null || cacheSize != ClientSize) BuildScene(s, vw, vh);

        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(sceneCache!, 0, 0, ClientSize.Width, ClientSize.Height);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(s, s);
        float half = vw / 2f;
        DrawClown(g, true, new RectangleF(0, 0, half, vh), vh);
        DrawClown(g, false, new RectangleF(half, 0, vw - half, vh), vh);
        DrawPlayerPanel(g, vw, true);
        DrawPlayerPanel(g, vw, false);
        DrawTimer(g, vw);
        if (!running) DrawOverlay(g, vw, vh);
    }

    private void BuildScene(float s, float vw, float vh)
    {
        sceneCache?.Dispose();
        sceneCache = new Bitmap(ClientSize.Width, ClientSize.Height);
        cacheSize = ClientSize;
        using var g = Graphics.FromImage(sceneCache);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Black);
        g.ScaleTransform(s, s);
        float half = vw / 2f;
        var left = new RectangleF(0, 0, half, vh);
        var right = new RectangleF(half, 0, vw - half, vh);
        DrawScene(g, left, true, redBackground);
        DrawScene(g, right, false, blueBackground);
        DrawPad(g, left, vh, true);
        DrawPad(g, right, vh, false);
        using (var glow = new Pen(Color.FromArgb(70, 255, 210, 70), 18f)) g.DrawLine(glow, half, 0, half, vh - 92);
        using (var line = new Pen(Color.FromArgb(255, 235, 150), 5f)) g.DrawLine(line, half, 0, half, vh - 92);
        DrawBottomBar(g, vw, vh);
    }

    // ----- Escenarios -----
    private static void DrawScene(Graphics g, RectangleF rect, bool red, Image? custom)
    {
        g.SetClip(rect);
        if (custom != null)
        {
            float sc = Math.Max(rect.Width / custom.Width, rect.Height / custom.Height);
            float w = custom.Width * sc, h = custom.Height * sc;
            g.DrawImage(custom, rect.X + (rect.Width - w) / 2f, rect.Y + (rect.Height - h) / 2f, w, h);
        }
        else if (red) DrawCircusScene(g, rect);
        else DrawCastleScene(g, rect);
        g.ResetClip();
    }

    private static void DrawSky(Graphics g, RectangleF rect, Color top, Color bottom)
    {
        using var b = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical);
        g.FillRectangle(b, rect);
    }

    private static void DrawCloud(Graphics g, float x, float y, float sc)
    {
        using var b = new SolidBrush(Color.FromArgb(240, 255, 255, 255));
        g.FillEllipse(b, x, y + 20 * sc, 120 * sc, 50 * sc);
        g.FillEllipse(b, x + 20 * sc, y, 70 * sc, 56 * sc);
        g.FillEllipse(b, x + 60 * sc, y + 8 * sc, 70 * sc, 50 * sc);
        g.FillEllipse(b, x - 20 * sc, y + 28 * sc, 80 * sc, 40 * sc);
    }

    private static void DrawGround(Graphics g, RectangleF rect, float groundY)
    {
        using (var hill = new SolidBrush(Color.FromArgb(115, 200, 75)))
            g.FillEllipse(hill, rect.X - 120, groundY - 50, rect.Width + 240, 160);
        var area = new RectangleF(rect.X, groundY + 30, rect.Width, rect.Bottom - groundY - 30);
        using var grass = new LinearGradientBrush(area, Color.FromArgb(95, 195, 60), Color.FromArgb(40, 135, 40), LinearGradientMode.Vertical);
        g.FillRectangle(grass, area);
    }

    private static void DrawBalloon(Graphics g, float x, float y, float r, Color c)
    {
        using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 2f)) g.DrawLine(pen, x, y + r * 1.2f, x, y + r * 3f);
        using (var b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r * 1.2f, r * 2, r * 2.4f);
        using (var hl = new SolidBrush(Color.FromArgb(110, 255, 255, 255))) g.FillEllipse(hl, x - r * 0.55f, y - r * 0.9f, r * 0.5f, r * 0.8f);
    }

    private static void DrawTent(Graphics g, float cx, float baseY, float w, float h)
    {
        float roofBase = baseY - h * 0.32f;
        float apexY = baseY - h;
        const int n = 10;
        using var red = new SolidBrush(Color.FromArgb(215, 40, 45));
        using var white = new SolidBrush(Color.FromArgb(250, 245, 235));
        for (int i = 0; i < n; i++)
        {
            float x0 = cx - w / 2f + w * i / n, x1 = cx - w / 2f + w * (i + 1) / n;
            var brush = i % 2 == 0 ? red : white;
            g.FillPolygon(brush, new[] { new PointF(cx, apexY), new PointF(x0, roofBase), new PointF(x1, roofBase) });
            g.FillRectangle(brush, x0, roofBase, x1 - x0, baseY - roofBase);
        }
        using (var yellow = new SolidBrush(Color.FromArgb(255, 200, 50)))
        {
            const int k = 12;
            for (int i = 0; i < k; i++)
                g.FillEllipse(yellow, cx - w / 2f + w * i / k, roofBase - 14, w / k + 1, 34);
        }
        using (var pole = new Pen(Color.FromArgb(230, 170, 40), 6f)) g.DrawLine(pole, cx, apexY, cx, apexY - 70);
        using (var flag = new SolidBrush(Color.FromArgb(225, 40, 50)))
            g.FillPolygon(flag, new[] { new PointF(cx, apexY - 70), new PointF(cx + 60, apexY - 52), new PointF(cx, apexY - 36) });
    }

    private static void DrawCircusScene(Graphics g, RectangleF rect)
    {
        DrawSky(g, rect, Color.FromArgb(70, 150, 235), Color.FromArgb(175, 225, 250));
        DrawCloud(g, rect.X + rect.Width * 0.42f, 200, 1.2f);
        DrawCloud(g, rect.X + rect.Width * 0.06f, 250, 0.9f);
        float groundY = rect.Bottom - 330;
        DrawGround(g, rect, groundY);
        DrawTent(g, rect.X + rect.Width * 0.22f, groundY + 70, 500, 470);
        DrawTent(g, rect.X + rect.Width * 0.74f, groundY + 50, 380, 360);
        // valla
        using (var wood = new SolidBrush(Color.FromArgb(190, 120, 50)))
        {
            g.FillRectangle(wood, rect.X + rect.Width * 0.18f, groundY + 70, rect.Width * 0.6f, 12);
            g.FillRectangle(wood, rect.X + rect.Width * 0.18f, groundY + 110, rect.Width * 0.6f, 12);
            for (int i = 0; i < 9; i++) g.FillRectangle(wood, rect.X + rect.Width * (0.18f + 0.07f * i), groundY + 55, 12, 90);
        }
        // pacas de heno
        using (var hay = new SolidBrush(Color.FromArgb(225, 175, 60)))
        using (var hayLine = new Pen(Color.FromArgb(170, 120, 30), 3f))
        {
            var bale1 = new RectangleF(rect.X - 10, groundY + 20, 210, 120);
            var bale2 = new RectangleF(rect.Right - 200, groundY + 90, 190, 100);
            foreach (var bale in new[] { bale1, bale2 })
            {
                using var p = RoundRect(bale, 18);
                g.FillPath(hay, p);
                g.DrawPath(hayLine, p);
                for (int i = 1; i < 4; i++) g.DrawLine(hayLine, bale.X + 10, bale.Y + bale.Height * i / 4f, bale.Right - 10, bale.Y + bale.Height * i / 4f);
            }
        }
        // barril
        var barrel = new RectangleF(rect.X + 8, groundY + 90, 150, 200);
        using (var p = RoundRect(barrel, 40))
        using (var wood = new SolidBrush(Color.FromArgb(150, 90, 40)))
        using (var edge = new Pen(Color.FromArgb(80, 45, 20), 4f))
        {
            g.FillPath(wood, p);
            g.DrawPath(edge, p);
        }
        using (var band = new SolidBrush(Color.FromArgb(120, 125, 135)))
        {
            g.FillRectangle(band, barrel.X + 6, barrel.Y + 40, barrel.Width - 12, 14);
            g.FillRectangle(band, barrel.X + 6, barrel.Bottom - 56, barrel.Width - 12, 14);
        }
        // globos
        DrawBalloon(g, rect.X + 55, 150, 34, Color.FromArgb(230, 50, 70));
        DrawBalloon(g, rect.Right - 170, 230, 36, Color.FromArgb(235, 70, 90));
        DrawBalloon(g, rect.Right - 110, 280, 38, Color.FromArgb(50, 140, 240));
        DrawBalloon(g, rect.Right - 60, 270, 32, Color.FromArgb(250, 210, 50));
        DrawBalloon(g, rect.X + 40, groundY + 30, 26, Color.FromArgb(60, 150, 240));
    }

    private static void DrawTower(Graphics g, float cx, float baseY, float w, float h)
    {
        var body = new RectangleF(cx - w / 2f, baseY - h, w, h);
        using (var stone = new SolidBrush(Color.FromArgb(190, 185, 175)))
        using (var edge = new Pen(Color.FromArgb(110, 105, 95), 2f))
        {
            g.FillRectangle(stone, body);
            g.DrawRectangle(edge, body.X, body.Y, body.Width, body.Height);
            for (int i = 0; i < 4; i++) g.FillRectangle(stone, body.X + i * w / 4f, body.Y - 14, w / 8f, 14);
        }
        using (var roof = new SolidBrush(Color.FromArgb(40, 80, 190)))
            g.FillPolygon(roof, new[] { new PointF(cx - w * 0.62f, body.Y - 10), new PointF(cx + w * 0.62f, body.Y - 10), new PointF(cx, body.Y - w * 1.05f) });
        using (var win = new SolidBrush(Color.FromArgb(60, 55, 60)))
            g.FillRectangle(win, cx - 8, body.Y + h * 0.3f, 16, 30);
    }

    private static void DrawTree(Graphics g, float x, float baseY, float sc)
    {
        using (var trunk = new SolidBrush(Color.FromArgb(120, 80, 40)))
            g.FillRectangle(trunk, x - 10 * sc, baseY - 90 * sc, 20 * sc, 90 * sc);
        using var leaf = new SolidBrush(Color.FromArgb(50, 150, 50));
        using var leaf2 = new SolidBrush(Color.FromArgb(75, 180, 60));
        g.FillEllipse(leaf, x - 70 * sc, baseY - 200 * sc, 140 * sc, 120 * sc);
        g.FillEllipse(leaf2, x - 50 * sc, baseY - 235 * sc, 100 * sc, 100 * sc);
        g.FillEllipse(leaf2, x - 85 * sc, baseY - 150 * sc, 90 * sc, 80 * sc);
    }

    private static void DrawCastleScene(Graphics g, RectangleF rect)
    {
        DrawSky(g, rect, Color.FromArgb(80, 160, 240), Color.FromArgb(190, 232, 255));
        DrawCloud(g, rect.X + rect.Width * 0.12f, 240, 1.1f);
        DrawCloud(g, rect.X + rect.Width * 0.5f, 330, 0.8f);
        float groundY = rect.Bottom - 330;
        DrawGround(g, rect, groundY);
        // muralla
        var wall = new RectangleF(rect.X + rect.Width * 0.14f, groundY - 110, rect.Width * 0.78f, 150);
        using (var stone = new SolidBrush(Color.FromArgb(175, 172, 165)))
        using (var edge = new Pen(Color.FromArgb(110, 105, 95), 2f))
        {
            g.FillRectangle(stone, wall);
            for (int i = 0; i < 30; i++)
                g.FillRectangle(stone, wall.X + i * wall.Width / 30f, wall.Y - 12, wall.Width / 60f, 12);
            for (int row = 1; row < 5; row++) g.DrawLine(edge, wall.X, wall.Y + row * 30, wall.Right, wall.Y + row * 30);
        }
        DrawTower(g, rect.X + rect.Width * 0.28f, groundY + 40, 74, 170);
        DrawTower(g, rect.X + rect.Width * 0.62f, groundY + 40, 90, 230);
        DrawTower(g, rect.X + rect.Width * 0.84f, groundY + 40, 104, 280);
        DrawTree(g, rect.Right - 70, groundY + 120, 1.15f);
        DrawTree(g, rect.X + rect.Width * 0.18f, groundY + 70, 0.8f);
        // caja de madera
        var crate = new RectangleF(rect.X + 4, rect.Bottom - 300, 150, 150);
        using (var wood = new SolidBrush(Color.FromArgb(200, 140, 70)))
        using (var edge = new Pen(Color.FromArgb(120, 75, 30), 5f))
        {
            g.FillRectangle(wood, crate);
            g.DrawRectangle(edge, crate.X, crate.Y, crate.Width, crate.Height);
            g.DrawLine(edge, crate.X, crate.Y, crate.Right, crate.Bottom);
            g.DrawLine(edge, crate.Right, crate.Y, crate.X, crate.Bottom);
        }
    }

    private static void DrawStar(Graphics g, float cx, float cy, float ro, Color fill, Color edge)
    {
        var pts = StarPoints(cx, cy, ro);
        using var b = new SolidBrush(fill);
        using var pen = new Pen(edge, Math.Max(2f, ro * 0.08f)) { LineJoin = LineJoin.Round };
        g.FillPolygon(b, pts);
        g.DrawPolygon(pen, pts);
    }

    private static PointF[] StarPoints(float cx, float cy, float ro)
    {
        var pts = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / 5;
            float r = i % 2 == 0 ? ro : ro * 0.42f;
            pts[i] = new PointF(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
        }
        return pts;
    }

    private static void DrawPad(Graphics g, RectangleF rect, float vh, bool red)
    {
        float w = rect.Width * 0.82f;
        float x = rect.X + (rect.Width - w) / 2f;
        float top = vh - 165f;
        Color topC = red ? Color.FromArgb(235, 55, 60) : Color.FromArgb(45, 110, 235);
        Color frontC = red ? Color.FromArgb(160, 20, 30) : Color.FromArgb(20, 55, 165);
        using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            g.FillEllipse(shadow, x + 10, top + 52, w - 20, 34);
        using (var front = new SolidBrush(frontC))
        using (var p = RoundRect(new RectangleF(x, top + 30, w, 42), 14))
            g.FillPath(front, p);
        using (var surface = new SolidBrush(topC))
            g.FillPolygon(surface, new[] { new PointF(x + 45, top), new PointF(x + w - 45, top), new PointF(x + w, top + 40), new PointF(x, top + 40) });
        using (var edge = new Pen(Color.FromArgb(90, 255, 255, 255), 3f))
            g.DrawLine(edge, x + 45, top + 2, x + w - 45, top + 2);
        DrawStar(g, x + w / 2f, top + 20, 30, Color.FromArgb(255, 215, 40), Color.FromArgb(190, 130, 10));
    }

    // ----- Personajes -----
    // Fases del salto (0..1): 0-0.12 se agacha, 0.12-0.50 sube, 0.50-0.88 baja con los brazos arriba, 0.88-1 aterriza agachado.
    private static float JumpLift(float phase)
    {
        if (phase <= 0f || phase >= 1f) return 0f;
        float t = (phase - 0.12f) / 0.76f;
        return t <= 0f || t >= 1f ? 0f : MathF.Sin(t * MathF.PI) * 110f;
    }

    private static Bitmap? SpriteFrame(float phase)
    {
        if (phase <= 0f || phase >= 1f) return Sprites.Stand;
        if (phase < 0.12f || phase >= 0.88f) return Sprites.Crouch;
        return phase < 0.50f ? Sprites.Air : Sprites.Up;
    }

    private void DrawClown(Graphics g, bool red, RectangleF rect, float vh)
    {
        float jump = red ? redJump : blueJump;
        float lift = JumpLift(jump);
        float padTop = vh - 165f;
        float cx = rect.X + rect.Width / 2f;
        float by = padTop + 16f - lift;
        float k = 1f - lift / 220f;
        using (var shadow = new SolidBrush(Color.FromArgb(85, 0, 0, 0)))
            g.FillEllipse(shadow, cx - 120 * k, padTop + 8, 240 * k, 26);

        var custom = red ? redClown : blueClown;
        var prevMode = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        if (custom != null)
        {
            const float H = 500f;
            float w = H * custom.Width / custom.Height, h = H;
            float maxW = rect.Width * 0.9f;
            if (w > maxW) { h = h * maxW / w; w = maxW; }
            g.DrawImage(custom, cx - w / 2f, by - h, w, h);
        }
        else if (Sprites.Stand != null)
        {
            var spr = SpriteFrame(jump) ?? Sprites.Stand;
            const float scale = 470f / 1100f; // el sprite "parado" mide 1100 px y se dibuja a 470 unidades
            float w = spr.Width * scale, h = spr.Height * scale;
            g.DrawImage(spr, cx - w / 2f, by - h, w, h);
        }
        else DrawClownBody(g, cx, by, 500f, lift > 8f);
        g.InterpolationMode = prevMode;
    }

    private static void DrawX(Graphics g, Pen pen, float cx, float cy, float size)
    {
        g.DrawLine(pen, cx - size, cy - size, cx + size, cy + size);
        g.DrawLine(pen, cx - size, cy + size, cx + size, cy - size);
    }

    private static void DrawArm(Graphics g, float sx, float sy, float ex, float ey, float u)
    {
        using (var white = new Pen(Color.White, 9 * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(white, sx, sy, ex, ey);
        using (var chk = new Pen(Color.FromArgb(30, 30, 40), 9 * u) { DashPattern = new float[] { 1f, 1f } })
            g.DrawLine(chk, sx, sy, ex, ey);
        using (var cuff = new Pen(Color.FromArgb(150, 230, 40), 10.5f * u))
            g.DrawLine(cuff, sx + (ex - sx) * 0.74f, sy + (ey - sy) * 0.74f, sx + (ex - sx) * 0.88f, sy + (ey - sy) * 0.88f);
        using (var glove = new SolidBrush(Color.FromArgb(255, 130, 20)))
        using (var edge = new Pen(Color.FromArgb(120, 55, 10), Math.Max(1.5f, u * 0.5f)))
        {
            g.FillEllipse(glove, ex - 5.5f * u, ey - 5.5f * u, 11 * u, 11 * u);
            g.DrawEllipse(edge, ex - 5.5f * u, ey - 5.5f * u, 11 * u, 11 * u);
        }
    }

    private static void DrawClownBody(Graphics g, float cx, float by, float H, bool air)
    {
        float u = H / 100f;
        using var edge = new Pen(Color.FromArgb(40, 25, 60), Math.Max(2f, u * 0.7f)) { LineJoin = LineJoin.Round };

        // piernas
        float lx = cx - (air ? 17 : 11) * u, rx = cx + (air ? 17 : 11) * u;
        float ly = by - (air ? 17 : 7) * u, ry = by - (air ? 26 : 7) * u;
        using (var legPen = new Pen(Color.FromArgb(40, 70, 210), 15 * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(legPen, cx - 8 * u, by - 42 * u, lx, ly - 8 * u);
            g.DrawLine(legPen, cx + 8 * u, by - 42 * u, rx, ry - 8 * u);
        }
        using (var patch = new Pen(Color.FromArgb(235, 40, 50), 2.6f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            DrawX(g, patch, cx - 9 * u, by - 36 * u, 3.5f * u);
            DrawX(g, patch, cx + 9 * u, by - 36 * u, 3.5f * u);
        }
        using (var shoe = new SolidBrush(Color.FromArgb(255, 120, 20)))
        using (var sole = new SolidBrush(Color.White))
        {
            foreach (var (fx, fy) in new[] { (lx, ly), (rx, ry) })
            {
                g.FillEllipse(sole, fx - 12 * u, fy - 3 * u, 24 * u, 8 * u);
                g.FillEllipse(shoe, fx - 12 * u, fy - 7 * u, 24 * u, 11 * u);
                g.DrawEllipse(edge, fx - 12 * u, fy - 7 * u, 24 * u, 11 * u);
            }
        }

        // chaleco
        var vest = new RectangleF(cx - 17 * u, by - 72 * u, 34 * u, 36 * u);
        using (var p = RoundRect(vest, 4 * u))
        using (var vb = new LinearGradientBrush(vest, Color.FromArgb(255, 165, 40), Color.FromArgb(240, 110, 15), LinearGradientMode.Vertical))
        {
            g.FillPath(vb, p);
            g.DrawPath(edge, p);
        }
        g.DrawLine(edge, cx, vest.Y + 2 * u, cx, vest.Bottom - 2 * u);
        using (var xp = new Pen(Color.FromArgb(40, 90, 230), 3f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            DrawX(g, xp, cx - 9 * u, by - 58 * u, 4 * u);
            DrawX(g, xp, cx + 9 * u, by - 58 * u, 4 * u);
        }

        // brazos
        if (air)
        {
            DrawArm(g, cx - 17 * u, by - 68 * u, cx - 34 * u, by - 92 * u, u);
            DrawArm(g, cx + 17 * u, by - 68 * u, cx + 34 * u, by - 92 * u, u);
        }
        else
        {
            DrawArm(g, cx - 17 * u, by - 68 * u, cx - 26 * u, by - 44 * u, u);
            DrawArm(g, cx + 17 * u, by - 68 * u, cx + 26 * u, by - 44 * u, u);
        }

        DrawClownHead(g, cx, by - 82 * u, 10.5f * u);
    }

    private static void DrawClownHead(Graphics g, float cx, float cy, float r)
    {
        using var hair = new SolidBrush(Color.FromArgb(35, 70, 235));
        for (int ring = 0; ring < 2; ring++)
            for (int i = 0; i < 11; i++)
            {
                double a = Math.PI + i * Math.PI / 10.0 + ring * 0.15;
                float rad = ring == 0 ? 1.05f : 1.35f;
                float hx = cx + (float)Math.Cos(a) * r * rad;
                float hy = cy - r * 0.15f + (float)Math.Sin(a) * r * (rad + 0.1f);
                float hr = ring == 0 ? 0.42f : 0.3f;
                g.FillEllipse(hair, hx - r * hr, hy - r * hr, r * hr * 2, r * hr * 2);
            }
        var face = new RectangleF(cx - r, cy - r * 1.1f, 2 * r, 2.2f * r);
        using (var fb = new SolidBrush(Color.FromArgb(255, 238, 225)))
        using (var fp = new Pen(Color.FromArgb(90, 60, 70), Math.Max(1.5f, r * 0.06f)))
        {
            g.FillEllipse(fb, face);
            g.DrawEllipse(fp, face);
        }
        // fleco
        g.FillPie(hair, cx - r * 1.05f, cy - r * 1.5f, r * 2.1f, r * 1.3f, 180, 180);
        // cejas, ojos
        using (var brow = new Pen(Color.FromArgb(40, 60, 200), r * 0.1f))
        using (var paint = new SolidBrush(Color.FromArgb(90, 205, 95)))
        using (var eyeW = new SolidBrush(Color.White))
        using (var pupil = new SolidBrush(Color.FromArgb(30, 30, 50)))
        {
            foreach (int sx in new[] { -1, 1 })
            {
                float ex = cx + sx * 0.42f * r, ey = cy - 0.2f * r;
                g.FillEllipse(paint, ex - 0.3f * r, ey - 0.36f * r, 0.6f * r, 0.72f * r);
                g.FillEllipse(eyeW, ex - 0.18f * r, ey - 0.2f * r, 0.36f * r, 0.4f * r);
                g.FillEllipse(pupil, ex - 0.09f * r, ey - 0.09f * r, 0.18f * r, 0.18f * r);
                g.DrawArc(brow, ex - 0.32f * r, cy - 0.78f * r, 0.64f * r, 0.4f * r, 200, 140);
            }
        }
        // mejillas
        using (var cheek = new SolidBrush(Color.FromArgb(110, 255, 120, 140)))
        {
            g.FillEllipse(cheek, cx - 0.85f * r, cy + 0.15f * r, 0.4f * r, 0.4f * r);
            g.FillEllipse(cheek, cx + 0.45f * r, cy + 0.15f * r, 0.4f * r, 0.4f * r);
        }
        // boca
        var mouth = new RectangleF(cx - 0.62f * r, cy + 0.38f * r, 1.24f * r, 0.66f * r);
        using (var md = new SolidBrush(Color.FromArgb(150, 20, 35))) g.FillPie(md, mouth, 0, 180);
        using (var teeth = new SolidBrush(Color.White)) g.FillRectangle(teeth, cx - 0.42f * r, cy + 0.7f * r, 0.84f * r, 0.12f * r);
        using (var lip = new Pen(Color.FromArgb(225, 30, 50), r * 0.1f)) g.DrawArc(lip, mouth, 0, 180);
        // nariz
        using (var nose = new SolidBrush(Color.FromArgb(230, 30, 40)))
        using (var shine = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
        {
            g.FillEllipse(nose, cx - 0.25f * r, cy + 0.02f * r, 0.5f * r, 0.5f * r);
            g.FillEllipse(shine, cx - 0.12f * r, cy + 0.08f * r, 0.14f * r, 0.14f * r);
        }
    }

    // ----- Panel superior -----
    private void DrawPlayerPanel(Graphics g, float vw, bool red)
    {
        var settings = GameSettings.Current;
        float X(float x, float w) => red ? x : vw - x - w;
        Color dark = red ? Color.FromArgb(110, 10, 15) : Color.FromArgb(10, 30, 110);

        var panel = new RectangleF(X(12, 598), 12, 598, 153);
        FillRound(g, panel, 34,
            red ? Color.FromArgb(240, 45, 45) : Color.FromArgb(40, 110, 235),
            red ? Color.FromArgb(165, 15, 25) : Color.FromArgb(15, 50, 165), Color.White, 5f);

        DrawPortrait(g, new RectangleF(X(22, 150), 20, 150, 138), red ? redClown : blueClown);

        DrawOutlined(g, red ? "JUGADOR 1" : "JUGADOR 2", TitleFont, 50, Color.White, dark, 8f,
            new RectangleF(X(180, 240), 28, 240, 60));
        string name = (red ? settings.Player1Name : settings.Player2Name).ToUpperInvariant();
        float ns = FitSize(g, name, TitleFont, 36f, 232f);
        DrawOutlined(g, name, TitleFont, ns, Color.FromArgb(255, 235, 40), dark, 6f,
            new RectangleF(X(180, 240), 92, 240, 52));

        var box = new RectangleF(X(428, 170), 18, 170, 141);
        FillRound(g, box, 26,
            red ? Color.FromArgb(135, 18, 24) : Color.FromArgb(15, 45, 130),
            red ? Color.FromArgb(70, 6, 12) : Color.FromArgb(6, 20, 75),
            red ? Color.FromArgb(255, 120, 120) : Color.FromArgb(110, 170, 255), 3f);
        DrawOutlined(g, (red ? redJumps : blueJumps).ToString("00"), TitleFont, 86, Color.FromArgb(255, 235, 40),
            Color.FromArgb(120, 70, 0), 8f, new RectangleF(box.X, 20, 170, 96));
        DrawOutlined(g, "SALTOS", TitleFont, 32, Color.White, dark, 5f, new RectangleF(box.X, 112, 170, 44));
    }

    private static void DrawPortrait(Graphics g, RectangleF rect, Image? img)
    {
        using var path = RoundRect(rect, 20);
        using (var bg = new LinearGradientBrush(rect, Color.FromArgb(120, 190, 255), Color.FromArgb(255, 230, 150), LinearGradientMode.Vertical))
            g.FillPath(bg, path);
        var state = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        if (img != null)
        {
            float w = rect.Width * 1.15f, h = w * img.Height / img.Width;
            g.DrawImage(img, rect.X - (w - rect.Width) / 2f, rect.Y - 2, w, h);
        }
        else if (Sprites.Head != null)
        {
            var head = Sprites.Head;
            float sc = Math.Max(rect.Width / head.Width, rect.Height / head.Height);
            float w = head.Width * sc, h = head.Height * sc;
            g.DrawImage(head, rect.X + (rect.Width - w) / 2f, rect.Y + (rect.Height - h) / 2f + rect.Height * 0.04f, w, h);
        }
        else DrawClownHead(g, rect.X + rect.Width / 2f, rect.Y + rect.Height * 0.52f, rect.Width * 0.30f);
        g.Restore(state);
        using var border = new Pen(Color.FromArgb(255, 205, 70), 4f) { LineJoin = LineJoin.Round };
        g.DrawPath(border, path);
    }

    private void DrawTimer(Graphics g, float vw)
    {
        const float w = 410f;
        float cx = (vw - w) / 2f;
        var outer = new RectangleF(cx, 10, w, 168);
        FillRound(g, outer, 30, Color.FromArgb(255, 220, 100), Color.FromArgb(205, 135, 20), Color.FromArgb(120, 70, 10), 3f);
        FillRound(g, new RectangleF(cx + 16, 26, w - 32, 136), 18, Color.FromArgb(10, 10, 25), Color.Black, Color.FromArgb(70, 45, 10), 3f);

        bool phase = (Environment.TickCount / 350) % 2 == 0;
        int idx = 0;
        for (float x = cx + 22; x <= cx + w - 22; x += 24) DrawBulb(g, x, 170, (idx++ + (phase ? 0 : 1)) % 2 == 0);
        for (float y = 58; y <= 160; y += 24)
        {
            bool on = (idx++ + (phase ? 0 : 1)) % 2 == 0;
            DrawBulb(g, cx + 8, y, on);
            DrawBulb(g, cx + w - 8, y, on);
        }

        FillRound(g, new RectangleF(cx + 30, 12, w - 60, 62), 22, Color.FromArgb(45, 100, 215), Color.FromArgb(15, 45, 130), Color.White, 4f);
        DrawOutlined(g, "TIEMPO RESTANTE", TitleFont, 38, Color.White, Color.FromArgb(10, 30, 100), 6f,
            new RectangleF(cx + 30, 12, w - 60, 62));

        bool urgent = running && remaining <= 10 && remaining > 0;
        Color digits = urgent && phase ? Color.FromArgb(255, 90, 60) : Color.FromArgb(255, 235, 40);
        DrawOutlined(g, $"{remaining / 60:00}:{remaining % 60:00}", TitleFont, 92, digits, Color.FromArgb(130, 70, 0), 8f,
            new RectangleF(cx + 16, 74, w - 32, 90));
    }

    private static void DrawBulb(Graphics g, float x, float y, bool on)
    {
        if (on)
        {
            using var glow = new SolidBrush(Color.FromArgb(90, 255, 240, 120));
            g.FillEllipse(glow, x - 10, y - 10, 20, 20);
        }
        using var b = new SolidBrush(on ? Color.FromArgb(255, 255, 215) : Color.FromArgb(235, 175, 50));
        g.FillEllipse(b, x - 6, y - 6, 12, 12);
    }

    // ----- Barra inferior -----
    private void DrawBottomBar(Graphics g, float vw, float vh)
    {
        var bar = new RectangleF(0, vh - 92, vw, 92);
        using (var b = new LinearGradientBrush(bar, Color.FromArgb(40, 30, 80), Color.FromArgb(12, 10, 35), LinearGradientMode.Vertical))
            g.FillRectangle(b, bar);
        using (var gold = new Pen(Color.FromArgb(255, 200, 60), 3f)) g.DrawLine(gold, 0, bar.Y, vw, bar.Y);

        Color gold1 = Color.FromArgb(255, 215, 40), gold2 = Color.FromArgb(190, 130, 10);

        var lb = new RectangleF(10, vh - 84, 360, 74);
        FillRound(g, lb, 22, Color.FromArgb(240, 45, 45), Color.FromArgb(165, 15, 25), Color.FromArgb(255, 200, 60), 3f);
        DrawStar(g, lb.X + 44, lb.Y + 37, 28, gold1, gold2);
        DrawOutlined(g, "¡SALTA Y GANA!", TitleFont, 40, Color.White, Color.FromArgb(110, 10, 15), 7f,
            new RectangleF(lb.X + 80, lb.Y, lb.Width - 90, lb.Height));

        var rb = new RectangleF(vw - 370, vh - 84, 360, 74);
        FillRound(g, rb, 22, Color.FromArgb(45, 110, 235), Color.FromArgb(15, 50, 165), Color.FromArgb(255, 200, 60), 3f);
        DrawStar(g, rb.X + 34, rb.Y + 37, 22, gold1, gold2);
        DrawStar(g, rb.Right - 34, rb.Y + 37, 22, gold1, gold2);
        DrawOutlined(g, "¡QUIÉN HACE\nMÁS SALTOS?", TitleFont, 28, Color.White, Color.FromArgb(10, 30, 110), 6f,
            new RectangleF(rb.X + 64, rb.Y, rb.Width - 128, rb.Height));

        // cápsula con las teclas
        var segs = new List<(string Text, bool IsKey, Color Col)>
        {
            ("Presiona tu tecla para saltar:   ", false, Color.White),
            ("ROJO ", false, Color.FromArgb(255, 120, 120)),
            (KeyLabel(RedKey), true, Color.FromArgb(255, 235, 40)),
            ("   AZUL ", false, Color.FromArgb(130, 185, 255)),
            (KeyLabel(BlueKey), true, Color.FromArgb(255, 235, 40)),
        };
        using var fmt = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap
        };
        float avail = vw - 2 * 390f;
        float size = 26f, total = 0f;
        foreach (float candidate in new[] { 26f, 22f, 18f })
        {
            size = candidate;
            total = 0f;
            using var f = new Font(BodyFont, size, FontStyle.Bold, GraphicsUnit.Pixel);
            foreach (var seg in segs)
                total += g.MeasureString(seg.Text, f, PointF.Empty, fmt).Width + (seg.IsKey ? 34f : 0f);
            if (total + 60f <= avail) break;
        }
        float pillW = total + 60f, pillY = vh - 80f;
        var pill = new RectangleF((vw - pillW) / 2f, pillY, pillW, 66f);
        FillRound(g, pill, 33, Color.FromArgb(135, 55, 190), Color.FromArgb(70, 20, 120), Color.FromArgb(215, 135, 255), 3f);
        using var font = new Font(BodyFont, size, FontStyle.Bold, GraphicsUnit.Pixel);
        float px = pill.X + 30f;
        foreach (var seg in segs)
        {
            float tw = g.MeasureString(seg.Text, font, PointF.Empty, fmt).Width;
            using var brush = new SolidBrush(seg.Col);
            if (seg.IsKey)
            {
                var keyBox = new RectangleF(px, pillY + 11, tw + 34f, 44);
                FillRound(g, keyBox, 14, Color.FromArgb(55, 18, 95), Color.FromArgb(35, 8, 65), Color.FromArgb(255, 205, 70), 2f);
                using var center = new StringFormat(fmt) { Alignment = StringAlignment.Center };
                g.DrawString(seg.Text, font, brush, keyBox, center);
                px += tw + 34f;
            }
            else
            {
                g.DrawString(seg.Text, font, brush, new RectangleF(px, pillY, tw + 4f, 66f), fmt);
                px += tw;
            }
        }
    }

    // ----- Resultado -----
    private void DrawOverlay(Graphics g, float vw, float vh)
    {
        using (var shade = new SolidBrush(Color.FromArgb(175, 0, 0, 0))) g.FillRectangle(shade, 0, 0, vw, vh);
        var panel = new RectangleF(vw / 2f - 460, vh / 2f - 170, 920, 340);
        FillRound(g, panel, 40, Color.FromArgb(75, 25, 130), Color.FromArgb(28, 10, 65), Color.FromArgb(255, 205, 70), 6f);

        var s = GameSettings.Current;
        bool finished = remaining <= 0;
        string title = !finished ? "PRESIONA M PARA CONFIGURAR"
            : redJumps == blueJumps ? "¡EMPATE!"
            : $"¡GANA {(redJumps > blueJumps ? s.Player1Name : s.Player2Name).ToUpperInvariant()}!";
        float ts = FitSize(g, title, TitleFont, 84f, 860f);
        DrawOutlined(g, title, TitleFont, ts, Color.FromArgb(255, 215, 40), Color.FromArgb(110, 60, 0), 8f,
            new RectangleF(panel.X, panel.Y + 30, panel.Width, 130));
        if (finished)
            DrawOutlined(g, $"{redJumps:00}  -  {blueJumps:00}", TitleFont, 64, Color.White, Color.FromArgb(40, 15, 80), 7f,
                new RectangleF(panel.X, panel.Y + 160, panel.Width, 90));
        DrawOutlined(g, "ENTER = NUEVA PARTIDA     |     M = CONFIGURACIÓN", BodyFont, 26, Color.White, Color.FromArgb(40, 15, 80), 4f,
            new RectangleF(panel.X, panel.Bottom - 70, panel.Width, 50), StringAlignment.Center, StringAlignment.Center, FontStyle.Bold);
    }

    // ----- Utilidades de dibujo -----
    private static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        float d = rad * 2f;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static void FillRound(Graphics g, RectangleF r, float rad, Color c1, Color c2, Color border, float borderWidth)
    {
        using var path = RoundRect(r, rad);
        using (var b = new LinearGradientBrush(r, c1, c2, LinearGradientMode.Vertical)) g.FillPath(b, path);
        if (borderWidth > 0f)
        {
            using var pen = new Pen(border, borderWidth) { LineJoin = LineJoin.Round };
            g.DrawPath(pen, path);
        }
    }

    private static void DrawOutlined(Graphics g, string text, FontFamily family, float size, Color fill, Color outline,
        float outlineWidth, RectangleF bounds, StringAlignment h = StringAlignment.Center,
        StringAlignment v = StringAlignment.Center, FontStyle style = FontStyle.Regular)
    {
        if (!family.IsStyleAvailable(style)) style = FontStyle.Regular;
        using var path = new GraphicsPath();
        // OJO: GenericTypographic incluye LineLimit y no dibuja nada si la línea es más alta que la caja.
        using var fmt = new StringFormat(StringFormat.GenericTypographic) { Alignment = h, LineAlignment = v };
        fmt.FormatFlags = StringFormatFlags.NoClip;
        path.AddString(text, family, (int)style, size, bounds, fmt);
        using var pen = new Pen(outline, outlineWidth) { LineJoin = LineJoin.Round };
        using var brush = new SolidBrush(fill);
        g.DrawPath(pen, path);
        g.FillPath(brush, path);
    }

    private static float FitSize(Graphics g, string text, FontFamily family, float size, float maxWidth)
    {
        while (size > 14f)
        {
            using var f = new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel);
            if (g.MeasureString(text, f, PointF.Empty, StringFormat.GenericTypographic).Width <= maxWidth) break;
            size -= 2f;
        }
        return size;
    }
}

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown duration = new() { Minimum = 5, Maximum = 600, Width = 110 };
    private readonly TextBox redName = new() { Width = 180 };
    private readonly TextBox blueName = new() { Width = 180 };
    private readonly TextBox redKey = new() { Width = 100 };
    private readonly TextBox blueKey = new() { Width = 100 };
    private readonly TextBox redImage = new() { Width = 300 };
    private readonly TextBox blueImage = new() { Width = 300 };
    private readonly TextBox redBg = new() { Width = 300 };
    private readonly TextBox blueBg = new() { Width = 300 };
    private readonly TextBox music = new() { Width = 300 };
    private readonly TextBox effect = new() { Width = 300 };
    private readonly CheckBox musicEnabled = new() { Text = "Reproducir música de fondo", AutoSize = true };
    private readonly TrackBar musicVolume = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, Width = 220 };
    private readonly TrackBar effectVolume = new() { Minimum = 0, Maximum = 100, TickFrequency = 10, Width = 220 };

    public SettingsForm()
    {
        Text = "MENÚ M - CONFIGURACIÓN DEL JUEGO";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 760);
        MinimumSize = new Size(700, 650);
        BackColor = Color.FromArgb(28, 30, 40);
        ForeColor = Color.White;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(18), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        Controls.Add(panel);
        var s = GameSettings.Current;
        AddSection(panel, "PARTIDA");
        AddRow(panel, "Duración (segundos)", duration); duration.Value = Math.Clamp(s.DurationSeconds, 5, 600);
        AddSection(panel, "JUGADORES Y CONTROLES  (para cambiar una tecla, haz clic en su casilla y púlsala)");
        foreach (var tb in new[] { redKey, blueKey }) { tb.ReadOnly = true; tb.KeyDown += CaptureKey; }
        AddRow(panel, "Nombre equipo rojo", redName); redName.Text = s.Player1Name;
        AddRow(panel, "Tecla de salto rojo", redKey); redKey.Text = s.Player1Key;
        AddRow(panel, "Personaje rojo (PNG/JPG)", redImage, "Buscar", () => PickImage(redImage)); redImage.Text = s.Player1Image;
        AddRow(panel, "Escenario rojo (PNG/JPG)", redBg, "Buscar", () => PickImage(redBg)); redBg.Text = s.Player1Background;
        AddRow(panel, "Nombre equipo azul", blueName); blueName.Text = s.Player2Name;
        AddRow(panel, "Tecla de salto azul", blueKey); blueKey.Text = s.Player2Key;
        AddRow(panel, "Personaje azul (PNG/JPG)", blueImage, "Buscar", () => PickImage(blueImage)); blueImage.Text = s.Player2Image;
        AddRow(panel, "Escenario azul (PNG/JPG)", blueBg, "Buscar", () => PickImage(blueBg)); blueBg.Text = s.Player2Background;
        AddSection(panel, "MÚSICA Y EFECTOS");
        AddRow(panel, "Archivo de música (MP3/WAV)", music, "Buscar", () => PickAudio(music)); music.Text = s.MusicFile;
        AddRow(panel, "Sonido de salto (WAV/MP3)", effect, "Buscar", () => PickAudio(effect)); effect.Text = s.JumpSoundFile;
        musicEnabled.Checked = s.MusicEnabled; panel.Controls.Add(musicEnabled);
        AddRow(panel, "Volumen música", musicVolume); musicVolume.Value = s.MusicVolume;
        AddRow(panel, "Volumen efectos", effectVolume); effectVolume.Value = s.EffectsVolume;
        var buttons = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(0, 18, 0, 12) };
        var save = new Button { Text = "GUARDAR", BackColor = Color.ForestGreen, ForeColor = Color.White, Width = 140, Height = 42 };
        var cancel = new Button { Text = "CANCELAR", Width = 120, Height = 42 };
        save.Click += (_, _) => SaveSettings();
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(save); buttons.Controls.Add(cancel); panel.Controls.Add(buttons);
    }

    private static void AddSection(FlowLayoutPanel panel, string text)
    {
        panel.Controls.Add(new Label { Text = text, Font = new Font("Arial", 12, FontStyle.Bold), ForeColor = Color.Gold, AutoSize = true, Margin = new Padding(0, 16, 0, 5) });
    }

    private static void AddRow(FlowLayoutPanel panel, string label, Control input, string? buttonText = null, Action? action = null)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Width = 680, Margin = new Padding(0, 4, 0, 4) };
        row.Controls.Add(new Label { Text = label, Width = 205, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.White, Height = 30 });
        row.Controls.Add(input);
        if (buttonText != null)
        {
            var b = new Button { Text = buttonText, Width = 80 };
            b.Click += (_, _) => action?.Invoke();
            row.Controls.Add(b);
        }
        panel.Controls.Add(row);
    }

    private static void CaptureKey(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (e.KeyCode is Keys.Tab or Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.Escape or Keys.M or Keys.Enter) return;
        tb.Text = e.KeyCode.ToString();
        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    private static void PickImage(TextBox target)
    {
        using var d = new OpenFileDialog { Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Todos|*.*" };
        if (d.ShowDialog() == DialogResult.OK) target.Text = d.FileName;
    }

    private static void PickAudio(TextBox target)
    {
        using var d = new OpenFileDialog { Filter = "Audio|*.mp3;*.wav|Todos|*.*" };
        if (d.ShowDialog() == DialogResult.OK) target.Text = d.FileName;
    }

    private void SaveSettings()
    {
        if (GameForm.ParseKey(redKey.Text, Keys.A) == GameForm.ParseKey(blueKey.Text, Keys.L))
        {
            MessageBox.Show(this, "Los dos jugadores no pueden usar la misma tecla.", "Teclas repetidas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var s = GameSettings.Current;
        s.DurationSeconds = (int)duration.Value;
        s.Player1Name = string.IsNullOrWhiteSpace(redName.Text) ? "EQUIPO ROJO" : redName.Text.Trim();
        s.Player2Name = string.IsNullOrWhiteSpace(blueName.Text) ? "EQUIPO AZUL" : blueName.Text.Trim();
        s.Player1Key = NormalizeKey(redKey.Text, "A");
        s.Player2Key = NormalizeKey(blueKey.Text, "L");
        s.Player1Image = redImage.Text; s.Player2Image = blueImage.Text;
        s.Player1Background = redBg.Text; s.Player2Background = blueBg.Text;
        s.MusicFile = music.Text; s.JumpSoundFile = effect.Text;
        s.MusicEnabled = musicEnabled.Checked;
        s.MusicVolume = musicVolume.Value; s.EffectsVolume = effectVolume.Value;
        s.Save();
        if (s.MusicEnabled) AudioEngine.PlayMusic(); else AudioEngine.StopMusic();
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string NormalizeKey(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return value.Trim().ToUpperInvariant();
    }
}
