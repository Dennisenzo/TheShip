using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using LLama.Transformers;

namespace TheShip.AI;

/// <summary>
/// A local GGUF chat model via LLamaSharp: Vulkan GPU if available, CPU otherwise, no network.
/// Ported from the Cities repo's LocalLlm, keeping its infrastructure (native library setup, chat templates
/// incl. Gemma 4 and Qwen3 thinking suppression, reply cleanup, timing stats). Left out for now: the model host
/// process, the CUDA backend, the reply prefix cache and the VRAM budget.
///
/// Every call runs in a fresh context (StatelessExecutor), with the prompt rebuilt from game state.
/// </summary>
public sealed class LocalLlm : ILlmProvider
{
    private readonly LLamaWeights _weights;
    private readonly StatelessExecutor _executor;
    private readonly bool _suppressThinking;
    private readonly bool _gemma4Turns;
    private readonly int _contextSize;

    public string Name { get; }
    public string Backend { get; }

    public event Action<LlmCallStats>? CallCompleted;

    // llama.cpp's native logs are only shown while the model loads (to see the GPU being picked up);
    // afterwards only errors get through. Each call makes a fresh context, which is otherwise noisy.
    private static volatile bool _showNativeLogs = true;
    private static bool _lastNativeLogShown;
    public static Action<string>? NativeLog { get; set; }

