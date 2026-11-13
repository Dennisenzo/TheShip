using System.Text;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheShip.AI;
using TheShip.Core;

namespace TheShip.Game;

/// <summary>
/// The window. Four screens in the wireframe look of the interface design: the bridge (ship plan, issues, crew, log),
/// a crew dossier, the record with the ship logs, and the ending. Every control is a button or a click; the input line is only
/// for speaking. A typed line is read by the model, shown as it will be recorded, and said once the player confirms it.
/// </summary>
public sealed partial class ShipGame : Microsoft.Xna.Framework.Game
{
    private enum Screen { Bridge, Dossier, Record, Ending }

    private readonly GraphicsDeviceManager _graphics;
    private readonly Options _options;
    private readonly MainThreadQueue _main = new();
    private readonly Queue<char> _typed = new();
    private readonly StringBuilder _input = new();
    private readonly List<string> _history = new();
    /// <summary>What a click asked for; run at the start of the next update, never in the middle of a draw.</summary>
    private readonly List<Action> _actions = new();
    private readonly Sounds _sounds = new();
    private int _historyAt;
    private int _scroll;
    private KeyboardState _prevKeys;
    private int _prevWheel;
    private ButtonState _prevLeft = ButtonState.Released;
    private bool _click;

    private SpriteBatch _batch = null!;
    private Texture2D _pixel = null!;
    private FontSystem _fonts = null!;
    private Painter _p = null!;

    private Task<ILlmProvider>? _loading;
    /// <summary>Written by the loading task as well as the game thread.</summary>
    private volatile string _loadingText = "";
    private Voyage? _voyage;
    private ShipMind? _mind;
    /// <summary>The record of this run in Sessions/ (see <see cref="SessionLog"/>).</summary>
    private SessionLog? _session;
    /// <summary>The person selected on the bridge: who you speak to alone, and who repair requests go to.</summary>
    private CrewMember? _target;
    private CrewMember? _dossier;
    private Screen _screen = Screen.Bridge;
    private bool _endingShown;
    /// <summary>No model could be loaded: the window only shows why.</summary>
    private bool _fatal;

