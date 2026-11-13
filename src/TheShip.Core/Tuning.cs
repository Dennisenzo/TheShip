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
    public const double WorkingAge = 64;

    public const double StartStores = 1500;     // person-days
    public const int StartParts = 2;
    /// <summary>Run variation: extra clock hours per alert, and the most years an alert moves either way.</summary>
    public const int AlertSlackMin = 1;
    public const int AlertSlackMax = 6;
    public const int AlertYearJitter = 2;
    /// <summary>Side faults of alerts 3 and 5: work hours, and the share of the stores a lost blight leaves.</summary>
    public const double BlightHours = 40;
    public const double PowerSagHours = 14;
    public const double BlightStoresLeft = 0.6;
    /// <summary>Most faults one alert holds, returning patches included.</summary>
    public const int MaxFaultsPerAlert = 4;
    /// <summary>A failed repair burns a spare part too.</summary>
    public const bool FailedRepairSpendsPart = true;
    /// <summary>Years the arrival slips when the navigation fix was only a patch at the last drift.</summary>
    public const double NavPatchArrivalSlipYears = 9;
    /// <summary>Each sleeper's death chance when a patched pod controller gives way at the last drift.</summary>
    public const double PatchFailDeathChance = 0.10;
    public const double ThawRiskPerWear = 0.008;
    public const double ThawInjury = 25;
    /// <summary>Share of failed thaws that kill outright instead of injuring.</summary>
    public const double ThawFatalShare = 0.3;
    /// <summary>A sleeper's death chance in a drift is pod wear x years x this.</summary>
    public const double SleepDeathPerWearYear = 0.0001;
    public const double PowerLowDeathChance = 0.05;
    /// <summary>Pod wear a lost failed-pod or pod-controller fault adds.</summary>
    public const int PodLostWear = 6;
    public const int ControllerLostWear = 8;
    public const int PodsWornAt = 8;
    public const double StoresShortAt = 300;
    /// <summary>Days a person awake lives once the stores are empty (DESIGN.md, "What every decision spends": hunger).</summary>
    public const double StarveDays = 60;
    public const int PartsShortAt = 1;

    /// <summary>A number counts as true within this distance (the date, in years).</summary>
    public const double NumberTolerance = 1;

    // Trust
    public const double LieTrust = 22;
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
    /// <summary>A pending question still unanswered after an aftermath beat: they ask again and lose this much trust.</summary>
    public const double QuestionIgnored = 2;
    /// <summary>Going to sleep with a question still pending.</summary>
    public const double QuestionIgnoredAtSleep = 4;
    /// <summary>How often a person repeats a question before they give up and check it themselves.</summary>
    public const int QuestionRepeatsMax = 2;
    public const double LogGap = 5;
    public const double LockedDoor = 5;
    public const double ConfirmedTruth = 3;
    /// <summary>A sleeper died of a fault left to run out: each awake crew member loses this trust ("You let them die in there"), and the morale of learning a death.</summary>
    public const double LeftBehindTrust = 4;
    /// <summary>The patient of the sleeper illness is treated: a small lift for whoever treated them.</summary>
    public const double IllnessTreatedMorale = 4;
    public const double DoubtThreshold = 55;
    /// <summary>
    /// Morale gained on hearing, and believing, the good answer to a question they asked or that matters to them. A true good answer
    /// earns it; a lie borrows it (the hard truth's morale is paid when the lie is found out).
    /// </summary>
    public const double ReliefMorale = 6;

    /// <summary>Work speed follows the team's average morale: 1 + (morale - neutral) / span, within min and max (<see cref="Voyage.MoraleRate"/>).</summary>
    public const double MoraleRateNeutral = 70;
    public const double MoraleRateSpan = 150;
    public const double MoraleRateMin = 0.8;
    public const double MoraleRateMax = 1.15;

    // Repairs
    public const double RepairBaseChance = 0.85;
    public const double RepairPerSkill = 0.12;
    /// <summary>Morale moves the repair chance by (morale - 50) / this.</summary>
    public const double RepairMoraleSpan = 200;
    public const double RepairChanceMin = 0.35;
    public const double RepairChanceMax = 0.97;
    public const double RepairInjuryChance = 0.15;
    public const double RepairInjury = 20;
    public const double DangerDose = 15;
    public const double UnskilledRate = 0.5;
    public const double PatchRate = 2;
    public const double EngineerSpotsAlteredLog = 0.6;

    // Bay takeover (Short haul: 6 crew)
    /// <summary>Awake people in the Against band (trust under 15) it takes, and the Engineering they need between them.</summary>
    public const int BayTakers = 2;
    public const int BayEngineering = 2;
    /// <summary>A kept promise to a taker whose trust is at least this hands the bay back.</summary>
    public const double BayReturnTrust = 30;
    /// <summary>The trust a pod refuser needs before the ship's word on the pods lifts the refusal.</summary>
    public const double PodRefuserListensTrust = 30;

    // Ending (Short haul: 6 crew)
    public const int LandfallAlive = 4;
    public const int LandfallWorkingAge = 3;
}
