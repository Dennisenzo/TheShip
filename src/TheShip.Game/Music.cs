using System;
using System.IO;
using System.Threading;

namespace TheShip.Game;

/// <summary>Which mood the music is in. The game picks it from what it already shows; the music never changes the game.</summary>
public enum MusicMood { Calm, Alert, Drift, Landfall, Bleak }

/// <summary>
/// Every number of the sound of the ambient music and the alarm. These are sound, not game tuning, so they live here and not in
/// Tuning.cs. Nothing in this file needs an audio device or MonoGame: it is plain DSP, rendered by <see cref="MusicEngine"/>.
/// </summary>
public static class MusicParams
{
    public const int SampleRate = 44100;
    /// <summary>Loudness of the whole music, 0 to 1. The interface sounds in Sounds.cs sit at 0.25 to 0.3.</summary>
    public const float MasterVolume = 0.16f;
    /// <summary>Loudness of the alarm, 0 to 1 (mixed after the music, not through its master volume).</summary>
    public const float AlarmVolume = 0.35f;
    /// <summary>Seconds a mood change takes to settle (the chord glides, the filter and the levels follow).</summary>
    public const float MoodFadeSeconds = 7f;
    /// <summary>Music level while the alarm sounds (1 = no ducking), how fast it falls and how long it takes to come back.</summary>
    public const float DuckGain = 0.2f;
    public const float DuckAttackSeconds = 0.08f;
    public const float DuckReleaseSeconds = 2.5f;
    /// <summary>Seconds the music stays ducked after the alarm's last whoop.</summary>
    public const float DuckHoldSeconds = 0.4f;
    /// <summary>Seconds the music takes to fade when it is switched on or off.</summary>
    public const float SwitchSeconds = 1.5f;

    /// <summary>Slow movement: the filter sweep period in seconds and its depth.</summary>
    public const float FilterSweepSeconds = 43f;
    public const float FilterSweepDepth = 0.45f;
    public const float WobbleBaseHz = 0.11f;
    /// <summary>Detune between the two oscillators of a note, in cents (their beating is the wobble).</summary>
    public const float DetuneCents = 7f;

    /// <summary>Reverb and echo.</summary>
    public const float CombFeedback = 0.89f;
    public const float CombDamping = 0.35f;
    public const float EchoSeconds = 0.43f;
    public const float EchoFeedback = 0.48f;

    /// <summary>Bell tones: length of their ring (seconds), loudness, pool size.</summary>
    public const float BellDecaySeconds = 2.6f;
    public const float BellVolume = 0.11f;
    public const int BellVoices = 6;

    /// <summary>The alarm: three rising whoops.</summary>
    public const int AlarmWhoops = 3;
    public const float AlarmWhoopSeconds = 0.5f;
    public const float AlarmGapSeconds = 0.12f;
    public const float AlarmLowHz = 520f;
    public const float AlarmOctaves = 0.85f;
    public const float AlarmAttackSeconds = 0.025f;
    public const float AlarmReleaseSeconds = 0.09f;
    /// <summary>How much of the alarm goes through the reverb.</summary>
    public const float AlarmWet = 0.25f;
    public static float AlarmSeconds => AlarmWhoops * (AlarmWhoopSeconds + AlarmGapSeconds) - AlarmGapSeconds;

    /// <summary>The mapping from an alert's open faults to tension (0 to 1) for the alert mood.</summary>
    public const float TensionCritical = 0.4f;
    public const float TensionPerExtraFault = 0.1f;
    public const float TensionShortHours = 6f;
    public const float TensionMediumHours = 24f;
    public const float TensionShort = 0.4f;
    public const float TensionMedium = 0.2f;
}

