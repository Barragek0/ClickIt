namespace ClickIt.Features.Click.Application;

internal sealed class HarvestEncounterUpgrade(ClickItSettings settings, InventoryInteractionPolicy inventory)
{
    private const int LabelUpdateDelayMs = 80;
    private const int InventoryRetryDelayMs = 250;
    private const int WarningHoldMs = 3_000;
    private readonly object _sync = new();
    private readonly Dictionary<long, PendingUpgrade> _pending = [];
    private long _areaHash = long.MinValue;
    private long _inventoryRetryAt;
    private CurrencyWarning? _warning;

    internal enum UpgradeStep { Wait, Open, Identify, Scour, Alchemy }

    private readonly record struct PendingUpgrade(UpgradeStep LastClick, long CheckAfter, int? WisdomBefore, bool StartedNormal);
    private sealed record CurrencyWarning(string Text, long ExpiresAt, bool StartedNormal);

    private bool IsUpgradeEnabled(bool startedNormal)
        => startedNormal ? settings.HarvestAlchNormal.Value : settings.HarvestIdentifyScourAlchMagic.Value;

    internal string? GetWarning()
    {
        CurrencyWarning? warning = Volatile.Read(ref _warning);
        return settings.Enable.Value && settings.ClickHarvest.Value
            && warning != null && IsUpgradeEnabled(warning.StartedNormal) && Environment.TickCount64 < warning.ExpiresAt
            ? warning.Text
            : null;
    }

    // True consumes this tick, including while waiting for the previous click to take effect.
    internal bool TryHandle(LabelOnGround label, Vector2 windowTopLeft, GameController gameController,
        ClickLabelInteractionService interaction, bool allowWhenHotkeyInactive, out bool readyToOpenUpgradedTree)
    {
        // Regular clicks and manual hover clicks run on separate coroutines.
        lock (_sync)
            return TryAdvance(label, windowTopLeft, gameController, interaction, allowWhenHotkeyInactive, out readyToOpenUpgradedTree);
    }

