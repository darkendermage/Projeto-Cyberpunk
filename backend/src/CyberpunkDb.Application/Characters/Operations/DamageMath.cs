namespace CyberpunkDb.Application.Characters.Operations;

// CP:R p.186, shared by the sheet and the combat table: subtract the location's SP (half, rounded up, vs melee
// and Martial Arts, p.176/178), then double head hits, or triple with Cracked Skull (p.170, p.188).
public static class DamageMath
{
    public const int HeadMultiplier = 2;
    public const int CrackedSkullMultiplier = 3;

    // 0 when the damage ignores armor, half (rounded up) vs melee
    public static int EffectiveSp(int sp, bool halveArmor, bool bypassArmor) =>
        bypassArmor ? 0 : halveArmor ? (sp + 1) / 2 : sp;

    public static int Through(int amount, int effectiveSp, bool head, bool crackedSkull)
    {
        var through = Math.Max(0, amount - effectiveSp);
        return head && through > 0 ? through * (crackedSkull ? CrackedSkullMultiplier : HeadMultiplier) : through;
    }
}