    public ShipGame(Options options)
    {
        _options = options;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = Painter.ScreenWidth,
            PreferredBackBufferHeight = Painter.ScreenHeight,
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

    protected override void Initialize()
    {
        base.Initialize();
        WindowState.Maximize(Window.Handle);
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _sounds.Load();
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _fonts = new FontSystem();
        _fonts.AddFont(LoadFont());
        _p = new Painter(_batch, _pixel, _fonts);

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
        _loading = Task.Run<ILlmProvider>(async () =>
        {
            var llm = new LocalLlm(path, gpuLayerCount: layers);
            // The first calls carry one-time costs (GPU shader compilation can take a minute): pay them here, not on the player's first line.
            _loadingText = "Warming up " + Path.GetFileName(path) + " on " + llm.Backend + " ..."
                           + "\nThe first run after installing the game or a graphics driver can take a minute. Later runs are quick.";
            try
            {
                await ShipMind.WarmUpAsync(llm);
            }
            catch (Exception)
            {
                // A model that can't answer the warm-up shows it on the first real call; the load itself worked.
            }
            return llm;
        });
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
        _voyage = new Voyage(_options.Seed, Variation.Draw(_options.Seed));
        _session = new SessionLog("seed " + _options.Seed + ", variation " + _voyage.Variation + ", model " + llm.Name + " on " + llm.Backend);
        llm.CallCompleted += s => _session.Line("llm", s.Kind + ": " + s.PromptTokens + " prompt tokens, " + s.GeneratedTokens
                                                       + " generated, first token " + s.FirstToken.TotalSeconds.ToString("0.00") + " s, total "
                                                       + s.Total.TotalSeconds.ToString("0.00") + " s" + (s.TrimmedTurns > 0 ? ", trimmed " + s.TrimmedTurns : ""));
        _held.Clear();
        _mind = new ShipMind(_voyage, llm, _main) { ReadTimeout = TimeSpan.FromSeconds(ReadHoldSeconds) };
        _mind.LineDropped += text => _session.Line("read", "dropped by the game (nobody who heard it is awake, or the voyage had ended): " + text);
        _mind.Trace += text => _session.Line("model", text);
        _mind.LineRead += (text, reading, failed) => _session.Line("read",
            (failed ? "the model's reading was unusable, recorded as plain talk; " : "") + Describe(_voyage!, reading) + ": " + text);
        Note(note);
        if (_session.FilePath is { } file)
            Note("This run is recorded in " + file + ".");
        _session.Snapshot(_voyage);
        Note("Wake someone from the crew list, then advance the clock. Type to speak to whoever is awake; select a person to speak to them alone.");
    }

    private void Note(string text) => _voyage?.Log.Add(new LogEntry(LogKind.Help, "", text));

    /// <summary>Queue what a click asked for.</summary>
    private void Do(Action action)
    {
        _sounds.Click(); // every button, card, door and tab queues its click here
        _actions.Add(action);
    }

    /// <summary>
    /// The longest the model may take over the reading of a said line, counted from when it starts on it (a hanging model);
    /// after that the call is stopped and the line is applied as plain talk. Readings take about 0.4 s.
    /// </summary>
    private const double ReadHoldSeconds = 5;

    /// <summary>
    /// Queue a click that changes the game, and record it in the session log (with the state after a clock move).
    /// While a said line is still being read the click waits for it (<see cref="ShipMind.AfterReading"/>), so the line
    /// counts at the moment it was said: before a Sleep, an Advance or a Repair clicked right after it.
    /// </summary>
    private void Act(string what, Action action, bool snapshot = false) => Do(() =>
    {
        void Run()
        {
            _held.Remove(what);
            _session?.Line("click", what);
            action();
            if (snapshot && _voyage is not null)
            {
                _session?.Follow(_voyage);
                _session?.Snapshot(_voyage);
            }
        }
        if (_mind is null)
        {
            Run();
            return;
        }
        if (_mind.Holding)
        {
            // A clock move clicked twice while a line is read would move the clock twice once it lands: the second is ignored.
            if (snapshot && !_held.Add(what))
            {
                _session?.Line("click", what + " (ignored: the same click is already waiting for the reading)");
                return;
            }
        }
        _mind.AfterReading(Run);
    });

    /// <summary>Clock moves waiting for a said line to be read (<see cref="Act"/>), so a second identical click is ignored.</summary>
    private readonly HashSet<string> _held = new();

    /// <summary>
    /// While a said line is being read the clicks that change the game wait for it: a small note at the right of the
    /// input line says so, so a held Advance does not look like a dead button.
    /// </summary>
    private void DrawHoldNote(Rectangle inputLine)
    {
        if (_mind is not { Holding: true })
            return;
        var dots = new string('.', 1 + (int)(DateTime.UtcNow.TimeOfDay.TotalMilliseconds / 400 % 3));
        var text = (_held.Count > 0 ? "reading your line, your click waits" : "reading your line") + dots.PadRight(3);
        _p.TextRight(_p.Small, text, inputLine.Right - 14, inputLine.Y + 14, Painter.Dim);
    }

    /// <summary>How the 1600 x 900 layout sits in the window: its scale and its top-left corner.</summary>
    private (float Scale, float X, float Y) View()
    {
        var width = GraphicsDevice.Viewport.Width;
        var height = GraphicsDevice.Viewport.Height;
        var scale = Math.Max(0.1f, Math.Min(width / (float)Painter.ScreenWidth, height / (float)Painter.ScreenHeight));
        return (scale, (width - Painter.ScreenWidth * scale) / 2f, (height - Painter.ScreenHeight * scale) / 2f);
    }

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

        if (_actions.Count > 0)
        {
            var actions = _actions.ToList();
            _actions.Clear();
            foreach (var action in actions)
                action();
            _scroll = 0;
        }
        if (_voyage is not null)
            _session?.Follow(_voyage);

        var mouse = Mouse.GetState();
        var view = View();
        if (_p != null)
            _p.Mouse = new Vector2((mouse.X - view.X) / view.Scale, (mouse.Y - view.Y) / view.Scale);
        if (_prevLeft == ButtonState.Pressed && mouse.LeftButton == ButtonState.Released && IsActive)
            _click = true;
        _prevLeft = mouse.LeftButton;
        _scroll += (mouse.ScrollWheelValue - _prevWheel) / 40;
        _prevWheel = mouse.ScrollWheelValue;

        var keys = Keyboard.GetState();
        bool Pressed(Keys k) => keys.IsKeyDown(k) && !_prevKeys.IsKeyDown(k);
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
            if (_screen == Screen.Record && _voyage is { Phase: Phase.Ended })
                _screen = Screen.Ending;
            else if (_screen != Screen.Bridge)
                _screen = Screen.Bridge;
            else
                _input.Clear();
        }
        _prevKeys = keys;

