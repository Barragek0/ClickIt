namespace ClickIt.Features.Skills;

internal sealed class VaalMoltenShellAutomationService(ClickItSettings settings, GameController gameController, InputHandler inputHandler)
{
    internal void Update()
    {
        if (AreaClassification.IsSafeArea(gameController) != false
            || !inputHandler.CanPerformBackgroundAutomation(gameController) || Mouse.DisableNativeInput)
            return;

        try
        {
            bool always = settings.VaalMoltenShellAlways.Value;
            if (!always && !settings.VaalMoltenShellLifeEnabled.Value && !settings.VaalMoltenShellEnergyShieldEnabled.Value
                && !settings.VaalMoltenShellNormalEnabled.Value && !settings.VaalMoltenShellMagicEnabled.Value
                && !settings.VaalMoltenShellRareEnabled.Value && !settings.VaalMoltenShellUniqueEnabled.Value)
                return;

            object? skillBar = gameController.IngameState?.IngameUi?.SkillBar;
            if (!SkillBarBindingResolver.TryFindReadySkillBinding(skillBar, "vaal_molten_shell", out SkillBinding binding, out _)
                || !SkillBarBindingResolver.TryResolveSkillRuntimeState(binding.Entry, out bool isUsing, out bool? allowed, out bool? usable)
                || isUsing || allowed == false || usable != true || Keyboard.IsKeyDown(binding.BoundKey))
                return;

            if (!always)
            {
                if (settings.VaalMoltenShellLifeEnabled.Value || settings.VaalMoltenShellEnergyShieldEnabled.Value)
                {
                    Entity? player = gameController.Player;
                    if (player == null || !DynamicAccess.TryGetComponent<Life>(player, out Life? life) || life == null)
                        return;
                    if (settings.VaalMoltenShellLifeEnabled.Value && !VaalMoltenShellPolicy.BelowPercent(life.Health.Current, life.Health.Max, settings.VaalMoltenShellLifePercent.Value))
                        return;
                    if (settings.VaalMoltenShellEnergyShieldEnabled.Value && !VaalMoltenShellPolicy.BelowPercent(life.EnergyShield.Current, life.EnergyShield.Max, settings.VaalMoltenShellEnergyShieldPercent.Value))
                        return;
                }

                var rules = LazyModeBlockerService.BuildNearbyMonsterRestrictionSettings(
                    settings.VaalMoltenShellNormalEnabled.Value ? SystemMath.Clamp(settings.VaalMoltenShellNormalCount.Value, 1, 9) : 0, SystemMath.Clamp(settings.VaalMoltenShellNormalDistance.Value, 1, 99),
                    settings.VaalMoltenShellMagicEnabled.Value ? SystemMath.Clamp(settings.VaalMoltenShellMagicCount.Value, 1, 9) : 0, SystemMath.Clamp(settings.VaalMoltenShellMagicDistance.Value, 1, 99),
                    settings.VaalMoltenShellRareEnabled.Value ? SystemMath.Clamp(settings.VaalMoltenShellRareCount.Value, 1, 9) : 0, SystemMath.Clamp(settings.VaalMoltenShellRareDistance.Value, 1, 99),
                    settings.VaalMoltenShellUniqueEnabled.Value ? SystemMath.Clamp(settings.VaalMoltenShellUniqueCount.Value, 1, 9) : 0, SystemMath.Clamp(settings.VaalMoltenShellUniqueDistance.Value, 1, 99));
                if (rules.HasEnabledRestrictions)
                {
                    List<Entity>? entities = gameController.EntityListWrapper?.OnlyValidEntities;
                    if (entities == null || !VaalMoltenShellPolicy.AnyMonsterConditionMet(rules, LazyModeBlockerService.CountNearbyMonsters(entities, rules)))
                        return;
                }
            }

            Keyboard.KeyPress(binding.BoundKey, SkillBarBindingResolver.KeyTapDelayMs);
        }
        catch
        {
            // Unreadable player, skill or monster state must not trigger a cast.
        }
    }
}

internal static class VaalMoltenShellPolicy
{
    internal static bool BelowPercent(int current, int max, int threshold)
        => current >= 0 && max >= 0 && (max == 0 ? current == 0 && threshold > 0 : (double)current * 100 < (double)max * threshold);

    internal static bool AnyMonsterConditionMet(NearbyMonsterRestrictionSettings rules, NearbyMonsterCounts counts)
        => (rules.NormalEnabled && counts.Normal >= rules.NormalThreshold)
            || (rules.MagicEnabled && counts.Magic >= rules.MagicThreshold)
            || (rules.RareEnabled && counts.Rare >= rules.RareThreshold)
            || (rules.UniqueEnabled && counts.Unique >= rules.UniqueThreshold);
}
