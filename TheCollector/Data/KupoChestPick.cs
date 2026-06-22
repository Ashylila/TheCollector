namespace TheCollector.Data;

// Which chest to scratch on a Kupo of Fortune card.
public enum KupoChestPick
{
    // The lone left hexagon — can only win 2nd through 4th prize.
    Left,

    // A random one of the three right hexagons — can win any of the 5 prizes.
    RandomRight,
}
