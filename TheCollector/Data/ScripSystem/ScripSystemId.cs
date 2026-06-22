namespace TheCollector.Data.ScripSystem;

public enum ScripSystemId
{
    Normal,
    Firmament,
    // Skybuilders' Resource Inspection (Flotpassant). Shares the Firmament currency, cap,
    // purchase list, planner and shop — only the turn-in step differs.
    Inspection,
}

public static class ScripSystemIds
{
    // Inspection rides on the Firmament economy (same Skybuilders' Scrip, goal, planner, shop),
    // so every Firmament-goal/UI branch must treat it the same.
    public static bool IsFirmamentLike(this ScripSystemId id) =>
        id is ScripSystemId.Firmament or ScripSystemId.Inspection;
}