/// <summary>Reads the game's state into tension; pure, so it can be tested.</summary>
public static class MusicTension
{
    /// <summary>0 to 1 from the open faults of the alert: a critical one, more of them, and little time left raise it.</summary>
    public static float Of(int openFaults, int criticalOpen, double shortestClockHours)
    {
        if (openFaults <= 0)
            return 0f;
        var t = criticalOpen > 0 ? MusicParams.TensionCritical : 0f;
        t += MusicParams.TensionPerExtraFault * (openFaults - 1);
        if (shortestClockHours <= MusicParams.TensionShortHours)
            t += MusicParams.TensionShort;
        else if (shortestClockHours <= MusicParams.TensionMediumHours)
            t += MusicParams.TensionMedium;
        return Math.Clamp(t, 0f, 1f);
    }
}

/// <summary>What a mood sounds like: a chord (root and ratios), the voices' levels and the movement.</summary>
public sealed class MoodSpec
{
    public float Root;
    public readonly float[] Ratios = new float[4];
    public readonly float[] Weights = new float[4];
    public float Cutoff;        // Hz of the drone's low-pass, before the sweep
    public float Cents;         // depth of the pitch wobble
    public float WobbleRate;    // speed of the wobble, 1 = MusicParams.WobbleBaseHz
    public float Pulse;         // level of the low pulse
    public float PulseHz;
    public float Noise;         // level of the hull hiss
    public float BellsPerMinute;
    public float Minor;         // 0 = major pentatonic bells, 1 = minor
    public float Wet;           // how much reverb is heard
    public float Drone;         // level of the drone
    public float Bright;        // second harmonic in the drone

    public MoodSpec Set(float root, float[] ratios, float[] weights, float cutoff, float cents, float wobble, float pulse, float pulseHz,
        float noise, float bells, float minor, float wet, float drone, float bright)
    {
        Root = root; Cutoff = cutoff; Cents = cents; WobbleRate = wobble; Pulse = pulse; PulseHz = pulseHz; Noise = noise;
        BellsPerMinute = bells; Minor = minor; Wet = wet; Drone = drone; Bright = bright;
        for (var i = 0; i < 4; i++)
        {
            Ratios[i] = ratios[i];
            Weights[i] = weights[i];
        }
        return this;
    }

    /// <summary>this = a + (b - a) * t. Every field is read before it is written, so <paramref name="a"/> may be this.</summary>
    public void LerpInto(MoodSpec a, MoodSpec b, float t)
    {
        static float L(float x, float y, float t) => x + (y - x) * t;
        Root = L(a.Root, b.Root, t); Cutoff = L(a.Cutoff, b.Cutoff, t); Cents = L(a.Cents, b.Cents, t);
        WobbleRate = L(a.WobbleRate, b.WobbleRate, t); Pulse = L(a.Pulse, b.Pulse, t); PulseHz = L(a.PulseHz, b.PulseHz, t);
        Noise = L(a.Noise, b.Noise, t); BellsPerMinute = L(a.BellsPerMinute, b.BellsPerMinute, t); Minor = L(a.Minor, b.Minor, t);
        Wet = L(a.Wet, b.Wet, t); Drone = L(a.Drone, b.Drone, t); Bright = L(a.Bright, b.Bright, t);
        for (var i = 0; i < 4; i++)
        {
            Ratios[i] = L(a.Ratios[i], b.Ratios[i], t);
            Weights[i] = L(a.Weights[i], b.Weights[i], t);
        }
    }

    public void CopyFrom(MoodSpec a) => LerpInto(a, a, 0f);

