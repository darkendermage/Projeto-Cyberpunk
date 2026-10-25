namespace CyberpunkDb.Application.Characters.Operations;

/// <summary>
/// The arithmetic of taking damage (CP:R p.186), shared by the character sheet and the combat table: subtract the
/// location's SP (half of it, rounded up, against melee weapons and Martial Arts, p.176/178), then double what gets
/// through a head hit, or triple it with Cracked Skull (p.170, p.188).
/// </summary>
public static class DamageMath
{
    public const int HeadMultiplier = 2;
    public const int CrackedSkullMultiplier = 3;

    /// <summary>The SP that actually stops the hit: 0 when the damage bypasses armor; half, rounded up, against melee.</summary>
    public static int EffectiveSp(int sp, bool halveArmor, bool bypassArmor) =>
        bypassArmor ? 0 : halveArmor ? (sp + 1) / 2 : sp;

    /// <summary>Damage that reaches the Hit Points.</summary>
    public static int Through(int amount, int effectiveSp, bool head, bool crackedSkull)
    {
        var through = Math.Max(0, amount - effectiveSp);
        return head && through > 0 ? through * (crackedSkull ? CrackedSkullMultiplier : HeadMultiplier) : through;
    }
}
