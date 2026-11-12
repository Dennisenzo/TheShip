using System.Globalization;
using System.Text;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheShip.AI;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// The prototype's window: text only. A status column on the left, the transcript on the right, one input line.
/// A line starting with "/" is a control (wake, repair, logs, the clock); anything else is said aloud to the awake crew.
/// </summary>
public sealed partial class ShipGame : Microsoft.Xna.Framework.Game
{
    private static readonly Color Back = new(10, 14, 18);
    private static readonly Color Panel = new(18, 24, 30);
    private static readonly Color Dim = new(110, 124, 134);
    private static readonly Color Plain = new(200, 208, 212);
    private static readonly Color Amber = new(236, 178, 76);
    private static readonly Color Green = new(132, 214, 150);
    private static readonly Color Cyan = new(140, 208, 232);
    private static readonly Color Red = new(232, 110, 96);

    private readonly GraphicsDeviceManager _graphics;
    private readonly Options _options;
    private readonly MainThreadQueue _main = new();
    private readonly Queue<char> _typed = new();
    private readonly StringBuilder _input = new();
    private readonly List<string> _history = new();
    private int _historyAt;
    private int _scroll;
    private KeyboardState _prevKeys;
    private int _prevWheel;

    private SpriteBatch _batch = null!;
    private Texture2D _pixel = null!;
    private FontSystem _fonts = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _small = null!;
    private float _charWidth;
    private float _smallCharWidth;

    private Task<ILlmProvider>? _loading;
    private string _loadingText = "";
    private Voyage? _voyage;
    private ShipMind? _mind;
    private CrewMember? _target;
    private bool _endingShown;
    /// <summary>No model could be loaded: the window only shows why.</summary>
    private bool _fatal;

    public ShipGame(Options options)
    {
        _options = options;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1600,
            PreferredBackBufferHeight = 900,
            SynchronizeWithVerticalRetrace = true
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "The Ship";
        Window.TextInput += (_, e) => _typed.Enqueue(e.Character);
    }

    public static string ModelDirectory => Path.Combine(AppContext.BaseDirectory, "Content", "Models", "llm");

    /// <summary>A path, a file name in Content/Models/llm, or null for the default model (as in Cities' GameSession).</summary>
    public static string? ResolveModel(string? requested)
    {
        if (requested is not null)
        {
            if (File.Exists(requested))
                return Path.GetFullPath(requested);
            var inDir = Path.Combine(ModelDirectory, requested);
            return File.Exists(inDir) ? inDir : null;
        }
        if (!Directory.Exists(ModelDirectory))
            return null;
        var preferred = Path.Combine(ModelDirectory, "gemma-4-E4B-it-Q4_K_M.gguf");
        return File.Exists(preferred) ? preferred : Directory.GetFiles(ModelDirectory, "*.gguf").OrderBy(f => f).FirstOrDefault();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _fonts = new FontSystem();
        _fonts.AddFont(LoadFont());
        _font = _fonts.GetFont(19);
        _small = _fonts.GetFont(16);
        _charWidth = Math.Max(1, _font.MeasureString("M").X);
        _smallCharWidth = Math.Max(1, _small.MeasureString("M").X);

        var path = ResolveModel(_options.Llm);
        if (path is null)
        {
            // The crew's words and the reading of yours come from the model: without one there is no game.
            _loadingText = (_options.Llm is null
                               ? "No GGUF model found in " + ModelDirectory + "."
                               : "The model '" + _options.Llm + "' was not found (looked for the path and in " + ModelDirectory + ").")
                           + "\nThe Ship needs a language model to run. The README.txt in that folder lists the models and where to get them."
                           + "\nPut a .gguf file there, or start with --llm <file.gguf>, and run the game again. Esc closes.";
            _fatal = true;
            return;
        }
        _loadingText = "Loading " + Path.GetFileName(path) + " ...";
        var layers = _options.GpuLayers;
        _loading = Task.Run<ILlmProvider>(() => new LocalLlm(path, gpuLayerCount: layers));
    }

