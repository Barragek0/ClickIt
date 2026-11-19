namespace ClickIt.Features.Click.Application;

internal sealed class HarvestEncounterUpgrade(ClickItSettings settings, InventoryInteractionPolicy inventory)
{
    private const int LabelUpdateDelayMs = 80;
    private const int InventoryRetryDelayMs = 250;
    private const int WarningHoldMs = 3_000;
    private readonly object _sync = new();
    private readonly Dictionary<long, PendingUpgrade> _pending = [];
    private readonly Dictionary<long, PendingReroll> _rerolls = [];
    private long _areaHash = long.MinValue;
    private long _inventoryRetryAt;
    private CurrencyWarning? _warning;

    internal enum UpgradeStep { Wait, Open, Identify, Scour, Alchemy, Chaos }

    private readonly record struct PendingUpgrade(UpgradeStep LastClick, long CheckAfter, int? WisdomBefore, bool StartedNormal);
    private readonly record struct PendingReroll(UpgradeStep LastClick, long CheckAfter, string? PreviousText, int? CurrencyBefore);
    private sealed record CurrencyWarning(string Text, long ExpiresAt, bool StartedNormal, bool Reroll = false);

    private bool IsUpgradeEnabled(bool startedNormal)
        => settings.HarvestRerollTargetMods.Value
            || (startedNormal ? settings.HarvestAlchNormal.Value : settings.HarvestIdentifyScourAlchMagic.Value);

    internal string? GetWarning()
    {
        CurrencyWarning? warning = Volatile.Read(ref _warning);
        return settings.Enable.Value && settings.ClickHarvest.Value
            && warning != null && (warning.Reroll ? settings.HarvestRerollTargetMods.Value : IsUpgradeEnabled(warning.StartedNormal))
            && Environment.TickCount64 < warning.ExpiresAt
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
        if (!settings.HarvestRerollTargetMods.Value && _rerolls.Count != 0)
            foreach (long id in _rerolls.Where(entry => entry.Value.LastClick != UpgradeStep.Identify).Select(entry => entry.Key).ToArray())
                _rerolls.Remove(id);
        if (!settings.HarvestIdentifyScourAlchMagic.Value && !settings.HarvestAlchNormal.Value && !settings.HarvestRerollTargetMods.Value)
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
            _rerolls.Clear();
            _inventoryRetryAt = 0;
            Volatile.Write(ref _warning, null);
        }

        long treeId = tree.Id;
        long now = Environment.TickCount64;
        _pending.TryGetValue(treeId, out PendingUpgrade pending);
        if (_rerolls.TryGetValue(treeId, out PendingReroll currentReroll)
            && ShouldResetRerollForRarity(currentReroll.LastClick, rarity))
        {
            _rerolls.Remove(treeId);
            _pending.Remove(treeId);
            pending = default;
            interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: cleared stale {currentReroll.LastClick} reroll state for {rarity} rarity");
        }
        interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: current rarity={rarity}, upgrade={pending.LastClick}, reroll={(_rerolls.TryGetValue(treeId, out PendingReroll state) ? state.LastClick : UpgradeStep.Wait)}");
        if (settings.HarvestRerollTargetMods.Value &&
            (rarity == MonsterRarity.Rare || _rerolls.ContainsKey(treeId)))
        {
            if (now < pending.CheckAfter)
                return true;
            return TryReroll(label, windowTopLeft, gameController, interaction, allowWhenHotkeyInactive,
                areaHash, treeId, rarity, pending.LastClick == UpgradeStep.Alchemy, out readyToOpenUpgradedTree);
        }
        if (!settings.HarvestRerollTargetMods.Value && rarity == MonsterRarity.Rare)
        {
            _rerolls.TryGetValue(treeId, out PendingReroll identification);
            if (now < identification.CheckAfter || TryIdentifyRare(label, windowTopLeft, gameController, interaction,
                    allowWhenHotkeyInactive, areaHash, treeId, identification, ReadModifierText(label)))
                return true;
            _rerolls.Remove(treeId);
            readyToOpenUpgradedTree = identification.LastClick == UpgradeStep.Identify || pending.LastClick == UpgradeStep.Alchemy;
            return false;
        }
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