    public static readonly MoodSpec Calm = new MoodSpec().Set(55f, new[] { 1f, 3f, 4f, 4.5f }, new[] { .35f, .25f, .22f, .18f },
        1400, 5, 1f, 0f, 1f, .03f, 5, 0, .35f, 1f, .25f);
    public static readonly MoodSpec AlertCalm = new MoodSpec().Set(55f, new[] { 1f, 2.4f, 3f, 3.556f }, new[] { .35f, .25f, .22f, .18f },
        1800, 8, 1.5f, .25f, 1.2f, .05f, 3, 1, .3f, 1f, .35f);
    public static readonly MoodSpec AlertTense = new MoodSpec().Set(51.9f, new[] { 1f, 2.4f, 2.8125f, 3.556f }, new[] { .38f, .24f, .2f, .18f },
        3000, 13, 2.2f, .5f, 2f, .09f, 2, 1, .3f, 1f, .5f);
    public static readonly MoodSpec Drift = new MoodSpec().Set(36.7f, new[] { 1f, 2f, 3f, 4.5f }, new[] { .45f, .3f, .2f, .12f },
        500, 4, .5f, 0f, 1f, .02f, 1, 1, .55f, 1.1f, .1f);
    public static readonly MoodSpec Landfall = new MoodSpec().Set(65.4f, new[] { 1f, 3f, 5f, 6f }, new[] { .3f, .25f, .22f, .22f },
        3200, 5, .8f, 0f, 1f, .02f, 8, 0, .4f, 1f, .3f);
    public static readonly MoodSpec Bleak = new MoodSpec().Set(49f, new[] { 1f, 2.133f, 3f, 4.2f }, new[] { .4f, .25f, .2f, .15f },
        700, 12, .7f, 0f, 1f, .06f, .8f, 1, .5f, 1f, .1f);
}

/// <summary>
/// The endless track. Plain DSP: <see cref="Render"/> fills interleaved stereo floats. Mood, tension, the alarm and the on/off switch are
/// set from another thread (single writes of simple values) and followed smoothly, so nothing clicks. No allocation while rendering.
/// </summary>
public sealed class MusicEngine
{
    private const float TwoPi = MathF.PI * 2f;
    private const int Block = 32;
    private const float Dt1 = 1f / MusicParams.SampleRate;

    // ---- Set from the game thread ----
    private volatile int _mood = (int)MusicMood.Calm;
    private volatile float _tension;
    private volatile bool _musicOn = true;
    private int _alarmRequest;
    private int _snap = 1;

    // ---- Target and current mood ----
    private readonly MoodSpec _target = new();
    private readonly MoodSpec _now = new();

    // ---- Drone ----
    private readonly float[] _phaseA = new float[4], _phaseB = new float[4], _incA = new float[4], _incB = new float[4], _weight = new float[4];
    private readonly float[] _lfoPhase = { 0.3f, 1.7f, 3.1f, 4.4f };
    private float _sweepPhase, _pulsePhase, _pulseGatePhase;
    private float _lpL1, _lpL2, _lpR1, _lpR2, _lpCoef = 0.1f;
    private float _noiseA, _noiseB, _noiseC, _noiseD;
    private uint _rng = 2463534242u;
    private float _pulseAmt, _noiseAmt, _wetAmt, _droneAmt, _brightAmt, _pulseHz;
    private bool _minorBells;

    // ---- Bells ----
    private readonly float[] _bellF = new float[MusicParams.BellVoices];
    private readonly float[] _bellT = new float[MusicParams.BellVoices];
    private readonly float[] _bellP = new float[MusicParams.BellVoices * 3];
    private readonly float[] _bellPan = new float[MusicParams.BellVoices];
    private float _bellCountdown = 2f;

    // ---- Space: four damped combs and two allpasses per side, and a ping-pong echo ----
    private static readonly int[] CombLens = { 1557, 1617, 1491, 1422 };
    private static readonly int[] AllpassLens = { 225, 556 };
    private readonly float[][] _combL = new float[4][], _combR = new float[4][];
    private readonly int[] _combPosL = new int[4], _combPosR = new int[4];
    private readonly float[] _combLpL = new float[4], _combLpR = new float[4];
    private readonly float[][] _apL = new float[2][], _apR = new float[2][];
    private readonly int[] _apPosL = new int[2], _apPosR = new int[2];
    private readonly float[] _echoL, _echoR;
    private int _echoPosL, _echoPosR;
    private float _echoLpL, _echoLpR;

    // ---- Levels ----
    private float _musicGain, _duck = 1f;
    private float _alarmT = -1f, _alarmPhase;

