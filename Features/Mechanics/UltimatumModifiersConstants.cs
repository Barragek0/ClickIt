namespace ClickIt.Features.Mechanics
{
    internal static class UltimatumModifiersConstants
    {
        private static readonly KeyValuePair<string, int>[] TieredModifierMaxTiers =
        [
            new("Blistering Cold", 4),
            new("Raging Dead", 4),
            new("Restless Ground", 4),
            new("Blood Altar", 2),
            new("Quicksand", 4),
            new("Razor Dance", 4),
            new("Choking Miasma", 4),
            new("Stormcaller Runes", 4),
            new("Reduced Recovery", 2),
            new("Ruin", 4),
            new("Stalking Ruin", 4)
        ];

        private static readonly string[] TierSuffixes = ["I", "II", "III", "IV"];
        private static readonly HashSet<string> HideBaseNameInStagedTable = new(StringComparer.OrdinalIgnoreCase)
        {
            "Ruin",
            "Stalking Ruin"
        };

        public static readonly string[] AllModifierNames =
        [
            "Resistant Monsters",
            "Shielding Monsters",
            "Impenetrable Monsters",
            "Dexterous Monsters",
            "Unstoppable Monsters",
            "Waning Spirit",
            "Unlucky Criticals",
            "Ailment and Curse Reflection",
            "Lessened Reach",
            "Buffs Expire Faster",
            "Siphoned Charges",
            "Totem of Costly Might",
            "Totem of Costly Potency",

            "Prismatic Monsters",
            "Profane Monsters",
            "Lethal Rare Monsters",
            "Shattered Shield",
            "Overwhelming Monsters",
            "Precise Monsters",
            "Putrid Monsters",
            "Siphoning Monsters",
            "Raging Dead",
            "Blistering Cold",
            "Restless Ground",
            "Blood Altar",
            "Quicksand",
            "Razor Dance",
            "Choking Miasma",

            "Escalating Monster Speed",
            "Escalating Damage Taken",
            "Hindering Flasks",
            "Less Cooldown recovery",
            "Lightning Damage from Mana Costs",
            "Reduced Recovery",
            "Drought",
            "Limited Arena",
            "Treacherous Auras",
            "Occasional Impotence",

            "Stormcaller Runes",
            "Deadly Monsters",
            "Impurity",

            "Ruin",
            "Stalking Ruin"
        ];

        public static readonly string[] AllModifierNamesWithStages = BuildAllModifierNamesWithStages();

        public static readonly IReadOnlyDictionary<string, string> ModifierDescriptions =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ailment and Curse Reflection"] = "Your curses and non-damaging ailments bounce back to you.",
                ["Blistering Cold"] = "Cold bombs pop around you and can chill or freeze at higher tiers.",
                ["Blood Altar"] = "A blood ring around the altar deals heavy physical damage over time.",
                ["Buffs Expire Faster"] = "Your buffs run out very quickly.",
                ["Choking Miasma"] = "A chaos damage cloud follows you and gets faster at higher tiers.",
                ["Deadly Monsters"] = "Every monster hit is a crit, so big damage spikes happen much more often.",
                ["Dexterous Monsters"] = "Monsters get huge evasion and spell suppression.",
                ["Drought"] = "You cannot gain flask charges.",
                ["Escalating Damage Taken"] = "You take more and more damage the longer the round goes.",
                ["Escalating Monster Speed"] = "Monsters speed up over time while they stay alive.",
                ["Hindering Flasks"] = "Using a flask heavily slows you for a short time.",
                ["Impenetrable Monsters"] = "Your elemental penetration does not work on monsters.",
                ["Impurity"] = "You lose maximum resistances.",
                ["Less Cooldown recovery"] = "Your cooldowns recharge much slower.",
                ["Lessened Reach"] = "You lose a lot of area and projectile speed.",
                ["Lightning Damage from Mana Costs"] = "Spending mana shocks you with lightning damage.",
                ["Lethal Rare Monsters"] = "Rare monsters roll extra mods and become much scarier.",
                ["Limited Arena"] = "The fight area becomes much smaller.",
                ["Occasional Impotence"] = "Every few seconds, you and your minions deal no damage.",
                ["Overwhelming Monsters"] = "Monsters ignore your physical damage reduction.",
                ["Precise Monsters"] = "Monster hits cannot be evaded.",
                ["Prismatic Monsters"] = "Monsters gain lots of extra elemental damage.",
                ["Profane Monsters"] = "Monsters gain a big chunk of extra chaos damage.",
                ["Putrid Monsters"] = "Monsters poison and bleed on hit.",
                ["Raging Dead"] = "Skulls chase you and spit fire when they get close.",
                ["Razor Dance"] = "Blades fly at you and stack Corrupted Blood.",
                ["Reduced Recovery"] = "Your life, mana, and energy shield recovery are heavily reduced.",
                ["Resistant Monsters"] = "Monsters have much higher elemental resistances.",
                ["Restless Ground"] = "Unhallowed ground patches spawn, hinder you, and can add Ruin at high tier.",
                ["Ruin"] = "Special monster attacks apply Ruin; at 7 Ruin you instantly fail.",
                ["Shattered Shield"] = "You cannot block.",
                ["Shielding Monsters"] = "Monsters get a high chance to block attacks and spells.",
                ["Siphoned Charges"] = "You lose charges every second while monsters gain charges on hit.",
                ["Siphoning Monsters"] = "Monster hits drain your mana and energy shield.",
                ["Stalking Ruin"] = "An invulnerable shade hunts you and applies Ruin; at 7 Ruin you fail.",
                ["Stormcaller Runes"] = "Standing in runes calls deadly lightning storms.",
                ["Totem of Costly Might"] = "An invulnerable totem speeds everything up and boosts physical damage.",
                ["Totem of Costly Potency"] = "An invulnerable totem speeds everything up and boosts elemental damage.",
                ["Treacherous Auras"] = "Your ally auras also buff enemies.",
                ["Unlucky Criticals"] = "Your crit chance becomes unlucky and your crit damage is reduced.",
                ["Unstoppable Monsters"] = "Monsters cannot be slowed or stunned.",
                ["Waning Spirit"] = "Your non-curse aura effect is cut in half.",
                ["Quicksand"] = "Large quicksand pools keep spawning and slow you hard."
            };

        public static string GetDescription(string modifierName)
        {
            if (ModifierDescriptions.TryGetValue(modifierName, out string? description))
                return description;

            if (TryGetTieredModifierBaseName(modifierName, out string baseModifierName, out string tierSuffix)
                && ModifierDescriptions.TryGetValue(baseModifierName, out string? baseDescription)
                && !string.IsNullOrWhiteSpace(baseDescription))
            {
                return $"{baseDescription} (Tier {tierSuffix})";
            }

            return modifierName;
        }

        private static string[] BuildAllModifierNamesWithStages()
        {
            List<string> ordered = new(AllModifierNames.Length + 64);
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < AllModifierNames.Length; i++)
            {
                string name = AllModifierNames[i];
                if (HideBaseNameInStagedTable.Contains(name))
                    continue;

                if (seen.Add(name))
                    ordered.Add(name);
            }

            for (int i = 0; i < TieredModifierMaxTiers.Length; i++)
            {
                string baseName = TieredModifierMaxTiers[i].Key;
                int maxTier = SystemMath.Clamp(TieredModifierMaxTiers[i].Value, 1, TierSuffixes.Length);
                for (int t = 0; t < maxTier; t++)
                {
                    string stagedName = $"{baseName} {TierSuffixes[t]}";
                    if (seen.Add(stagedName))
                        ordered.Add(stagedName);
                }
            }

            return [.. ordered];
        }

        private static bool TryGetTieredModifierBaseName(string modifierName, out string baseModifierName, out string tierSuffix)
        {
            baseModifierName = string.Empty;
            tierSuffix = string.Empty;

            if (string.IsNullOrWhiteSpace(modifierName))
                return false;

            string trimmed = modifierName.Trim();
            for (int i = 0; i < TierSuffixes.Length; i++)
            {
                string suffix = TierSuffixes[i];
                string marker = " " + suffix;
                if (!trimmed.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
                    continue;

                string candidateBase = trimmed[..^marker.Length].Trim();
                if (candidateBase.Length == 0)
                    return false;

                baseModifierName = candidateBase;
                tierSuffix = suffix;
                return true;
            }

            return false;
        }

        public static Vector4 GetPriorityGradientColor(int index, int totalCount, float alpha = 1f)
        {
            alpha = SystemMath.Clamp(alpha, 0f, 1f);
            if (totalCount <= 1)
                return new Vector4(0.95f, 0.95f, 0.25f, alpha);

            float t = SystemMath.Clamp(index / (float)(totalCount - 1), 0f, 1f);

            float r = 0.25f + (0.70f * t);
            float g = 1.00f - (0.72f * t);
            float b = 0.20f - (0.12f * t);

            return new Vector4(r, g, b, alpha);
        }
    }
}