        // An explicitly unidentified tree needs Wisdom; otherwise try Scouring
        // first and fall back to Wisdom if the action row proves Scouring absent.
        bool startingMagic = rarity == MonsterRarity.Magic && pending.LastClick == UpgradeStep.Wait;
        UpgradeStep step = ChooseStep(pending.LastClick, rarity, identified, wisdomConsumed, startedNormal);
        interaction.LogLabelAction(() => $"[Harvest] Tree {treeId} rarity={rarity} identified={identified?.ToString() ?? "unreadable"} previous={pending.LastClick} next={step}");
        if (step == UpgradeStep.Open)
        {
            if (settings.HarvestRerollTargetMods.Value)
                return true;
            readyToOpenUpgradedTree = pending.LastClick == UpgradeStep.Alchemy;
            Volatile.Write(ref _warning, null);
            return false;
        }

        if (step == UpgradeStep.Wait || now < _inventoryRetryAt)
            return true;

        stock ??= ReadCurrency(gameController, alchemyOnly: startedNormal);
        string? missing = GetMissingUpgradeCurrency(step, stock.Value.Wisdom, stock.Value.Scour, stock.Value.Alchemy);
        if (missing != null)
        {
            interaction.LogLabelAction(() => $"[Harvest] Tree {treeId} blocked by missing currency: {missing}");
            Volatile.Write(ref _warning, new CurrencyWarning($"Harvest needs {missing}", now + WarningHoldMs, startedNormal));
            _inventoryRetryAt = now + InventoryRetryDelayMs;
            return true;
        }