    public MusicEngine()
    {
        for (var i = 0; i < 4; i++)
        {
            _combL[i] = new float[CombLens[i]];
            _combR[i] = new float[CombLens[i] + 23];
        }
        for (var i = 0; i < 2; i++)
        {
            _apL[i] = new float[AllpassLens[i]];
            _apR[i] = new float[AllpassLens[i] + 11];
        }
        _echoL = new float[(int)(MusicParams.EchoSeconds * MusicParams.SampleRate)];
        _echoR = new float[(int)(MusicParams.EchoSeconds * 1.31f * MusicParams.SampleRate)];
        for (var i = 0; i < MusicParams.BellVoices; i++)
            _bellT[i] = -1f;
        _target.CopyFrom(MoodSpec.Calm);
    }

    /// <summary>The music is wanted (on) or not; it fades. The alarm sounds either way.</summary>
    public bool MusicOn { get => _musicOn; set => _musicOn = value; }

    /// <summary>True while there is something to hear: music on, music still fading out, or the alarm.</summary>
    public bool Active => _musicOn || _musicGain > 0.001f || _alarmT >= 0f || Volatile.Read(ref _alarmRequest) != 0;

    /// <summary>Choose the mood; <paramref name="tension"/> (0 to 1) only matters for the alert. <paramref name="snap"/> jumps there at once.</summary>
    public void SetMood(MusicMood mood, float tension = 0f, bool snap = false)
    {
        _tension = Math.Clamp(tension, 0f, 1f);
        _mood = (int)mood;
        if (snap)
            Volatile.Write(ref _snap, 1);
    }

    /// <summary>Sound the alarm now (ducks the music while it plays).</summary>
    public void TriggerAlarm() => Volatile.Write(ref _alarmRequest, 1);

    private float Rand() // -1..1
    {
        _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
        return (_rng & 0xFFFFFF) / (float)0x7FFFFF - 1f;
    }

    private float Rand01() => Rand() * 0.5f + 0.5f;

    private void ChooseTarget()
    {
        switch ((MusicMood)_mood)
        {
            case MusicMood.Alert: _target.LerpInto(MoodSpec.AlertCalm, MoodSpec.AlertTense, _tension); break;
            case MusicMood.Drift: _target.CopyFrom(MoodSpec.Drift); break;
            case MusicMood.Landfall: _target.CopyFrom(MoodSpec.Landfall); break;
            case MusicMood.Bleak: _target.CopyFrom(MoodSpec.Bleak); break;
            default: _target.CopyFrom(MoodSpec.Calm); break;
        }
    }

    /// <summary>Fill <paramref name="buffer"/> with <paramref name="frames"/> interleaved stereo frames in -1..1.</summary>
    public void Render(float[] buffer, int frames)
    {
        var done = 0;
        while (done < frames)
        {
            var n = Math.Min(Block, frames - done);
            Control(n * Dt1);
            for (var i = 0; i < n; i++)
            {
                Sample(out var l, out var r);
                buffer[(done + i) * 2] = l;
                buffer[(done + i) * 2 + 1] = r;
            }
            done += n;
        }
    }

