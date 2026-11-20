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
        AudioEngine.PrepareDefaults();
        Application.Run(new GameForm());
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
    private readonly System.Windows.Forms.Timer clock = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 16 };
    private int remaining;
    private int redJumps, blueJumps;
    private float redJump, blueJump;
    private bool running;
    private Image? redClown, blueClown, redBackground, blueBackground;

    public GameForm()
    {
        Text = "JUEGO DE SALTOS - 2 JUGADORES";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(900, 540);
        BackColor = Color.Black;
        DoubleBuffered = true;
        KeyPreview = true;
        remaining = GameSettings.Current.DurationSeconds;
        LoadImages();
        clock.Tick += (_, _) => { if (running && remaining > 0) { remaining--; Invalidate(); } if (remaining <= 0) Finish(); };
        animation.Tick += (_, _) => { redJump = redJump >= 1f ? 0f : Math.Min(1f, redJump + 0.035f); blueJump = blueJump >= 1f ? 0f : Math.Min(1f, blueJump + 0.035f); Invalidate(); };
        animation.Start();
        KeyDown += HandleKey;
        Shown += (_, _) => StartRound();
    }

    private void LoadImages()
    {
        redClown = LoadImage(GameSettings.Current.Player1Image);
        blueClown = LoadImage(GameSettings.Current.Player2Image);
        redBackground = LoadImage(GameSettings.Current.Player1Background);
        blueBackground = LoadImage(GameSettings.Current.Player2Background);
    }

    private static Image? LoadImage(string path)
    {
        try { return File.Exists(path) ? Image.FromFile(path) : null; } catch { return null; }
    }

    private void HandleKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.M) { e.Handled = true; OpenSettings(); return; }
        if (!running) return;
        if (Enum.TryParse<Keys>(GameSettings.Current.Player1Key, true, out var redKey) && e.KeyCode == redKey)
        {
            redJumps++; redJump = 0.001f; AudioEngine.PlayJump(); Invalidate(); e.Handled = true;
        }
        if (Enum.TryParse<Keys>(GameSettings.Current.Player2Key, true, out var blueKey) && e.KeyCode == blueKey)
        {
            blueJumps++; blueJump = 0.001f; AudioEngine.PlayJump(); Invalidate(); e.Handled = true;
        }
    }

    private void StartRound()
    {
        redJumps = blueJumps = 0;
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
        if (wasRunning && remaining > 0) { running = true; clock.Start(); }
        Invalidate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        clock.Dispose(); animation.Dispose();
        AudioEngine.StopMusic();
        redClown?.Dispose(); blueClown?.Dispose(); redBackground?.Dispose(); blueBackground?.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int half = ClientSize.Width / 2;
        DrawPlayer(g, new Rectangle(0, 0, half, ClientSize.Height), true);
        DrawPlayer(g, new Rectangle(half, 0, ClientSize.Width - half, ClientSize.Height), false);
        using var divider = new Pen(Color.White, 5);
        g.DrawLine(divider, half, 0, half, ClientSize.Height);
        using var top = new SolidBrush(Color.FromArgb(225, 15, 15, 25));
        g.FillRectangle(top, 0, 0, ClientSize.Width, 92);
        DrawCentered(g, $"TIEMPO  {remaining / 60:00}:{remaining % 60:00}", new Font("Arial", 30, FontStyle.Bold), Color.White,
            new Rectangle(0, 15, ClientSize.Width, 60));
        if (!running)
        {
            string result = remaining <= 0 ? (redJumps == blueJumps ? "¡EMPATE!" : redJumps > blueJumps ? "¡GANA EL EQUIPO ROJO!" : "¡GANA EL EQUIPO AZUL!") : "PRESIONA M PARA CONFIGURAR";
            using var shade = new SolidBrush(Color.FromArgb(175, 0, 0, 0));
            g.FillRectangle(shade, ClientRectangle);
            DrawCentered(g, result, new Font("Arial", 34, FontStyle.Bold), Color.Gold, ClientRectangle);
            DrawCentered(g, "M = MENÚ DE CONFIGURACIÓN     |     ENTER = NUEVA PARTIDA",
                new Font("Arial", 15, FontStyle.Bold), Color.White, new Rectangle(0, ClientSize.Height - 75, ClientSize.Width, 45));
        }
        else
        {
            DrawCentered(g, "M = MENÚ", new Font("Arial", 11, FontStyle.Bold), Color.White,
                new Rectangle(0, ClientSize.Height - 32, ClientSize.Width, 24));
        }
    }

    private void DrawPlayer(Graphics g, Rectangle area, bool red)
    {
        var settings = GameSettings.Current;
        var bg = red ? redBackground : blueBackground;
        var baseColor = red ? Color.FromArgb(110, 22, 30) : Color.FromArgb(18, 45, 110);
        using (var brush = new SolidBrush(baseColor)) g.FillRectangle(brush, area);
        if (bg != null) g.DrawImage(bg, area);
        string name = red ? settings.Player1Name : settings.Player2Name;
        int score = red ? redJumps : blueJumps;
        Image? clown = red ? redClown : blueClown;
        float jump = red ? redJump : blueJump;
        DrawCentered(g, name, new Font("Arial", 23, FontStyle.Bold), Color.White,
            new Rectangle(area.X, 102, area.Width, 40));
        DrawCentered(g, score.ToString("000"), new Font("Arial", 48, FontStyle.Bold), Color.Yellow,
            new Rectangle(area.X, 145, area.Width, 65));
        int h = (int)(area.Height * 0.57);
        int w = (int)(h * 0.48);
        // El personaje conserva siempre la misma coordenada horizontal; solo sube y baja en su propio sitio.\n        float jumpLift = jump > 0f && jump < 1f ? MathF.Sin(jump * MathF.PI) * Math.Min(125, area.Height * 0.20f) : 0f;\n        var target = new Rectangle(area.X + (area.Width - w) / 2, area.Y + area.Height - h - 45 - (int)jumpLift, w, h);
        if (clown != null) g.DrawImage(clown, target);
        else DrawClownPlaceholder(g, target, red ? Color.OrangeRed : Color.DodgerBlue);
        DrawCentered(g, red ? "JUGADOR 1" : "JUGADOR 2", new Font("Arial", 15, FontStyle.Bold), Color.White,
            new Rectangle(area.X, area.Bottom - 40, area.Width, 30));
    }

    private static void DrawClownPlaceholder(Graphics g, Rectangle r, Color accent)
    {
        using var skin = new SolidBrush(Color.PeachPuff);
        using var hair = new SolidBrush(Color.RoyalBlue);
        using var suit = new SolidBrush(Color.DarkOrange);
        using var nose = new SolidBrush(Color.Red);
        g.FillEllipse(hair, r.X + r.Width / 5, r.Y, r.Width * 3 / 5, r.Height / 5);
        g.FillEllipse(skin, r.X + r.Width / 4, r.Y + r.Height / 14, r.Width / 2, r.Height / 4);
        g.FillEllipse(nose, r.X + r.Width / 2 - 9, r.Y + r.Height / 8, 18, 18);
        g.FillRectangle(suit, r.X + r.Width / 6, r.Y + r.Height / 3, r.Width * 2 / 3, r.Height / 2);
        using var pen = new Pen(Color.White, 5);
        g.DrawArc(pen, r.X + r.Width / 3, r.Y + r.Height / 5, r.Width / 3, r.Height / 9, 10, 160);
        using var accentBrush = new SolidBrush(accent);
        g.FillRectangle(accentBrush, r.X + r.Width / 6, r.Y + r.Height * 4 / 5, r.Width * 2 / 3, r.Height / 7);
    }

    private static void DrawCentered(Graphics g, string text, Font font, Color color, Rectangle bounds)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, bounds, format);
        font.Dispose();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.KeyCode == Keys.Enter && !running) StartRound();
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
        AddSection(panel, "JUGADORES Y CONTROLES");
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
