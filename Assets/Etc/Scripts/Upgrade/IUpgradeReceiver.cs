using System.Collections.Generic;

public interface IUpgradeReceiver
{
    bool CanApplyUpgrade(UpgradeDefinition upgrade);
    void ApplyUpgrade(UpgradeDefinition upgrade);
}

public static class UpgradeReceiverExtensions
{
    public static void ApplyUpgrades(this IUpgradeReceiver receiver, IReadOnlyList<UpgradeDefinition> upgrades)
    {
        if (receiver == null || upgrades == null)
        {
            return;
        }

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeDefinition upgrade = upgrades[i];

            if (upgrade == null)
            {
                continue;
            }

            if (!receiver.CanApplyUpgrade(upgrade))
            {
                continue;
            }

            receiver.ApplyUpgrade(upgrade);
        }
    }
}