    /// <summary>Once per block: follow the target, update the pitches and the slow movements.</summary>
    private void Control(float dt)
    {
        ChooseTarget();
        if (Interlocked.Exchange(ref _snap, 0) == 1)
            _now.CopyFrom(_target);
        _now.LerpInto(_now, _target, 1f - MathF.Exp(-dt / (MusicParams.MoodFadeSeconds / 3f)));

        if (Interlocked.Exchange(ref _alarmRequest, 0) == 1)
        {
            _alarmT = 0f;
            _alarmPhase = 0f;
        }
        var alarmOn = _alarmT >= 0f && _alarmT < MusicParams.AlarmSeconds + MusicParams.DuckHoldSeconds;
        var duckTarget = alarmOn ? MusicParams.DuckGain : 1f;
        var duckTime = duckTarget < _duck ? MusicParams.DuckAttackSeconds : MusicParams.DuckReleaseSeconds;
        _duck += (duckTarget - _duck) * (1f - MathF.Exp(-dt / duckTime));
        _musicGain += ((_musicOn ? 1f : 0f) - _musicGain) * (1f - MathF.Exp(-dt / (MusicParams.SwitchSeconds / 3f)));
        if (_alarmT > MusicParams.AlarmSeconds + MusicParams.DuckHoldSeconds + 0.5f)
            _alarmT = -1f;

        // Slow movement.
        _sweepPhase = Wrap(_sweepPhase + TwoPi * dt / MusicParams.FilterSweepSeconds);
        var cutoff = _now.Cutoff * (1f + MusicParams.FilterSweepDepth * MathF.Sin(_sweepPhase));
        _lpCoef = 1f - MathF.Exp(-TwoPi * Math.Clamp(cutoff, 60f, 12000f) / MusicParams.SampleRate);
        for (var n = 0; n < 4; n++)
        {
            _lfoPhase[n] = Wrap(_lfoPhase[n] + TwoPi * MusicParams.WobbleBaseHz * _now.WobbleRate * (0.7f + 0.25f * n) * dt);
            var bend = _now.Cents * MathF.Sin(_lfoPhase[n]);
            var f = _now.Root * _now.Ratios[n];
            var inc = TwoPi / MusicParams.SampleRate;
            _incA[n] = inc * f * CentsToRatio(bend - MusicParams.DetuneCents * 0.5f);
            _incB[n] = inc * f * CentsToRatio(-bend + MusicParams.DetuneCents * 0.5f);
            _weight[n] = _now.Weights[n];
        }
        _pulseAmt = _now.Pulse; _pulseHz = _now.PulseHz; _noiseAmt = _now.Noise; _wetAmt = _now.Wet; _droneAmt = _now.Drone; _brightAmt = _now.Bright;
        _minorBells = _now.Minor > 0.5f;

        // Bells: random timing, a pentatonic note, a long ring.
        _bellCountdown -= dt;
        if (_bellCountdown <= 0f && _now.BellsPerMinute > 0.05f)
        {
            _bellCountdown = 60f / _now.BellsPerMinute * (0.4f + 1.2f * Rand01());
            StartBell();
        }
        for (var b = 0; b < MusicParams.BellVoices; b++)
            if (_bellT[b] >= 0f)
            {
                _bellT[b] += dt;
                if (_bellT[b] > MusicParams.BellDecaySeconds * 7f)
                    _bellT[b] = -1f;
            }
    }

    private static readonly float[] MajorScale = { 1f, 9f / 8f, 5f / 4f, 3f / 2f, 5f / 3f };
    private static readonly float[] MinorScale = { 1f, 6f / 5f, 4f / 3f, 3f / 2f, 9f / 5f };

    private void StartBell()
    {
        var slot = -1;
        for (var b = 0; b < MusicParams.BellVoices; b++)
            if (_bellT[b] < 0f) { slot = b; break; }
        if (slot < 0)
            return;
        var scale = _minorBells ? MinorScale : MajorScale;
        var step = Math.Min(4, (int)(Rand01() * 5f));
        var octave = Rand01() < 0.4f ? 2f : 1f;
        _bellF[slot] = _now.Root * 8f * scale[step] * octave;
        _bellT[slot] = 0f;
        _bellPan[slot] = Rand() * 0.8f;
        _bellP[slot * 3] = _bellP[slot * 3 + 1] = _bellP[slot * 3 + 2] = 0f;
    }

    private static float CentsToRatio(float cents) => MathF.Pow(2f, cents / 1200f);

    private static float Wrap(float p) => p >= TwoPi ? p - TwoPi : p;