        Volatile.Write(ref _warning, null);
        bool triedScourFirst = startingMagic && step == UpgradeStep.Scour;
        ClickLabelInteractionService.LabelActionResult result = SendAction(step);
        if (startingMagic && step == UpgradeStep.Scour && result == ClickLabelInteractionService.LabelActionResult.NoMatch)
        {
            step = UpgradeStep.Identify;
            missing = GetMissingUpgradeCurrency(step, stock.Value.Wisdom, stock.Value.Scour, stock.Value.Alchemy);
            if (missing != null)
            {
                Volatile.Write(ref _warning, new CurrencyWarning($"Harvest needs {missing}", now + WarningHoldMs, startedNormal));
                _inventoryRetryAt = now + InventoryRetryDelayMs;
                return true;
            }
            result = SendAction(step);
        }
        if (startingMagic && !triedScourFirst && step == UpgradeStep.Identify && identified != false && result == ClickLabelInteractionService.LabelActionResult.NoMatch)
        {
            step = UpgradeStep.Scour;
            missing = GetMissingUpgradeCurrency(step, stock.Value.Wisdom, stock.Value.Scour, stock.Value.Alchemy);
            if (missing != null)
            {
                Volatile.Write(ref _warning, new CurrencyWarning($"Harvest needs {missing}", now + WarningHoldMs, startedNormal));
                _inventoryRetryAt = now + InventoryRetryDelayMs;
                return true;
            }
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

    private bool TryReroll(LabelOnGround label, Vector2 windowTopLeft, GameController gameController,
        ClickLabelInteractionService interaction, bool allowWhenHotkeyInactive, long areaHash, long treeId,
        MonsterRarity rarity, bool wasUpgraded, out bool readyToOpen)
    {
        readyToOpen = false;
        long now = Environment.TickCount64;
        _rerolls.TryGetValue(treeId, out PendingReroll pending);
        if (now < pending.CheckAfter)
            return true;

        string? text = rarity == MonsterRarity.Rare ? ReadModifierText(label) : null;
        if (pending.LastClick == UpgradeStep.Scour)
        {
            if (rarity != MonsterRarity.White)
                return true;
        }
        else if (pending.LastClick == UpgradeStep.Alchemy)
        {
            if (rarity != MonsterRarity.Rare || string.IsNullOrWhiteSpace(text))
                return true;
        }
        else if (pending.LastClick == UpgradeStep.Chaos)
        {
            if (rarity != MonsterRarity.Rare || string.IsNullOrWhiteSpace(text))
                return true;
            if (string.Equals(text, pending.PreviousText, StringComparison.Ordinal))
            {
                CurrencyStock current = ReadCurrency(gameController);
                if (!ChaosRerollConfirmed(pending.PreviousText, text, pending.CurrencyBefore, current.ChaosCount, current.SawInventoryItems))
                    return true;
            }
        }

        if (rarity == MonsterRarity.Rare)
        {
            if (TryIdentifyRare(label, windowTopLeft, gameController, interaction, allowWhenHotkeyInactive, areaHash, treeId, pending, text))
                return true;
            if (string.IsNullOrWhiteSpace(text) || text.Contains("Unidentified", StringComparison.OrdinalIgnoreCase))
                return true;
            if (MatchesTarget(text, settings.HarvestTargetStackedDecks.Value, settings.HarvestMinStackedDecks.Value,
                    settings.HarvestTargetThreads.Value, settings.HarvestMinThreads.Value,
                    settings.HarvestTargetCurrencyItems.Value, settings.HarvestMinCurrencyItems.Value, settings.HarvestTargetUniqueItems.Value))
            {
                _rerolls.Remove(treeId);
                Volatile.Write(ref _warning, null);
                readyToOpen = wasUpgraded || pending.LastClick != UpgradeStep.Wait;
                return false;
            }
        }
        else if (rarity != MonsterRarity.White || pending.LastClick != UpgradeStep.Scour)
            return true;

        if (now < _inventoryRetryAt)
            return true;
        bool useChaos = settings.HarvestRerollMethod.Value == 0;
        UpgradeStep next = rarity == MonsterRarity.White ? UpgradeStep.Alchemy : useChaos ? UpgradeStep.Chaos : UpgradeStep.Scour;
        CurrencyStock stock = ReadCurrency(gameController);
        string? missing = next == UpgradeStep.Chaos ? (!stock.Chaos ? "Chaos Orbs" : null)
            : !stock.Scour && next == UpgradeStep.Scour ? "Orbs of Scouring"
            : !stock.Alchemy ? "Orbs of Alchemy" : null;
        if (missing != null)
        {
            Volatile.Write(ref _warning, new CurrencyWarning($"Harvest needs {missing}", now + WarningHoldMs, false, true));
            _inventoryRetryAt = now + InventoryRetryDelayMs;
            return true;
        }
        Volatile.Write(ref _warning, null);
        string name = next == UpgradeStep.Chaos ? LabelActionButtons.ChaosOrb
            : next == UpgradeStep.Scour ? LabelActionButtons.OrbOfScouring : LabelActionButtons.OrbOfAlchemy;
        var result = interaction.TryClickLabelAction(label, windowTopLeft, name, allowWhenHotkeyInactive: allowWhenHotkeyInactive,
            canStillClick: () => settings.ClickHarvest.Value && settings.HarvestRerollTargetMods.Value
                && AreaUiSnapshotReader.TryReadCurrentAreaHash(gameController, out long currentArea) && currentArea == areaHash
                && TryReadTree(label, out Entity? currentTree, out MonsterRarity currentRarity)
                && currentTree.Id == treeId && currentRarity == rarity
                && (rarity != MonsterRarity.Rare || (string.Equals(ReadModifierText(label), text, StringComparison.Ordinal)
                    && (settings.HarvestRerollMethod.Value == 0) == useChaos
                    && !MatchesTarget(text!, settings.HarvestTargetStackedDecks.Value, settings.HarvestMinStackedDecks.Value,
                        settings.HarvestTargetThreads.Value, settings.HarvestMinThreads.Value,
                        settings.HarvestTargetCurrencyItems.Value, settings.HarvestMinCurrencyItems.Value, settings.HarvestTargetUniqueItems.Value))));
        if (result == ClickLabelInteractionService.LabelActionResult.Clicked)
            _rerolls[treeId] = new PendingReroll(next, Environment.TickCount64 + LabelUpdateDelayMs, text, stock.ChaosCount);
        return true;
    }

    private bool TryIdentifyRare(LabelOnGround label, Vector2 windowTopLeft, GameController gameController,
        ClickLabelInteractionService interaction, bool allowWhenHotkeyInactive, long areaHash, long treeId,
        PendingReroll pending, string? text)
    {
        bool? identified = null;
        if (TryReadTree(label, out Entity? tree, out _) && DynamicAccess.TryGetComponent<Mods>(tree, out Mods? mods)
            && DynamicAccess.TryReadBool(mods, static component => component.Identified, out bool value))
            identified = value;
        bool saysUnidentified = text?.Contains("Unidentified", StringComparison.OrdinalIgnoreCase) == true;
        bool readableMods = !string.IsNullOrWhiteSpace(text) && !saysUnidentified;
        if (saysUnidentified)
            identified = false;
        if (pending.LastClick == UpgradeStep.Identify)
        {
            CurrencyStock current = ReadCurrency(gameController);
            bool consumed = current.SawInventoryItems && pending.CurrencyBefore is int before
                && current.WisdomCount is int after && after < before;
            bool textUpdated = readableMods && !string.Equals(pending.PreviousText, text, StringComparison.Ordinal);
            if (!RareIdentificationConfirmed(identified, textUpdated, consumed))
            {
                interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: waiting for rare identification to update");
                return true;
            }
            return false;
        }
        if (!NeedsRareIdentification(identified, readableMods))
            return false;
        long now = Environment.TickCount64;
        if (now < _inventoryRetryAt)
            return true;
        CurrencyStock wisdom = ReadCurrency(gameController);
        if (!wisdom.Wisdom)
        {
            bool normalOnly = settings.HarvestAlchNormal.Value && !settings.HarvestIdentifyScourAlchMagic.Value;
            Volatile.Write(ref _warning, new CurrencyWarning("Harvest needs Scrolls of Wisdom", now + WarningHoldMs, normalOnly, settings.HarvestRerollTargetMods.Value));
            _inventoryRetryAt = now + InventoryRetryDelayMs;
            return true;
        }
        interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: checking Scroll of Wisdom for rare tree");
        var result = interaction.TryClickLabelAction(label, windowTopLeft, LabelActionButtons.ScrollOfWisdom,
            allowWhenHotkeyInactive: allowWhenHotkeyInactive,
            canStillClick: () => settings.ClickHarvest.Value && (IsUpgradeEnabled(false) || settings.HarvestAlchNormal.Value)
                && AreaUiSnapshotReader.TryReadCurrentAreaHash(gameController, out long currentArea) && currentArea == areaHash
                && TryReadTree(label, out Entity? currentTree, out MonsterRarity currentRarity)
                && currentTree.Id == treeId && currentRarity == MonsterRarity.Rare);
        if (result == ClickLabelInteractionService.LabelActionResult.Clicked)
        {
            _rerolls[treeId] = new PendingReroll(UpgradeStep.Identify, Environment.TickCount64 + LabelUpdateDelayMs, text, wisdom.WisdomCount);
            return true;
        }
        return result != ClickLabelInteractionService.LabelActionResult.NoMatch || !readableMods;
    }

    internal static bool ChaosRerollConfirmed(string? previous, string? current, int? before, int? after, bool inventoryReadable)
        => !string.IsNullOrWhiteSpace(current) &&
            (!string.Equals(previous, current, StringComparison.Ordinal)
                || (inventoryReadable && before.HasValue && after.HasValue && after.Value < before.Value));

    internal static bool NeedsRareIdentification(bool? identified, bool readableMods)
        => identified == false || (identified == null && !readableMods);

    internal static bool RareIdentificationConfirmed(bool? identified, bool textUpdated, bool wisdomConsumed)
        => identified == true || textUpdated || wisdomConsumed;

    internal static bool ShouldResetRerollForRarity(UpgradeStep lastClick, MonsterRarity rarity)
        => rarity == MonsterRarity.Magic || (rarity == MonsterRarity.White && lastClick is not (UpgradeStep.Scour or UpgradeStep.Alchemy));

    private static string? ReadModifierText(LabelOnGround label)
    {
        object? root = LabelActionButtons.ReadVisibleLabelRoot(label);
        return DynamicAccess.TryGetChildAtIndex(root, 0, out object? first)
            && DynamicAccess.TryGetChildAtIndex(first, 1, out object? second)
            && DynamicAccess.TryGetChildAtIndex(second, 3, out object? third)
            && DynamicAccess.TryGetChildAtIndex(third, 0, out object? element)
            && DynamicAccess.TryReadString(element, static value => value.TextNoTags, out string text) ? text : null;
    }

    internal static bool MatchesTarget(string text, bool decks, int minDecks, bool threads, int minThreads,
        bool currency = false, int minCurrency = 1, bool unique = false)
    {
        if (!decks && !threads && !currency && !unique)
            decks = true;
        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = System.Text.RegularExpressions.Regex.Match(line,
                @"^Tier\s*3\s+Sacred\s+Fauna\s+drop\s+(\d+|an)\s+additional\s+(Stacked\s+Decks?|Threads?|Currency\s+Items?|Unique\s+Items?)\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
            if (!match.Success)
                continue;
            int count = string.Equals(match.Groups[1].Value, "an", StringComparison.OrdinalIgnoreCase)
                ? 1 : int.TryParse(match.Groups[1].Value, out int parsed) ? parsed : 0;
            string target = match.Groups[2].Value;
            if (target.StartsWith("Stacked", StringComparison.OrdinalIgnoreCase) ? decks && count >= Math.Clamp(minDecks, 1, 3)
                : target.StartsWith("Thread", StringComparison.OrdinalIgnoreCase) ? threads && count >= Math.Clamp(minThreads, 1, 3)
                : target.StartsWith("Currency", StringComparison.OrdinalIgnoreCase) ? currency && count >= Math.Clamp(minCurrency, 1, 4)
                : unique && count >= 1)
                return true;
        }
        return false;
    }

    internal static bool? ResolveIdentification(bool? entityIdentified, bool hasWisdomButton, bool hasScouringButton)
        => hasWisdomButton ? false : entityIdentified ?? (hasScouringButton ? true : null);

    internal static UpgradeStep ChooseStep(UpgradeStep lastClick, MonsterRarity rarity, bool? identified, bool wisdomConsumed,
        bool alchNormal = false)
        => (lastClick, rarity) switch
        {
            (UpgradeStep.Wait, MonsterRarity.Magic) => identified == false ? UpgradeStep.Identify : UpgradeStep.Scour,
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
        bool chaos = false;
        int chaosCount = 0;
        bool canReadChaosCount = true;
        IReadOnlyList<Entity> items = inventory.ReadCurrentInventoryItems(gameController);
        for (int i = 0; i < items.Count; i++)
        {
            Entity item = items[i];
            if (!DynamicAccess.TryReadString(item, DynamicAccessProfiles.Path, out string path))
            {
                canReadWisdomCount = false;
                canReadChaosCount = false;
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
            else if (!alchemyOnly && path.EndsWith("/Currency/CurrencyRerollRare", StringComparison.OrdinalIgnoreCase))
            {
                chaos = true;
                if (DynamicAccess.TryGetComponent<StackComponent>(item, out StackComponent? stack)
                    && DynamicAccess.TryGetDynamicValue(stack, static component => component.Size, out object? size)
                    && size is int count && count > 0)
                    chaosCount += count;
                else
                    canReadChaosCount = false;
            }
        }

        return new CurrencyStock(hasWisdom, scour, alchemy, canReadWisdomCount ? wisdom : null, items.Count > 0,
            chaos, canReadChaosCount ? chaosCount : null);
    }

    internal static string? GetMissingUpgradeCurrency(UpgradeStep step, bool wisdom, bool scour, bool alchemy)
    {
        List<string>? missing = null;
        if (step == UpgradeStep.Identify && !wisdom)
            (missing ??= []).Add("Scrolls of Wisdom");
        if (step == UpgradeStep.Scour && !scour)
            (missing ??= []).Add("Orbs of Scouring");
        if (step is UpgradeStep.Scour or UpgradeStep.Alchemy && !alchemy)
            (missing ??= []).Add("Orbs of Alchemy");
        return missing == null ? null : string.Join(", ", missing);
    }

    private readonly record struct CurrencyStock(bool Wisdom, bool Scour, bool Alchemy, int? WisdomCount, bool SawInventoryItems,
        bool Chaos, int? ChaosCount);
}