    private bool TryAdvance(LabelOnGround label, Vector2 windowTopLeft, GameController gameController,
        ClickLabelInteractionService interaction, bool allowWhenHotkeyInactive, out bool readyToOpenUpgradedTree)
    {
        readyToOpenUpgradedTree = false;
        if (!settings.ClickHarvest.Value)
            return true;
        if (!settings.HarvestIdentifyScourAlchMagic.Value && !settings.HarvestAlchNormal.Value)
        {
            Volatile.Write(ref _warning, null);
            return false;
        }

        if (!AreaUiSnapshotReader.TryReadCurrentAreaHash(gameController, out long areaHash))
        {
            interaction.LogLabelAction(() => "[Harvest] Waiting for the current area hash");
            return true;
        }

        if (!TryReadTree(label, out Entity? tree, out MonsterRarity rarity))
        {
            interaction.LogLabelAction(() => $"[Harvest] Cannot read a valid tree and rarity: {ClickLabelSelectionMath.DescribeLabel(label)}");
            return true;
        }

        if (areaHash != _areaHash)
        {
            _areaHash = areaHash;
            _pending.Clear();
            _inventoryRetryAt = 0;
            Volatile.Write(ref _warning, null);
        }

        long treeId = tree.Id;
        long now = Environment.TickCount64;
        _pending.TryGetValue(treeId, out PendingUpgrade pending);
        // ExileCore reports Normal encounter rarity as White.
        bool startedNormal = pending.LastClick == UpgradeStep.Wait ? rarity == MonsterRarity.White : pending.StartedNormal;
        if (!IsUpgradeEnabled(startedNormal))
            return false;

        if (now < pending.CheckAfter)
            return true;

        bool? identified = null;
        if (rarity == MonsterRarity.Magic && pending.LastClick is UpgradeStep.Wait or UpgradeStep.Identify)
        {
            bool? entityIdentified = null;
            if (DynamicAccess.TryGetComponent<Mods>(tree, out Mods? mods)
                && DynamicAccess.TryReadBool(mods, static component => component.Identified, out bool isIdentified))
                entityIdentified = isIdentified;

            object? root = LabelActionButtons.ReadVisibleLabelRoot(label);
            int row = LabelActionButtons.DefaultRowIndex;
            bool hasWisdomButton = LabelActionButtons.FindButton(root, row, LabelActionButtons.ScrollOfWisdom, out _) != null;
            bool hasScouringButton = !hasWisdomButton && entityIdentified == null
                && LabelActionButtons.FindButton(root, row, LabelActionButtons.OrbOfScouring, out _) != null;
            identified = ResolveIdentification(entityIdentified, hasWisdomButton, hasScouringButton);
        }

        CurrencyStock? stock = null;
        bool wisdomConsumed = false;
        if (pending.LastClick == UpgradeStep.Identify && rarity == MonsterRarity.Magic && identified != true)
        {
            if (now < _inventoryRetryAt)
                return true;

            stock = ReadCurrency(gameController);
            wisdomConsumed = pending.WisdomBefore is int before
                && stock.Value.WisdomCount == before - 1
                && stock.Value.SawInventoryItems;
            if (!wisdomConsumed)
                _inventoryRetryAt = now + InventoryRetryDelayMs;
        }

        // Cold tooltips cannot disprove that a Wisdom action exists.
        bool startingMagic = rarity == MonsterRarity.Magic && pending.LastClick == UpgradeStep.Wait;
        UpgradeStep step = startingMagic
            ? UpgradeStep.Identify
            : ChooseStep(pending.LastClick, rarity, identified, wisdomConsumed, startedNormal);
        interaction.LogLabelAction(() => $"[Harvest] Tree {treeId} rarity={rarity} identified={identified?.ToString() ?? "unreadable"} previous={pending.LastClick} next={step}");
        if (step == UpgradeStep.Open)
        {
            readyToOpenUpgradedTree = pending.LastClick == UpgradeStep.Alchemy;
            Volatile.Write(ref _warning, null);
            return false;
        }

        if (step == UpgradeStep.Wait || now < _inventoryRetryAt)
            return true;

        stock ??= ReadCurrency(gameController, alchemyOnly: startedNormal);
        string? missing = stock.Value.GetMissingCurrency(step, startingMagic: !startedNormal && pending.LastClick == UpgradeStep.Wait);
        if (missing != null)
        {
            interaction.LogLabelAction(() => $"[Harvest] Tree {treeId} blocked by missing currency: {missing}");
            Volatile.Write(ref _warning, new CurrencyWarning($"Harvest needs {missing}", now + WarningHoldMs, startedNormal));
            _inventoryRetryAt = now + InventoryRetryDelayMs;
            return true;
        }

        Volatile.Write(ref _warning, null);
        ClickLabelInteractionService.LabelActionResult result = SendAction(step);
        if (startingMagic && identified != false && result == ClickLabelInteractionService.LabelActionResult.NoMatch)
        {
            step = UpgradeStep.Scour;
            result = SendAction(step);
        }
        if (result == ClickLabelInteractionService.LabelActionResult.Clicked)
        {
            _pending[treeId] = new PendingUpgrade(step, Environment.TickCount64 + LabelUpdateDelayMs, stock.Value.WisdomCount, startedNormal);
        }

        return true;

        ClickLabelInteractionService.LabelActionResult SendAction(UpgradeStep action)
        {
            string tooltipName = action switch
            {
                UpgradeStep.Identify => LabelActionButtons.ScrollOfWisdom,
                UpgradeStep.Scour => LabelActionButtons.OrbOfScouring,
                UpgradeStep.Alchemy => LabelActionButtons.OrbOfAlchemy,
                _ => throw new InvalidOperationException($"Unexpected Harvest currency step: {action}"),
            };
            return interaction.TryClickLabelAction(label, windowTopLeft, tooltipName,
                allowWhenHotkeyInactive: allowWhenHotkeyInactive,
                canStillClick: () => settings.ClickHarvest.Value && IsUpgradeEnabled(startedNormal)
                    && AreaUiSnapshotReader.TryReadCurrentAreaHash(gameController, out long currentArea)
                    && currentArea == areaHash
                    && TryReadTree(label, out Entity? currentTree, out MonsterRarity currentRarity)
                    && currentTree.Id == treeId && currentRarity == rarity);
        }
    }