    private void Sample(out float outL, out float outR)
    {
        float l = 0f, r = 0f;
        // Drone: two slightly detuned oscillators per note, one leaning left, one right.
        for (var n = 0; n < 4; n++)
        {
            var pa = _phaseA[n] = Wrap(_phaseA[n] + _incA[n]);
            var pb = _phaseB[n] = Wrap(_phaseB[n] + _incB[n]);
            var sa = MathF.Sin(pa) + _brightAmt * MathF.Sin(2f * pa);
            var sb = MathF.Sin(pb) + _brightAmt * MathF.Sin(2f * pb);
            var w = _weight[n] * 0.5f * _droneAmt;
            l += w * (sa * 0.8f + sb * 0.2f);
            r += w * (sb * 0.8f + sa * 0.2f);
        }
        // A low pulse on the root.
        if (_pulseAmt > 0.001f)
        {
            _pulsePhase = Wrap(_pulsePhase + _incA[0]);
            _pulseGatePhase = Wrap(_pulseGatePhase + TwoPi * _pulseHz * Dt1);
            var gate = 0.5f + 0.5f * MathF.Sin(_pulseGatePhase);
            var p = MathF.Sin(_pulsePhase) * gate * gate * gate * _pulseAmt * 0.5f;
            l += p; r += p;
        }
        // Low-pass the drone (two poles).
        _lpL1 += (l - _lpL1) * _lpCoef; _lpL2 += (_lpL1 - _lpL2) * _lpCoef;
        _lpR1 += (r - _lpR1) * _lpCoef; _lpR2 += (_lpR1 - _lpR2) * _lpCoef;
        var dl = _lpL2; var dr = _lpR2;
        // Hull hiss: noise through four one-poles, soft.
        if (_noiseAmt > 0.001f)
        {
            _noiseA += (Rand() - _noiseA) * 0.12f; _noiseB += (_noiseA - _noiseB) * 0.12f;
            _noiseC += (Rand() - _noiseC) * 0.09f; _noiseD += (_noiseC - _noiseD) * 0.09f;
            dl += _noiseB * _noiseAmt * 2.2f;
            dr += _noiseD * _noiseAmt * 2.2f;
        }
        // Bells.
        float bl = 0f, br = 0f;
        for (var b = 0; b < MusicParams.BellVoices; b++)
        {
            var t = _bellT[b];
            if (t < 0f) continue;
            var env = MathF.Min(1f, t / 0.004f) * MathF.Exp(-t / MusicParams.BellDecaySeconds);
            var f = _bellF[b];
            var i3 = b * 3;
            _bellP[i3] = Wrap(_bellP[i3] + TwoPi * f * Dt1);
            _bellP[i3 + 1] = Wrap(_bellP[i3 + 1] + TwoPi * f * 2.756f * Dt1);
            _bellP[i3 + 2] = Wrap(_bellP[i3 + 2] + TwoPi * f * 5.404f * Dt1);
            var s = (MathF.Sin(_bellP[i3]) + 0.35f * MathF.Sin(_bellP[i3 + 1]) * MathF.Exp(-t / 0.8f)
                     + 0.15f * MathF.Sin(_bellP[i3 + 2]) * MathF.Exp(-t / 0.4f)) * env * MusicParams.BellVolume;
            bl += s * (0.5f - _bellPan[b] * 0.5f);
            br += s * (0.5f + _bellPan[b] * 0.5f);
        }
        var alarm = AlarmSample();

        // Space: reverb of a send, plus a ping-pong echo of the bells.
        Reverb((dl + dr) * 0.25f + (bl + br) * 0.9f + alarm * MusicParams.AlarmWet, out var rl, out var rr);
        EchoStep(bl + br + (dl + dr) * 0.05f, out var el, out var er);
        var ml = dl + bl * 0.6f + (rl + el * 0.6f) * _wetAmt * 2.2f;
        var mr = dr + br * 0.6f + (rr + er * 0.6f) * _wetAmt * 2.2f;

        var gain = MusicParams.MasterVolume * _musicGain * _duck;
        outL = MathF.Tanh(ml * gain + alarm * MusicParams.AlarmVolume);
        outR = MathF.Tanh(mr * gain + alarm * MusicParams.AlarmVolume);
    }

