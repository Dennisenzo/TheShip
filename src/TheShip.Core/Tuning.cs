namespace TheShip.Core;

/// <summary>Every number that tunes the game, in one place.</summary>
public static class Tuning
{
    public const double HoursPerYear = 8760;
    public const double ThawHours = 6;
    /// <summary>Three months awake before a pod can take a person again.</summary>
    public const double MinAwakeHours = 90 * 24;
    public const int MaxAwake = 3;
    public const double AftermathStepHours = 14 * 24;
    /// <summary>Most two-week steps one "advance until something happens" takes.</summary>
    public const int AdvanceMaxSteps = 8;

    public const int ThawAgeMin = 3;
    public const int ThawAgeMax = 5;
    /// <summary>One year of ageing per ten years asleep.</summary>
    public const double SleepAgeing = 0.1;
    public const double WorkingAge = 65;

    public const double StartStores = 1500;     // person-days
    public const int StartParts = 2;
    /// <summary>Run variation: extra clock hours per alert, and the most years an alert moves either way.</summary>
    public const int AlertSlackMin = 1;
    public const int AlertSlackMax = 6;
    public const int AlertYearJitter = 2;
    /// <summary>A failed repair burns a spare part too.</summary>
    public const bool FailedRepairSpendsPart = true;
    /// <summary>Years the arrival slips when the navigation fix was only a patch at the last drift.</summary>
    public const double NavPatchArrivalSlipYears = 9;
    /// <summary>Each sleeper's death chance when a patched pod controller gives way at the last drift.</summary>
    public const double PatchFailDeathChance = 0.15;
    public const double ThawRiskPerWear = 0.012;
    public const double ThawInjury = 25;
    public const double PowerLowDeathChance = 0.10;
    public const int PodsWornAt = 8;
    public const double StoresShortAt = 300;
    /// <summary>Days a person awake lives once the stores are empty (DESIGN.md, "What every decision spends": hunger).</summary>
    public const double StarveDays = 60;
    public const int PartsShortAt = 1;

    /// <summary>A number counts as true within this distance (the date, in years).</summary>
    public const double NumberTolerance = 1;

    // Trust
    public const double LieTrust = 25;
    public const double LieMattersExtra = 10;
    public const double AlteredLogTrust = 30;
    public const double HardTruthTrust = 8;
    public const double PromiseMade = 2;
    public const double PromiseKept = 10;
    public const double PromiseBroken = 15;
    public const double RequestGranted = 6;
    public const double RequestIgnored = 4;
    public const double HurtByOrder = 10;
    public const double Dodge = 3;
    public const double LogGap = 5;
    public const double LockedDoor = 5;
    public const double ConfirmedTruth = 3;
    /// <summary>A sleeper died of a fault left to run out: each awake crew member loses this trust ("You let them die in there"), and the morale of learning a death.</summary>
    public const double LeftBehindTrust = 4;
    /// <summary>The patient of the sleeper illness is treated: a small lift for whoever treated them.</summary>
    public const double IllnessTreatedMorale = 4;
    public const double DoubtThreshold = 55;

    // Repairs
    public const double RepairBaseChance = 0.8;
    public const double RepairPerSkill = 0.12;
    public const double RepairInjuryChance = 0.15;
    public const double RepairInjury = 20;
    public const double DangerDose = 15;
    public const double UnskilledRate = 0.5;
    public const double PatchRate = 2;
    public const double EngineerSpotsAlteredLog = 0.6;

    // Ending (Short haul: 6 crew)
    public const int LandfallAlive = 3;
    public const int LandfallWorkingAge = 2;
}
