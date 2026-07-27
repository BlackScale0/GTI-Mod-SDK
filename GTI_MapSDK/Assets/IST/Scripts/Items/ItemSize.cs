public enum ItemSize
{
    Pocket, // Fits in hotbar slot. Up to 4 held simultaneously. Invisible when pocketed.
    Carry,  // Two-handed. Visible to all. Slows movement. Hold LMB or item drops.
    Haul,   // Requires two players holding LMB simultaneously. Very slow. No jumping.
}