    /// <summary>The alarm: rising whoops (a sine with a little third and fifth harmonic), soft in and out. Time runs per sample.</summary>
    private float AlarmSample()
    {
        if (_alarmT < 0f)
            return 0f;
        _alarmT += Dt1;
        if (_alarmT >= MusicParams.AlarmSeconds)
            return 0f;
        var period = MusicParams.AlarmWhoopSeconds + MusicParams.AlarmGapSeconds;
        var local = _alarmT - (int)(_alarmT / period) * period;
        if (local >= MusicParams.AlarmWhoopSeconds)
            return 0f;
        var f = MusicParams.AlarmLowHz * MathF.Pow(2f, MusicParams.AlarmOctaves * local / MusicParams.AlarmWhoopSeconds);
        _alarmPhase = Wrap(_alarmPhase + TwoPi * f * Dt1);
        var attack = MathF.Sin(MathF.Min(1f, local / MusicParams.AlarmAttackSeconds) * MathF.PI * 0.5f);
        var release = MathF.Sin(MathF.Min(1f, (MusicParams.AlarmWhoopSeconds - local) / MusicParams.AlarmReleaseSeconds) * MathF.PI * 0.5f);
        var s = MathF.Sin(_alarmPhase) + 0.22f * MathF.Sin(3f * _alarmPhase) + 0.08f * MathF.Sin(5f * _alarmPhase);
        return s * attack * release * 0.7f;
    }

    private void Reverb(float input, out float l, out float r)
    {
        float sumL = 0f, sumR = 0f;
        var damp = MusicParams.CombDamping;
        for (var i = 0; i < 4; i++)
        {
            var bl = _combL[i]; var br = _combR[i];
            var pl = _combPosL[i]; var pr = _combPosR[i];
            var yl = bl[pl]; var yr = br[pr];
            _combLpL[i] = yl * (1f - damp) + _combLpL[i] * damp;
            _combLpR[i] = yr * (1f - damp) + _combLpR[i] * damp;
            bl[pl] = input + _combLpL[i] * MusicParams.CombFeedback;
            br[pr] = input + _combLpR[i] * MusicParams.CombFeedback;
            _combPosL[i] = pl + 1 == bl.Length ? 0 : pl + 1;
            _combPosR[i] = pr + 1 == br.Length ? 0 : pr + 1;
            sumL += yl; sumR += yr;
        }
        sumL *= 0.25f; sumR *= 0.25f;
        for (var i = 0; i < 2; i++)
        {
            var al = _apL[i]; var ar = _apR[i];
            var pl = _apPosL[i]; var pr = _apPosR[i];
            var bufL = al[pl]; var bufR = ar[pr];
            var outL = bufL - sumL; var outR = bufR - sumR;
            al[pl] = sumL + bufL * 0.5f; ar[pr] = sumR + bufR * 0.5f;
            sumL = outL; sumR = outR;
            _apPosL[i] = pl + 1 == al.Length ? 0 : pl + 1;
            _apPosR[i] = pr + 1 == ar.Length ? 0 : pr + 1;
        }
        l = sumL; r = sumR;
    }

    private void EchoStep(float input, out float l, out float r)
    {
        var yl = _echoL[_echoPosL]; var yr = _echoR[_echoPosR];
        _echoLpL += (yl - _echoLpL) * 0.3f; _echoLpR += (yr - _echoLpR) * 0.3f;
        _echoL[_echoPosL] = input + _echoLpR * MusicParams.EchoFeedback;
        _echoR[_echoPosR] = _echoLpL * MusicParams.EchoFeedback;
        _echoPosL = _echoPosL + 1 == _echoL.Length ? 0 : _echoPosL + 1;
        _echoPosR = _echoPosR + 1 == _echoR.Length ? 0 : _echoPosR + 1;
        l = yl; r = yr;
    }
}