    /// <summary>Bundled font, falling back to a system font if Content/Fonts is missing.</summary>
    private static byte[] LoadFont()
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "ShareTechMono-Regular.ttf");
        if (File.Exists(bundled))
            return File.ReadAllBytes(bundled);
        string[] fallbacks =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf"),
            "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
        };
        var fallback = fallbacks.FirstOrDefault(File.Exists)
                       ?? throw new FileNotFoundException("Font " + bundled + " is missing and no system fallback was found.");
        return File.ReadAllBytes(fallback);
    }

    private void Start(ILlmProvider llm, string note)
    {
        _voyage = new Voyage(_options.Seed);
        _mind = new ShipMind(_voyage, llm, _main);
        Note(note);
        Note("Type to speak to whoever is awake. /help lists the controls. Start with /wake <name>, then /go.");
    }

    private void Note(string text) => _voyage?.Log.Add(new LogEntry(LogKind.Help, "", text));

    protected override void Update(GameTime gameTime)
    {
        if (_loading is { IsCompleted: true } done)
        {
            _loading = null;
            if (done.IsCompletedSuccessfully)
            {
                Start(done.Result, "Model: " + done.Result.Name + " on " + done.Result.Backend + ".");
            }
            else
            {
                _loadingText = "The model failed to load: " + (done.Exception?.GetBaseException().Message ?? "unknown error")
                               + "\nThe Ship needs a language model to run. Try another file with --llm <file.gguf>, or --gpu-layers 0 for CPU only. Esc closes.";
                _fatal = true;
            }
        }
        _main.Drain();

        var keys = Keyboard.GetState();
        bool Pressed(Keys k) => keys.IsKeyDown(k) && !_prevKeys.IsKeyDown(k);
        var wheel = Mouse.GetState().ScrollWheelValue;
        _scroll += (wheel - _prevWheel) / 40;
        _prevWheel = wheel;
        if (Pressed(Keys.PageUp))
            _scroll += 12;
        if (Pressed(Keys.PageDown))
            _scroll -= 12;
        _scroll = Math.Max(0, _scroll);
        if (Pressed(Keys.Up) && _history.Count > 0)
        {
            _historyAt = Math.Max(0, _historyAt - 1);
            _input.Clear().Append(_history[_historyAt]);
        }
        if (Pressed(Keys.Down) && _history.Count > 0)
        {
            _historyAt = Math.Min(_history.Count, _historyAt + 1);
            _input.Clear();
            if (_historyAt < _history.Count)
                _input.Append(_history[_historyAt]);
        }
        if (Pressed(Keys.Escape))
        {
            if (_fatal)
                Exit();
            _input.Clear();
        }
        _prevKeys = keys;

        while (_typed.Count > 0)
        {
            var ch = _typed.Dequeue();
            if (ch == '\b')
            {
                if (_input.Length > 0)
                    _input.Length--;
            }
            else if (ch == '\r' || ch == '\n')
                Submit();
            else if (!char.IsControl(ch) && _input.Length < 400)
                _input.Append(ch);
        }

        if (_voyage is { Phase: Phase.Ended, Ending: { } ending } && !_endingShown)
        {
            _endingShown = true;
            ShowEnding(ending);
        }
        base.Update(gameTime);
    }

    private void Submit()
    {
        var text = _input.ToString().Trim();
        _input.Clear();
        if (text.Length == 0 || _voyage is null || _mind is null)
            return;
        _history.Add(text);
        _historyAt = _history.Count;
        _scroll = 0;
        if (text.StartsWith('/'))
        {
            Command(text);
            return;
        }
        if (_voyage.Phase == Phase.Ended)
            return;
        if (_mind.IsReading)
        {
            Note("Still reading your last line.");
            return;
        }
        var hearers = _target is { State: CrewState.Awake } one ? new List<CrewMember> { one } : _voyage.Awake.ToList();
        _ = _mind.Say(text, hearers);
    }

    private void ShowEnding(Ending ending)
    {
        Note("");
        Note("=== " + ending.Title.ToUpperInvariant() + " ===");
        Note(ending.Summary);
        Note("");
        Note("Who is left:");
        foreach (var line in ending.Survivors)
            Note("  " + line);
        Note("");
        Note("What they believe about you:");
        foreach (var line in ending.Beliefs)
            Note("  " + line);
        Note("");
        Note("The record of what you said:");
        if (ending.Record.Count == 0)
            Note("  You said nothing that was recorded.");
        foreach (var line in ending.Record)
            Note("  " + line);
        Note("");
        Note("PageUp and the mouse wheel scroll. /quit closes the game.");
    }

    protected override void UnloadContent()
    {
        _mind?.Dispose();
        _fonts?.Dispose();
        base.UnloadContent();
    }

    // ---- Drawing ----

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Back);
        var width = GraphicsDevice.Viewport.Width;
        var height = GraphicsDevice.Viewport.Height;
        _batch.Begin();
        if (_voyage is null)
        {
            var row = 0;
            foreach (var line in Wrap(_loadingText, (int)((width - 80) / _charWidth)))
                Text(_font, line, 40, 40 + 26 * row++, _fatal ? Red : Amber);
        }
        else
        {
            const int left = 520;
            _batch.Draw(_pixel, new Rectangle(0, 0, left, height), Panel);
            DrawStatus(_voyage, 16, 14, left - 32);
            DrawTranscript(_voyage, left + 20, 14, width - left - 40, height - 60);
            DrawInput(left + 20, height - 38, width - left - 40);
        }
        _batch.End();
        base.Draw(gameTime);
    }

    private void Text(SpriteFontBase font, string text, float x, float y, Color color) =>
        font.DrawText(_batch, Printable(text), new Vector2(MathF.Round(x), MathF.Round(y)), color);

    /// <summary>The model may write characters the bundled font lacks: map the common ones, replace the rest.</summary>
    private static string Printable(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                '‘' or '’' => "'",
                '“' or '”' => "\"",
                '–' or '—' => "-",
                '…' => "...",
                '\t' => " ",
                _ => ch is >= ' ' and <= 'ÿ' ? ch.ToString() : "?",
            });
        }
        return sb.ToString();
    }

    /// <summary>Word wrap for the monospaced font: by character count.</summary>
    private static List<string> Wrap(string text, int columns)
    {
        var lines = new List<string>();
        columns = Math.Max(8, columns);
        foreach (var paragraph in Printable(text).Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' '))
            {
                var piece = word;
                while (piece.Length > columns)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    lines.Add(piece[..columns]);
                    piece = piece[columns..];
                }
                if (line.Length > 0 && line.Length + 1 + piece.Length > columns)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                }
                if (line.Length > 0)
                    line.Append(' ');
                line.Append(piece);
            }
            lines.Add(line.ToString());
        }
        return lines;
    }

    private void DrawStatus(Voyage v, int x, int y, int width)
    {
        var columns = (int)(width / _smallCharWidth);
        const int step = 20;
        void Line(string text, Color color)
        {
            foreach (var line in Wrap(text, columns))
            {
                Text(_small, line, x, y, color);
                y += step;
            }
        }
        string N(double n) => n.ToString("0", CultureInfo.InvariantCulture);

        var phase = v.Phase switch { Phase.Alert => "ALERT", Phase.Aftermath => "AFTERMATH", _ => "ARRIVED" };
        Line("YEAR " + N(Math.Floor(v.Year)) + " OF " + Scenario.ArrivalYear + "   " + phase + "   alert " + v.Current.Number + "/" + v.Alerts.Count, Amber);
        Line("Stores " + N(v.Stores) + " person-days   Parts " + v.Parts + "   Pod wear " + v.PodWear, Plain);
        Line("Power " + (v.PowerLow ? "LOW" : "normal") + "   Course " + (v.NavLost ? "LOST" : "held") + "   Logs " + (v.LogsLocked ? "TAKEN FROM YOU" : "yours"), v.PowerLow || v.NavLost || v.LogsLocked ? Red : Plain);
        y += 8;

        Line("FAULTS: " + v.Current.Title, Amber);
        if (v.Current.Faults.Count == 0)
            Line("  none", Dim);
        foreach (var f in v.Current.Faults)
        {
            var state = f.State switch
            {
                FaultState.Open => "open, " + Voyage.Hours(Math.Max(0, f.Deadline - v.Clock)) + " left",
                FaultState.InRepair => (f.Patching ? "patching " : "in repair ") + N(100 * f.Progress / f.Hours) + "%, " + Voyage.Hours(Math.Max(0, f.Deadline - v.Clock)) + " left",
                FaultState.Repaired => "repaired",
                FaultState.Patched => "patched",
                _ => "LOST",
            };
            Line("  " + f.Name + " [" + f.Id + "]: " + state, f.State == FaultState.Lost ? Red : f.Resolved ? Green : Plain);
            if (!f.Resolved)
                Line("    needs " + f.Skill.ToString().ToLowerInvariant() + " " + f.Level + (f.People > 1 ? ", " + f.People + " people" : "") + ", " + N(f.Hours) + " h", Dim);
        }
        y += 8;

        Line("CREW (trust and morale are shown in the prototype)", Amber);
        foreach (var c in v.Crew)
        {
            var state = c.State switch
            {
                CrewState.Asleep => "asleep",
                CrewState.Thawing => "thawing, " + Voyage.Hours(Math.Max(0, c.ThawDoneAt - v.Clock)),
                CrewState.Awake => "AWAKE " + N(c.HoursAwake / 24) + " d" + (c.HoursAwake >= Tuning.MinAwakeHours ? ", can sleep" : ""),
                _ => "dead",
            };
            var color = c.State switch { CrewState.Dead => Red, CrewState.Awake => Green, CrewState.Thawing => Cyan, _ => Plain };
            Line(c.Name + ", " + c.Role + ", " + N(c.Age) + ": " + state, color);
            if (!c.Alive)
                continue;
            var skills = string.Join(" ", c.Skills.Select(s => s.Key.ToString()[..3].ToLowerInvariant() + s.Value));
            Line("  trust " + N(c.Trust) + " (" + CrewMember.BandText(c.Band) + ")  morale " + N(c.Morale) + "  health " + N(c.Health) + "  " + skills, Dim);
            if (c.State == CrewState.Awake)
            {
                if (c.PendingQuestion != null)
                    Line("  asked: " + v.GetFact(c.PendingQuestion).Question, Cyan);
                if (c.WorkingOn != null)
                    Line("  working on " + c.WorkingOn, Dim);
                if (c.Doubts.Count > 0)
                    Line("  doubts: " + string.Join(", ", c.Doubts), Dim);
            }
        }
        y += 8;

        var mind = _mind;
        if (mind != null)
        {
            Line("MODEL: " + mind.Llm.Name + (mind.IsReading ? "   reading your line..." : mind.Busy ? "   writing..." : ""), Dim);
            Line("Speaking to: " + (_target?.Name ?? "everyone awake") + "   (/to <name> or /to all)", Dim);
        }
    }

    private void DrawTranscript(Voyage v, int x, int y, int width, int height)
    {
        var columns = (int)(width / _charWidth);
        const int step = 24;
        var visible = Math.Max(1, height / step);
        var lines = new List<(string Text, Color Color)>();
        foreach (var entry in v.Log)
        {
            var color = entry.Kind switch
            {
                LogKind.Ship => Amber,
                LogKind.Crew => Cyan,
                LogKind.You => Green,
                LogKind.Recorded => Dim,
                _ => Dim,
            };
            var text = entry.Kind switch
            {
                LogKind.Crew => entry.Speaker + ": " + (entry.Pending ? "..." : entry.Text),
                LogKind.You => "YOU: " + entry.Text,
                LogKind.Recorded => "    " + entry.Text,
                _ => entry.Text,
            };
            foreach (var line in Wrap(text, columns))
                lines.Add((line, color));
            if (entry.Kind is LogKind.Ship or LogKind.Crew or LogKind.You)
                lines.Add(("", color));
        }
        _scroll = Math.Min(_scroll, Math.Max(0, lines.Count - visible));
        var first = Math.Max(0, lines.Count - visible - _scroll);
        var last = Math.Min(lines.Count, first + visible);
        for (var i = first; i < last; i++)
            Text(_font, lines[i].Text, x, y + (i - first) * step, lines[i].Color);
        if (_scroll > 0)
            Text(_small, "-- scrolled back " + _scroll + " lines: PageDown --", x, y + height - 4, Dim);
    }

    private void DrawInput(int x, int y, int width)
    {
        _batch.Draw(_pixel, new Rectangle(x - 8, y - 8, width + 16, 1), Dim);
        var columns = Math.Max(10, (int)(width / _charWidth) - 3);
        var text = _input.ToString();
        if (text.Length > columns)
            text = text[^columns..];
        var cursor = DateTime.UtcNow.Millisecond < 500 ? "_" : " ";
        Text(_font, "> " + text + cursor, x, y, Plain);
    }
}