    // 4096: the prompts here are well under 1K tokens (one to three people awake, about 20 facts).
    public LocalLlm(string modelPath, int contextSize = 4096, int gpuLayerCount = 99)
    {
        _showNativeLogs = true;
        _contextSize = contextSize;
        ConfigureNativeLibrary(vulkan: gpuLayerCount > 0);

        // GpuLayerCount 99 = "offload everything that fits"; ignored on a CPU-only load.
        // Flash attention with an 8-bit KV cache and a smaller micro-batch keep the context small in VRAM.
        var parameters = new ModelParams(modelPath)
        {
            ContextSize = (uint)contextSize,
            GpuLayerCount = gpuLayerCount,
            FlashAttention = true,
            TypeK = GGMLType.GGML_TYPE_Q8_0,
            TypeV = GGMLType.GGML_TYPE_Q8_0,
            UBatchSize = 256
        };
        _weights = LLamaWeights.LoadFromFile(parameters);
        _executor = new StatelessExecutor(_weights, parameters);

        Name = _weights.Metadata.TryGetValue("general.name", out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : Path.GetFileNameWithoutExtension(modelPath);
        // Gemma 4's chat template is Jinja that llama.cpp's built-in matcher doesn't know, so its turns
        // are formatted by hand. Only hybrid reasoning models (Qwen3 etc.) have an enable_thinking switch.
        _gemma4Turns = _weights.Metadata.TryGetValue("general.architecture", out var architecture)
                       && architecture.StartsWith("gemma4", StringComparison.Ordinal);
        _suppressThinking = !_gemma4Turns
                            && _weights.Metadata.TryGetValue("tokenizer.chat_template", out var chatTemplate)
                            && chatTemplate.Contains("enable_thinking", StringComparison.Ordinal);
        Backend = DescribeLoadedBackend();
        _showNativeLogs = false;
    }

    // Generation already stops on the model's own end-of-turn token; these are a safety net.
    private static readonly List<string> AntiPrompts = new()
    {
        "<|end|>", "<|user|>", "<|system|>", "<|assistant|>",  // Phi
        "<|im_end|>", "<|im_start|>",                          // ChatML (Qwen)
        "[INST]", "</s>",                                      // Mistral
        "<turn|>", "<|turn>",                                  // Gemma 4
    };

    public async Task<string> ChatAsync(string system, IReadOnlyList<ChatTurn> turns, LlmOptions options,
        Action<string>? onToken = null, CancellationToken cancellationToken = default)
    {
        // The real tokenizer has the last word: the oldest turns go if the prompt and the reply wouldn't fit the context.
        var (kept, trimmed, promptTokens) = Prompts.FitContext(system, turns, options.MaxTokens, _contextSize,
            (s, t) => _weights.Tokenize(FormatChat(s, t), true, true, Encoding.UTF8).Length);
        var prompt = FormatChat(system, kept);
        var antiPrompts = options.Dialogue ? AntiPrompts.Append("\n\n").ToList() : AntiPrompts;
        var sampling = new DefaultSamplingPipeline
        {
            // Qwen3's recommended non-thinking settings; fine for the other families too. Variety
            // comes from the random seed (a fixed seed repeats the same reply for the same prompt).
            Temperature = options.Temperature,
            TopP = options.Temperature > 0 ? 0.8f : 1f,
            TopK = options.Temperature > 0 ? 20 : 1,
            PresencePenalty = options.PresencePenalty ?? (options.Dialogue ? 1.5f : 0f),
            RepeatPenalty = options.RepeatPenalty ?? (options.Dialogue ? 1.1f : 1f),
            Seed = (uint)Random.Shared.Next(),
            Grammar = options.Grammar is null ? null : new Grammar(options.Grammar, "root")
        };
        var inferenceParams = new InferenceParams
        {
            MaxTokens = options.MaxTokens,
            AntiPrompts = antiPrompts,
            SamplingPipeline = sampling
        };

        var builder = new StringBuilder();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan? firstToken = null;
        var generated = 0;
        await foreach (var token in _executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            firstToken ??= stopwatch.Elapsed;
            generated++;
            builder.Append(token);
            onToken?.Invoke(token);
        }

        CallCompleted?.Invoke(new LlmCallStats(options.Kind, promptTokens, generated, firstToken ?? stopwatch.Elapsed, stopwatch.Elapsed, trimmed));

        var raw = builder.ToString();
        return options.Dialogue ? CleanReply(raw) : StripTemplate(raw);
    }

    private static void OnNativeLog(LLamaLogLevel level, string message)
    {
        // "Continue" lines belong to the previous message - follow its visibility.
        var show = level == LLamaLogLevel.Continue ? _lastNativeLogShown : _showNativeLogs || level == LLamaLogLevel.Error;
        _lastNativeLogShown = show;
        if (show)
            NativeLog?.Invoke(message);
    }

    /// <summary>
    /// Must run before any other LLamaSharp call in the process (the library throws if a native
    /// library already loaded). Prefers Vulkan, falls back to CPU.
    /// </summary>
    private static void ConfigureNativeLibrary(bool vulkan)
    {
        try
        {
            // CPU-only must not load Vulkan: it would still offload prompt processing.
            NativeLibraryConfig.All
                .WithCuda(false)
                .WithVulkan(vulkan)
                .WithAutoFallback(true)
                .WithLogCallback(OnNativeLog);
        }
        catch (InvalidOperationException)
        {
            // Already loaded earlier in this process - nothing to do.
        }
    }

    /// <summary>Which llama.cpp backend got loaded (ggml-vulkan.dll = GPU, ggml-cpu.dll only = CPU).</summary>
    public static string DescribeLoadedBackend()
    {
        var modules = System.Diagnostics.Process.GetCurrentProcess().Modules
            .Cast<System.Diagnostics.ProcessModule>()
            .Select(m => m.FileName ?? string.Empty)
            .Where(f => Path.GetFileName(f).StartsWith("ggml-", StringComparison.OrdinalIgnoreCase)
                        && !Path.GetFileName(f).Equals("ggml-base.dll", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToList();
        return modules.Count == 0 ? "unknown (no ggml backend module found)" : string.Join(", ", modules);
    }

    /// <summary>Formats a conversation with the model's own chat template, ready for its reply.</summary>
    private string FormatChat(string systemPrompt, IEnumerable<ChatTurn> turns)
    {
        if (_gemma4Turns)
            return FormatGemma4(systemPrompt, turns);

        var template = new LLamaTemplate(_weights) { AddAssistant = true };
        template.Add("system", systemPrompt);
        foreach (var turn in turns)
            template.Add(turn.Role, turn.Content);
        var prompt = PromptTemplateTransformer.ToModelPrompt(template);
        // Same as Qwen3's enable_thinking=false: an already-closed, empty think block.
        return _suppressThinking ? prompt + "<think>\n\n</think>\n\n" : prompt;
    }

    /// <summary>Gemma 4's turn format (thinking off): &lt;|turn&gt;role\n...&lt;turn|&gt;\n, "model" for the assistant.</summary>
    private static string FormatGemma4(string systemPrompt, IEnumerable<ChatTurn> turns)
    {
        var sb = new StringBuilder();
        sb.Append("<|turn>system\n").Append(systemPrompt.Trim()).Append("<turn|>\n");
        foreach (var turn in turns)
            sb.Append("<|turn>").Append(turn.Role == "assistant" ? "model" : turn.Role).Append('\n').Append(turn.Content.Trim()).Append("<turn|>\n");
        return sb.Append("<|turn>model\n").ToString();
    }

    // Gemma 4 26B opens every answer with an (empty) thought channel: <|channel>thought ... <channel|>.
    private static readonly System.Text.RegularExpressions.Regex ThoughtChannel =
        new(@"<\|channel>.*?<channel\|>", System.Text.RegularExpressions.RegexOptions.Singleline);

    private static string StripTemplate(string raw)
    {
        var text = ThoughtChannel.Replace(raw, "");
        var thinkEnd = text.IndexOf("</think>", StringComparison.Ordinal);
        if (thinkEnd >= 0)
            text = text[(thinkEnd + "</think>".Length)..];
        foreach (var tag in new[] { "<turn|>", "<|im_end|>", "<|end|>", "</s>" })
        {
            var i = text.IndexOf(tag, StringComparison.Ordinal);
            if (i >= 0)
                text = text[..i];
        }
        return text.Trim();
    }

    /// <summary>
    /// Drops any reasoning block, strips stray template tags, cuts at the first blank line, removes a
    /// "Name:" prefix and asterisk actions, and trims a trailing half-sentence.
    /// </summary>
    public static string CleanReply(string raw)
    {
        var text = ThoughtChannel.Replace(raw, "");
        var thinkEnd = text.IndexOf("</think>", StringComparison.Ordinal);
        if (thinkEnd >= 0)
            text = text[(thinkEnd + "</think>".Length)..];
        else if (text.TrimStart().StartsWith("<think>", StringComparison.Ordinal))
            text = string.Empty;

        var gemmaEnd = text.IndexOf("<turn|>", StringComparison.Ordinal);
        if (gemmaEnd >= 0)
            text = text[..gemmaEnd];
        var tagIndex = text.IndexOf("<|", StringComparison.Ordinal);
        if (tagIndex >= 0)
            text = text[..tagIndex];
        var blankLineIndex = text.IndexOf("\n\n", StringComparison.Ordinal);
        if (blankLineIndex >= 0)
            text = text[..blankLineIndex];

        // *shivers* style stage directions.
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*[^*]{1,60}\*", "").Replace("  ", " ");
        text = text.Trim().Trim('"').Trim();

        var colonIndex = text.IndexOf(':');
        if (colonIndex > 0 && colonIndex < 40 && text[..colonIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3)
            text = text[(colonIndex + 1)..].Trim().Trim('"').Trim();

        if (text.Length > 0 && !".!?\"'".Contains(text[^1]))
        {
            var lastEnd = text.LastIndexOfAny(new[] { '.', '!', '?' });
            if (lastEnd > 0)
                text = text[..(lastEnd + 1)];
        }
        return text.Length == 0 ? "..." : text;
    }

    public void Dispose()
    {
        _weights.Dispose();
    }
}
