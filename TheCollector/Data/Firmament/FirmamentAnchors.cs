using System.Numerics;

namespace TheCollector.Data.Firmament;

public static class FirmamentAnchors
{
    public const uint AppraiserTalkId = 721545;

    public const uint ExchangeTalkId = 721546;

    public const uint ScripItemId = 28063;

    // Lizbeth runs the Kupo of Fortune lottery in The Firmament. Several ENpcResident rows
    // share the name "Lizbeth"; the catalog keeps only the placement that resolves to the
    // Firmament territory, so listing every candidate here stays language-independent.
    public static readonly uint[] LizbethNpcIds = { 1031679, 1031692, 1035535 };

    // The confirmed Firmament Lizbeth (verified in-game via /xldev: targeting her reports
    // base id 1031692 at this world position). Pinned as a guaranteed anchor so the catalog
    // always has a working id + spot even if the Level-sheet scan or live object lookup
    // comes up empty.
    public const uint LizbethNpcId = 1031692;
    public static readonly Vector3 LizbethPosition = new(52.780884f, -16.000002f, 170.61108f);
}