        while (_typed.Count > 0)
        {
            var ch = _typed.Dequeue();
            if (_screen != Screen.Bridge)
                continue;
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
            _screen = Screen.Ending;
            _scroll = int.MaxValue / 2;
            ShowEnding(ending);
            _session?.Follow(_voyage);
            _session?.Ending(ending);
            _session?.Snapshot(_voyage);
        }
        StepTour();
        base.Update(gameTime);
    }

    /// <summary>Enter: the line is said at once. The model reads it right after and the reading is applied when it arrives. There are no typed controls.</summary>
    private void Submit()
    {
        var text = _input.ToString().Trim();
        _input.Clear();
        if (_voyage is null || _mind is null || text.Length == 0)
            return;
        _history.Add(text);
        _historyAt = _history.Count;
        _scroll = 0;
        if (_voyage.Phase == Phase.Ended)
            return;
        var hearers = _target is { State: CrewState.Awake } one ? new List<CrewMember> { one } : _voyage.Awake.ToList();
        if (hearers.Count == 0)
        {
            Note("Nobody is awake to hear you.");
            return;
        }
        _session?.Line("said", "to " + string.Join(", ", hearers.Select(h => h.Name)) + ": " + text);
        _mind.SayAtOnce(text, hearers);
    }

    /// <summary>How a line was recorded, in words, for the session log.</summary>
    private string Describe(Voyage v, Reading reading) => reading.Kind switch
    {
        LineKind.Statement when reading.FactKey != null && reading.Value != null && v.HasFact(reading.FactKey)
            => "a statement that " + v.GetFact(reading.FactKey).Say(reading.Value),
        LineKind.Statement => "a statement that fits no ship fact (kept as text)",
        LineKind.Promise => "a promise " + Voyage.PromiseText(reading.PromiseKey),
        LineKind.Dodge => "a dodge of what was asked",
        LineKind.Question => "a question",
        LineKind.Order => "an order",
        _ => "plain talk (nothing recorded)",
    };

    private void ShowEnding(Ending ending) =>
        Note("The voyage is over: " + ending.Title + ". The ending screen shows the crew, what they believe and the record.");

    protected override void UnloadContent()
    {
        _mind?.Dispose();
        _session?.Line("quit", _voyage?.Phase == Phase.Ended ? "after the ending" : "before the end of the voyage");
        _session?.Dispose();
        _tourTarget?.Dispose();
        _fonts?.Dispose();
        base.UnloadContent();
    }

    protected override void Draw(GameTime gameTime)
    {
        DrawScene();
        _click = false;
        base.Draw(gameTime);
    }

    /// <summary>One frame of the current screen into whatever render target (or the back buffer) is set.</summary>
    private void DrawScene()
    {
        GraphicsDevice.Clear(Painter.Ground);
        var view = View();
        _p.Click = _click;
        _batch.Begin(transformMatrix: Matrix.CreateScale(view.Scale, view.Scale, 1f) * Matrix.CreateTranslation(view.X, view.Y, 0f));
        if (_voyage is null)
        {
            var row = 0;
            foreach (var line in Painter.Wrap(_loadingText, (int)((Painter.ScreenWidth - 80) / _p.BodyWidth)))
                _p.Text(_p.Body, line, 40, 40 + 26 * row++, _fatal ? Painter.Accent : Painter.Ink);
        }
        else if (_screen == Screen.Dossier && _dossier is not null)
        {
            DrawDossier(_voyage, _dossier);
        }
        else if (_screen == Screen.Ending && _voyage.Ending is { } ending)
        {
            DrawEnding(_voyage, ending);
        }
        else if (_screen == Screen.Record)
        {
            DrawRecord(_voyage);
        }
        else
        {
            DrawBridge(_voyage);
        }
        _batch.End();
    }

    /// <summary>The bar on top of every screen: where you are, the year, and the phase of the voyage.</summary>
    private void Header(Voyage v, string title, bool back)
    {
        _p.Fill(new Rectangle(0, 0, Painter.ScreenWidth, 56), Painter.Panel);
        _p.Fill(new Rectangle(0, 56, Painter.ScreenWidth, 1), Painter.Edge);
        var x = 16;
        if (back)
        {
            if (_p.Button(new Rectangle(16, 8, 120, 40), "< BRIDGE"))
                Do(() => _screen = Screen.Bridge);
            x = 152;
        }
        _p.Text(_p.Head, title, x, 16, Painter.Bright);
        x += (int)_p.Head.MeasureString(title).X + 32;
        _p.Text(_p.Small, "YEAR", x, 21, Painter.Dim);
        _p.Text(_p.Head, Math.Floor(v.Year).ToString("0"), x + 44, 16, Painter.Bright);
        _p.Text(_p.Small, "/ " + Scenario.ArrivalYear, x + 76, 21, Painter.Dim);
        var phase = v.Phase switch
        {
            Phase.Alert => "ALERT " + v.Current.Number + " OF " + v.Alerts.Count + ": " + v.Current.Title.ToUpperInvariant(),
            Phase.Aftermath => "AFTERMATH OF ALERT " + v.Current.Number + ": " + v.Current.Title.ToUpperInvariant(),
            _ => v.Ending?.Title.ToUpperInvariant() ?? "ARRIVED",
        };
        if (v.Phase == Phase.Ended && _screen != Screen.Ending)
        {
            // The ending screen can always be opened again.
            var edge = Painter.ScreenWidth - 16;
            if (_p.Button(new Rectangle(title == "THE SHIP" ? edge - 410 : edge - 110, 8, 110, 40), "ENDING"))
                Do(() => _screen = Screen.Ending);
        }
        var chip = new Rectangle(x + 124, 14, (int)(phase.Length * _p.SmallWidth) + 20, 28);
        _p.Frame(chip, v.Phase == Phase.Alert ? Painter.Accent : Painter.Wire);
        _p.Text(_p.Small, phase, chip.X + 10, chip.Y + 6, v.Phase == Phase.Alert ? Painter.Accent : Painter.Ink);
        // The lockouts the crew hold: a chip each, so the loss of Wake and Sleep is never a mystery.
        var lockText = v.Lockouts.Bay ? "BAY TAKEN BY THE CREW" : v.Lockouts.BayWarned.Count > 0 ? "CREW WILL TAKE THE BAY" : null;
        if (lockText != null)
        {
            var lock1 = new Rectangle(chip.Right + 12, 14, (int)(lockText.Length * _p.SmallWidth) + 20, 28);
            _p.Frame(lock1, Painter.Accent);
            _p.Text(_p.Small, lockText, lock1.X + 10, lock1.Y + 6, Painter.Accent);
        }
    }
}