    internal static bool? ResolveIdentification(bool? entityIdentified, bool hasWisdomButton, bool hasScouringButton)
        => hasWisdomButton ? false : entityIdentified ?? (hasScouringButton ? true : null);

    internal static UpgradeStep ChooseStep(UpgradeStep lastClick, MonsterRarity rarity, bool? identified, bool wisdomConsumed,
        bool alchNormal = false)
        => (lastClick, rarity) switch
        {
            (UpgradeStep.Wait, MonsterRarity.Magic) => identified == true ? UpgradeStep.Scour : UpgradeStep.Identify,
            (UpgradeStep.Wait, MonsterRarity.White) when alchNormal => UpgradeStep.Alchemy,
            (UpgradeStep.Wait, MonsterRarity.White or MonsterRarity.Rare or MonsterRarity.Unique) => UpgradeStep.Open,
            (UpgradeStep.Identify, MonsterRarity.Magic) when identified == true || wisdomConsumed => UpgradeStep.Scour,
            (UpgradeStep.Identify or UpgradeStep.Scour, MonsterRarity.White) => UpgradeStep.Alchemy,
            (UpgradeStep.Alchemy, MonsterRarity.Rare) => UpgradeStep.Open,
            _ => UpgradeStep.Wait,
        };

    private static bool TryReadTree(LabelOnGround label, [NotNullWhen(true)] out Entity? item, out MonsterRarity rarity)
    {
        rarity = MonsterRarity.Error;
        if (!DynamicAccess.TryGetLabelItemOnGround(label, out item)
            || item == null
            || !DynamicAccess.TryReadBool(item, DynamicAccessProfiles.IsValid, out bool valid) || !valid
            || item.Id == 0
            || !DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path)
            || !MechanicClassifier.IsHarvestPath(path)
            || !DynamicAccess.TryGetDynamicValue(item, DynamicAccessProfiles.Rarity, out object? value)
            || value is not MonsterRarity resolved)
            return false;

        rarity = resolved;
        return true;
    }

    private CurrencyStock ReadCurrency(GameController gameController, bool alchemyOnly = false)
    {
        int wisdom = 0;
        bool hasWisdom = false;
        bool canReadWisdomCount = true;
        bool scour = false;
        bool alchemy = false;
        IReadOnlyList<Entity> items = inventory.ReadCurrentInventoryItems(gameController);
        for (int i = 0; i < items.Count; i++)
        {
            Entity item = items[i];
            if (!DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path))
            {
                canReadWisdomCount = false;
                continue;
            }

            if (path.EndsWith("/Currency/CurrencyUpgradeToRare", StringComparison.OrdinalIgnoreCase))
            {
                alchemy = true;
                if (alchemyOnly)
                    break;
            }
            else if (!alchemyOnly && path.EndsWith("/Currency/CurrencyIdentification", StringComparison.OrdinalIgnoreCase))
            {
                hasWisdom = true;
                if (DynamicAccess.TryGetComponent<StackComponent>(item, out StackComponent? stack)
                    && DynamicAccess.TryGetDynamicValue(stack, static component => component.Size, out object? size)
                    && size is int count && count > 0)
                    wisdom += count;
                else
                    canReadWisdomCount = false;
            }
            else if (!alchemyOnly && path.EndsWith("/Currency/CurrencyConvertToNormal", StringComparison.OrdinalIgnoreCase))
                scour = true;
        }

        return new CurrencyStock(hasWisdom, scour, alchemy, canReadWisdomCount ? wisdom : null, items.Count > 0);
    }

    private readonly record struct CurrencyStock(bool Wisdom, bool Scour, bool Alchemy, int? WisdomCount, bool SawInventoryItems)
    {
        internal string? GetMissingCurrency(UpgradeStep step, bool startingMagic)
        {
            List<string>? missing = null;
            if (startingMagic && !Wisdom)
                (missing ??= []).Add("Scrolls of Wisdom");
            if ((startingMagic || step == UpgradeStep.Scour) && !Scour)
                (missing ??= []).Add("Orbs of Scouring");
            if (!Alchemy)
                (missing ??= []).Add("Orbs of Alchemy");
            return missing == null ? null : string.Join(", ", missing);
        }
    }
}
