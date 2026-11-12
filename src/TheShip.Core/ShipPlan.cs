namespace TheShip.Core;

public enum DoorState { Open, Closed, Locked }

/// <summary>One section of the ship on the plan: its outline, its door onto the corridor, and where people stand in it.</summary>
public sealed class Section
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>The outline in plan units (<see cref="ShipPlan.Width"/> wide).</summary>
    public required (float X, float Y)[] Outline { get; init; }
    /// <summary>Inner lines that give the section its faceted look; drawing only.</summary>
    public (float X, float Y)[][] Mesh { get; init; } = Array.Empty<(float X, float Y)[]>();
    public required (float X, float Y) Label { get; init; }
    /// <summary>Where a fault marker and working crew are drawn.</summary>
    public required (float X, float Y) Spot { get; init; }
    /// <summary>The door as a rectangle on the corridor wall; null for a section with no door of its own.</summary>
    public (float X, float Y, float W, float H)? Door { get; init; }
    public DoorState DoorState { get; set; } = DoorState.Closed;
}

/// <summary>
/// The ship's plan (DESIGN.md, "Direct control": doors; the schematic of step 2). Engine and reactor aft, the helm at
/// the nose, rooms on both sides of one corridor. The numbers are plan units; the game scales them to the window.
/// </summary>
public static class ShipPlan
{
    public const float Width = 896;
    public const float Top = 40;
    public const float Height = 366;

    public static readonly (float X, float Y)[] Hull =
    {
        (40, 80), (770, 80), (830, 120), (878, 200), (886, 235), (878, 270), (830, 350), (770, 390), (40, 390), (10, 340), (10, 130),
    };

    /// <summary>The corridor's two walls and the power bus down its middle (x from, x to, y).</summary>
    public static readonly (float X1, float X2, float Y)[] Corridor = { (226, 760, 220), (226, 760, 250) };
    public static readonly (float X1, float X2, float Y) PowerBus = (226, 760, 235);

    /// <summary>The coolant loop: from the reactor through the coolant bay to both pod banks.</summary>
    public static readonly (float X, float Y)[][] Coolant =
    {
        new (float X, float Y)[] { (190, 160), (294, 160), (294, 92), (457, 92), (457, 100) },
        new (float X, float Y)[] { (190, 310), (232, 310), (232, 378), (457, 378), (457, 370) },
    };

    /// <summary>Which pod bank a crew member's pod is in, and the pod's top-left corner on the plan.</summary>
    public static (string Bank, float X, float Y) Pod(string crewId) => crewId switch
    {
        "mara" => ("podA", 380, 134),
        "idris" => ("podA", 437, 134),
        "tomas" => ("podA", 494, 134),
        "lin" => ("podB", 380, 262),
        "asha" => ("podB", 437, 262),
        _ => ("podB", 494, 262),
    };

    /// <summary>A pod's outline from its top-left corner.</summary>
    public static (float X, float Y)[] PodOutline(float x, float y) => new (float X, float Y)[]
    {
        (x + 8, y), (x + 36, y), (x + 44, y + 12), (x + 44, y + 58), (x + 36, y + 70), (x + 8, y + 70), (x, y + 58), (x, y + 12),
    };

    public static List<Section> Sections() => new()
    {
        new Section
        {
            Id = "engine", Name = "ENGINE", Label = (34, 240), Spot = (60, 200),
            Outline = new (float X, float Y)[] { (96, 120), (96, 350), (46, 372), (22, 330), (22, 140), (46, 98) },
            Mesh = new[] { new (float X, float Y)[] { (22, 140), (96, 235), (22, 330) }, new (float X, float Y)[] { (46, 98), (96, 235), (46, 372) } },
        },
        new Section
        {
            Id = "reactor", Name = "REACTOR", Label = (108, 112), Spot = (161, 300), Door = (222, 221, 8, 28),
            Outline = new (float X, float Y)[] { (106, 90), (216, 90), (226, 100), (226, 370), (216, 380), (106, 380), (96, 370), (96, 100) },
            Mesh = new[]
            {
                new (float X, float Y)[] { (106, 90), (161, 235), (216, 90) }, new (float X, float Y)[] { (106, 380), (161, 235), (216, 380) },
                new (float X, float Y)[] { (96, 100), (161, 235), (226, 100) }, new (float X, float Y)[] { (96, 370), (161, 235), (226, 370) },
                new (float X, float Y)[] { (161, 205), (187, 220), (187, 250), (161, 265), (135, 250), (135, 220), (161, 205) },
            },
        },
        Upper("coolant", "COOLANT BAY", 238, 350),
        Lower("stores", "STORES", 238, 350),
        new Section
        {
            Id = "podA", Name = "POD BANK A", Label = (374, 122), Spot = (540, 196), Door = (443, 216, 28, 8), DoorState = DoorState.Open,
            Outline = new (float X, float Y)[] { (372, 100), (542, 100), (552, 110), (552, 220), (362, 220), (362, 110) },
        },
        new Section
        {
            Id = "podB", Name = "POD BANK B", Label = (374, 356), Spot = (540, 346), Door = (443, 246, 28, 8), DoorState = DoorState.Open,
            Outline = new (float X, float Y)[] { (362, 250), (552, 250), (552, 360), (542, 370), (372, 370), (362, 360) },
        },
        Upper("medical", "MEDICAL", 564, 664),
        Lower("hydro", "HYDROPONICS", 564, 664),
        Upper("core", "CORE", 676, 760, DoorState.Locked),
        Lower("workshop", "WORKSHOP", 676, 760),
        new Section
        {
            Id = "helm", Name = "HELM", Label = (768, 160), Spot = (822, 235), Door = (756, 221, 8, 28),
            Outline = new (float X, float Y)[] { (760, 130), (812, 150), (850, 200), (858, 235), (850, 270), (812, 320), (760, 340) },
            Mesh = new[] { new (float X, float Y)[] { (760, 130), (858, 235), (760, 340) }, new (float X, float Y)[] { (812, 150), (760, 235), (812, 320) } },
        },
    };

    /// <summary>A room above the corridor between two x positions.</summary>
    private static Section Upper(string id, string name, float x1, float x2, DoorState door = DoorState.Closed) => new()
    {
        Id = id, Name = name, Label = (x1 + 12, 122), Spot = ((x1 + x2) / 2 + 24, 150), DoorState = door,
        Door = ((x1 + x2) / 2 - 14, 216, 28, 8),
        Outline = new (float X, float Y)[] { (x1 + 10, 100), (x2 - 10, 100), (x2, 110), (x2, 220), (x1, 220), (x1, 110) },
        Mesh = new[] { new (float X, float Y)[] { (x1, 220), ((x1 + x2) / 2, 100), (x2, 220) } },
    };

    /// <summary>A room below the corridor between two x positions.</summary>
    private static Section Lower(string id, string name, float x1, float x2, DoorState door = DoorState.Closed) => new()
    {
        Id = id, Name = name, Label = (x1 + 12, 356), Spot = ((x1 + x2) / 2 + 24, 310), DoorState = door,
        Door = ((x1 + x2) / 2 - 14, 246, 28, 8),
        Outline = new (float X, float Y)[] { (x1, 250), (x2, 250), (x2, 360), (x2 - 10, 370), (x1 + 10, 370), (x1, 360) },
        Mesh = new[] { new (float X, float Y)[] { (x1, 250), ((x1 + x2) / 2, 370), (x2, 250) } },
    };
}