/// <summary>Dev tool (--render-music): renders each mood and the alarm to WAV files so they can be heard without playing.</summary>
public static class MusicRender
{
    public static void Run(string dir, double seconds = 60, Action<string>? report = null)
    {
        Directory.CreateDirectory(dir);
        report ??= Console.WriteLine;
        Render(dir, "mood_calm", seconds, report, e => e.SetMood(MusicMood.Calm, snap: true));
        Render(dir, "mood_alert_calm", seconds, report, e => e.SetMood(MusicMood.Alert, 0.2f, snap: true));
        Render(dir, "mood_alert_tense", seconds, report, e => e.SetMood(MusicMood.Alert, 0.95f, snap: true));
        Render(dir, "mood_drift", seconds, report, e => e.SetMood(MusicMood.Drift, snap: true));
        Render(dir, "mood_landfall", seconds, report, e => e.SetMood(MusicMood.Landfall, snap: true));
        Render(dir, "mood_bleak", seconds, report, e => e.SetMood(MusicMood.Bleak, snap: true));
        // The alarm over the quiet mood: 4 s of calm, the alarm, then the music crossfading to the alert mood.
        Render(dir, "alarm", 16, report, e => e.SetMood(MusicMood.Calm, snap: true), (e, t) =>
        {
            if (t == 4) { e.TriggerAlarm(); e.SetMood(MusicMood.Alert, 0.5f); }
        });
        // A voyage in 100 s: calm, an alert with its alarm, a drift, the landfall.
        Render(dir, "journey", 100, report, e => e.SetMood(MusicMood.Calm, snap: true), (e, t) =>
        {
            if (t == 20) { e.TriggerAlarm(); e.SetMood(MusicMood.Alert, 0.4f); }
            if (t == 50) e.SetMood(MusicMood.Drift);
            if (t == 70) e.SetMood(MusicMood.Landfall);
        });
    }

    /// <summary>Renders <paramref name="seconds"/> and returns the interleaved samples; <paramref name="everySecond"/> runs before each second.</summary>
    public static float[] RenderSamples(double seconds, Action<MusicEngine> setup, Action<MusicEngine, int>? everySecond = null)
    {
        var engine = new MusicEngine();
        setup(engine);
        var rate = MusicParams.SampleRate;
        var total = (int)(seconds * rate);
        var samples = new float[total * 2];
        var chunk = new float[rate * 2];
        for (var s = 0; s * rate < total; s++)
        {
            everySecond?.Invoke(engine, s);
            var frames = Math.Min(rate, total - s * rate);
            engine.Render(chunk, frames);
            Array.Copy(chunk, 0, samples, s * rate * 2, frames * 2);
        }
        return samples;
    }

    private static void Render(string dir, string name, double seconds, Action<string> report, Action<MusicEngine> setup, Action<MusicEngine, int>? everySecond = null)
    {
        var samples = RenderSamples(seconds, setup, everySecond);
        var peak = 0f; var sumSq = 0.0; var bad = 0; var clipped = 0;
        foreach (var x in samples)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) { bad++; continue; }
            peak = Math.Max(peak, Math.Abs(x));
            sumSq += x * x;
            if (Math.Abs(x) >= 0.999f) clipped++;
        }
        WriteWav(Path.Combine(dir, name + ".wav"), samples, MusicParams.SampleRate);
        report($"{name}.wav  {seconds:0}s  peak {peak:0.000}  rms {Math.Sqrt(sumSq / samples.Length):0.000}  NaN {bad}  clipped {clipped}");
    }

    public static void WriteWav(string path, float[] interleaved, int rate)
    {
        using var w = new BinaryWriter(File.Create(path));
        var bytes = interleaved.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * 4); w.Write((short)4); w.Write((short)16);
        w.Write("data"u8); w.Write(bytes);
        foreach (var x in interleaved)
            w.Write((short)Math.Clamp(x * short.MaxValue, short.MinValue, short.MaxValue));
    }
}
