namespace ClickIt.Features.Click.Application;

internal sealed class HarvestEncounterUpgrade(ClickItSettings settings, InventoryInteractionPolicy inventory)
{
    private const int LabelUpdateDelayMs = 80;
    private const int ModifierUpdateTimeoutMs = 200;
    private const int InventoryRetryDelayMs = 250;
    private const int WarningHoldMs = 3_000;

    /// <summary>
    /// How far the player's grid position may drift between frames and still count as standing still, in grid units.
    /// Deliberately small: slow drift moves the camera and invalidates the screen rectangle the reroll icon was resolved
    /// from, which is how a reroll click lands on the tree itself.
    /// </summary>
    private const float PlayerStillThreshold = 0.25f;

    private readonly object _sync = new();
    private readonly Dictionary<long, PendingUpgrade> _pending = [];
    private readonly Dictionary<long, PendingReroll> _rerolls = [];
    private long _areaHash = long.MinValue;
    private long _inventoryRetryAt;
    private long _nextModifierDiagnosticAt;
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
        ClickLabelInteractionService interaction, bool allowWhenHotkeyInactive, out bool readyToOpenUpgradedTree, out bool needsApproach)
    {
        // Regular clicks and manual hover clicks run on separate coroutines.
        lock (_sync)
            return TryAdvance(label, windowTopLeft, gameController, interaction, allowWhenHotkeyInactive, out readyToOpenUpgradedTree, out needsApproach);
    }

    private bool TryAdvance(LabelOnGround label, Vector2 windowTopLeft, GameController gameController,
        ClickLabelInteractionService interaction, bool allowWhenHotkeyInactive, out bool readyToOpenUpgradedTree, out bool needsApproach)
    {
        readyToOpenUpgradedTree = false;
        needsApproach = false;
        if (!settings.ClickHarvest.Value)
            return true;
        if (DynamicAccess.TryGetLabelItemOnGround(label, out Entity? harvestItem)
            && DynamicAccess.TryReadString(harvestItem, DynamicAccessProfiles.Path, out string harvestPath)
            && MechanicClassifier.IsHarvestBossSeedPath(harvestPath))
            return !settings.HarvestBossSeeds.Value;
        if (!settings.HarvestRerollTargetMods.Value && _rerolls.Count != 0)
            _rerolls.Clear();
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
        if (settings.HarvestRerollTargetMods.Value)
        {
            if (now < pending.CheckAfter)
                return true;
            return TryReroll(label, windowTopLeft, gameController, interaction, allowWhenHotkeyInactive,
                areaHash, treeId, rarity, pending.LastClick == UpgradeStep.Alchemy, out readyToOpenUpgradedTree, out needsApproach);
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
            identified = ReadIdentificationState(label);
            if (identified == null)
            {
                interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: waiting for identification text");
                return true;
            }
        }

        CurrencyStock? stock = null;

        UpgradeStep step = ChooseStep(pending.LastClick, rarity, identified, false, startedNormal);
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
        ClickLabelInteractionService.LabelActionResult result = SendAction(step);
        needsApproach = result == ClickLabelInteractionService.LabelActionResult.Blocked;
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
            NumVector2? playerBaseline = ReadPlayerGridPos(gameController);
            if (playerBaseline == null)
                return ClickLabelInteractionService.LabelActionResult.Unavailable;

            return interaction.TryClickLabelAction(label, windowTopLeft, tooltipName,
                allowWhenHotkeyInactive: allowWhenHotkeyInactive,
                canStillClick: () => settings.ClickHarvest.Value && IsUpgradeEnabled(startedNormal)
                    && IsPlayerStillAt(ReadPlayerGridPos(gameController), playerBaseline)
                    && AreaUiSnapshotReader.TryReadCurrentAreaHash(gameController, out long currentArea)
                    && currentArea == areaHash
                    && TryReadTree(label, out Entity? currentTree, out MonsterRarity currentRarity)
                    && currentTree.Id == treeId && currentRarity == rarity
                    && (rarity != MonsterRarity.Magic || ReadIdentificationState(label) == identified));
        }
    }

    private bool TryReroll(LabelOnGround label, Vector2 windowTopLeft, GameController gameController,
        ClickLabelInteractionService interaction, bool allowWhenHotkeyInactive, long areaHash, long treeId,
        MonsterRarity rarity, bool wasUpgraded, out bool readyToOpen, out bool needsApproach)
    {
        readyToOpen = false;
        needsApproach = false;
        long now = Environment.TickCount64;
        _rerolls.TryGetValue(treeId, out PendingReroll pending);
        if (now < pending.CheckAfter)
            return true;

        bool? identified = rarity == MonsterRarity.White ? true : ReadIdentificationState(label);
        string? text = rarity is MonsterRarity.Rare or MonsterRarity.Magic ? ReadModifierText(label) : null;
        if (rarity is MonsterRarity.Rare or MonsterRarity.Magic && identified == null)
        {
            interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: waiting for identification text");
            if (now >= _nextModifierDiagnosticAt)
            {
                _nextModifierDiagnosticAt = now + 1000;
                interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: identification and modifier element; {DescribeModifierElement(label)}");
            }
            return true;
        }
        if (pending.LastClick == UpgradeStep.Identify && identified != true)
            return true;
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
            {
                interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: waiting for Chaos result; rarity={rarity}, modifier text={(text == null ? "unreadable" : "empty")}, previous={DescribeModifiers(pending.PreviousText)}, chaos before={pending.CurrencyBefore?.ToString() ?? "unreadable"}");
                if (now >= _nextModifierDiagnosticAt)
                {
                    _nextModifierDiagnosticAt = now + 1000;
                    interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: modifier element; {DescribeModifierElement(label)}");
                }
                return true;
            }
            if (!ChaosRerollConfirmed(pending.PreviousText, text, now, pending.CheckAfter))
            {
                interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: waiting for changed Chaos modifiers; previous={DescribeModifiers(pending.PreviousText)}");
                return true;
            }
            if (string.Equals(text, pending.PreviousText, StringComparison.Ordinal))
                interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: modifier update timed out after {ModifierUpdateTimeoutMs}ms; evaluating unchanged text");
        }

        if (identified == true && rarity is MonsterRarity.Rare or MonsterRarity.Magic)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Contains("Unidentified", StringComparison.OrdinalIgnoreCase))
            {
                if (now >= _nextModifierDiagnosticAt)
                {
                    _nextModifierDiagnosticAt = now + 1000;
                    interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: waiting for modifier text; {DescribeModifierElement(label)}");
                }
                return true;
            }
            bool matched = MatchesTarget(text, settings.HarvestTargetStackedDecks.Value, settings.HarvestMinStackedDecks.Value,
                    settings.HarvestTargetThreads.Value, settings.HarvestMinThreads.Value,
                    settings.HarvestTargetCurrencyItems.Value, settings.HarvestMinCurrencyItems.Value, settings.HarvestTargetUniqueItems.Value, settings.HarvestTargetDuplicatedItems.Value,
                    settings.HarvestTargetLifeforceQuantity.Value, settings.HarvestMinLifeforceQuantity.Value);
            if (pending.LastClick is UpgradeStep.Chaos or UpgradeStep.Alchemy)
                interaction.LogLabelAction(() => $"[Harvest] Tree {treeId}: modifiers={DescribeModifiers(text)}, matched={matched}, targets: decks={settings.HarvestTargetStackedDecks.Value}/{settings.HarvestMinStackedDecks.Value}, threads={settings.HarvestTargetThreads.Value}/{settings.HarvestMinThreads.Value}, currency={settings.HarvestTargetCurrencyItems.Value}/{settings.HarvestMinCurrencyItems.Value}, unique={settings.HarvestTargetUniqueItems.Value}, duplicated={settings.HarvestTargetDuplicatedItems.Value}, lifeforce={settings.HarvestTargetLifeforceQuantity.Value}/{settings.HarvestMinLifeforceQuantity.Value}");
            if (matched)
            {
                _rerolls.Remove(treeId);
                Volatile.Write(ref _warning, null);
                readyToOpen = wasUpgraded || pending.LastClick != UpgradeStep.Wait;
                return false;
            }
        }
        else if (rarity is not (MonsterRarity.White or MonsterRarity.Magic or MonsterRarity.Rare))
            return true;

        if (now < _inventoryRetryAt)
            return true;
        bool useChaos = settings.HarvestRerollMethod.Value == 0;
        UpgradeStep next = ChooseRerollStep(rarity, identified, targetMatched: false, useChaos);
        CurrencyStock stock = ReadCurrency(gameController);
        string? missing = GetMissingRerollCurrency(next, stock.Wisdom, stock.Scour, stock.Alchemy, stock.Chaos);
        if (missing != null)
        {
            Volatile.Write(ref _warning, new CurrencyWarning($"Harvest needs {missing}", now + WarningHoldMs, false, true));
            _inventoryRetryAt = now + InventoryRetryDelayMs;
            return true;
        }
        Volatile.Write(ref _warning, null);

        // Sample where the character stands before the dispatcher resolves the icon, then require it to still be there
        // when the click is actually sent. A walk that starts mid-resolve shifts the icon off the cursor onto the tree.
        NumVector2? playerBaseline = ReadPlayerGridPos(gameController);
        if (playerBaseline == null)
            return true;

        string name = next == UpgradeStep.Identify ? LabelActionButtons.ScrollOfWisdom
            : next == UpgradeStep.Chaos ? LabelActionButtons.ChaosOrb
            : next == UpgradeStep.Scour ? LabelActionButtons.OrbOfScouring : LabelActionButtons.OrbOfAlchemy;
        var result = interaction.TryClickLabelAction(label, windowTopLeft, name, allowWhenHotkeyInactive: allowWhenHotkeyInactive,
            canStillClick: () => settings.ClickHarvest.Value && settings.HarvestRerollTargetMods.Value
                && IsPlayerStillAt(ReadPlayerGridPos(gameController), playerBaseline)
                && AreaUiSnapshotReader.TryReadCurrentAreaHash(gameController, out long currentArea) && currentArea == areaHash
                && TryReadTree(label, out Entity? currentTree, out MonsterRarity currentRarity)
                && currentTree.Id == treeId && currentRarity == rarity
                && (rarity == MonsterRarity.White || ReadIdentificationState(label) == identified)
                && (rarity != MonsterRarity.Rare || next is not (UpgradeStep.Chaos or UpgradeStep.Scour)
                    || (settings.HarvestRerollMethod.Value == 0) == useChaos)
                && (next is not (UpgradeStep.Chaos or UpgradeStep.Scour) || (string.Equals(ReadModifierText(label), text, StringComparison.Ordinal)
                    && !MatchesTarget(text!, settings.HarvestTargetStackedDecks.Value, settings.HarvestMinStackedDecks.Value,
                        settings.HarvestTargetThreads.Value, settings.HarvestMinThreads.Value,
                        settings.HarvestTargetCurrencyItems.Value, settings.HarvestMinCurrencyItems.Value, settings.HarvestTargetUniqueItems.Value, settings.HarvestTargetDuplicatedItems.Value,
                        settings.HarvestTargetLifeforceQuantity.Value, settings.HarvestMinLifeforceQuantity.Value))));
        needsApproach = result == ClickLabelInteractionService.LabelActionResult.Blocked;
        if (result == ClickLabelInteractionService.LabelActionResult.Clicked)
            _rerolls[treeId] = new PendingReroll(next, Environment.TickCount64 + LabelUpdateDelayMs, text,
                next == UpgradeStep.Identify ? stock.WisdomCount : stock.ChaosCount);
        return true;
    }

    internal static bool ChaosRerollConfirmed(string? previous, string? current, long now, long checkAfter)
        => !string.IsNullOrWhiteSpace(current)
            && (!string.Equals(previous, current, StringComparison.Ordinal)
                || now >= checkAfter + ModifierUpdateTimeoutMs - LabelUpdateDelayMs);

    /// <summary>
    /// The player's grid position, or null when it cannot be read. Null is treated as "not still" by
    /// <see cref="IsPlayerStillAt"/>, so an unreadable boundary never grants a click.
    /// </summary>
    private static NumVector2? ReadPlayerGridPos(GameController? gameController)
    {
        try
        {
            if (gameController?.Player == null)
                return null;

            NumVector2 position = gameController.Player.GridPosNum;
            return float.IsFinite(position.X) && float.IsFinite(position.Y) ? position : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the player still stands where <paramref name="baseline"/> was sampled, within
    /// <see cref="PlayerStillThreshold"/>.
    ///
    /// The harvest tree's reroll icons are drawn under the tree and move with the camera, so a click resolved while the
    /// character is walking lands wherever the icon used to be - on the tree itself. This is a pure comparison: the
    /// caller samples the baseline once per tick and re-asks as often as it likes, so the answer cannot drift just
    /// because it was asked again.
    /// </summary>
    internal static bool IsPlayerStillAt(NumVector2? current, NumVector2? baseline)
        => current is { } now
            && baseline is { } then
            && MathF.Abs(now.X - then.X) <= PlayerStillThreshold
            && MathF.Abs(now.Y - then.Y) <= PlayerStillThreshold;

    private static string DescribeModifiers(string? text)
    {
        if (text == null)
            return "<unreadable>";
        string compact = text.Replace('\r', ' ').Replace('\n', ' ');
        return compact.Length <= 240 ? compact : compact[..240] + "...";
    }

    internal static bool ShouldResetRerollForRarity(UpgradeStep lastClick, MonsterRarity rarity)
        => (rarity == MonsterRarity.Magic && lastClick is UpgradeStep.Chaos or UpgradeStep.Alchemy)
            || (rarity == MonsterRarity.White && lastClick is not (UpgradeStep.Scour or UpgradeStep.Alchemy));

    internal static UpgradeStep ChooseRerollStep(MonsterRarity rarity, bool? identified, bool targetMatched, bool useChaos)
    {
        if (rarity == MonsterRarity.White)
            return UpgradeStep.Alchemy;
        if (rarity is not (MonsterRarity.Magic or MonsterRarity.Rare) || identified == null)
            return UpgradeStep.Wait;
        if (identified == false)
            return UpgradeStep.Identify;
        if (targetMatched)
            return UpgradeStep.Open;
        return rarity == MonsterRarity.Magic || !useChaos ? UpgradeStep.Scour : UpgradeStep.Chaos;
    }

    internal static string? GetMissingRerollCurrency(UpgradeStep step, bool wisdom, bool scour, bool alchemy, bool chaos)
        => step switch
        {
            UpgradeStep.Identify when !wisdom => "Scrolls of Wisdom",
            UpgradeStep.Chaos when !chaos => "Chaos Orbs",
            UpgradeStep.Scour when !scour => "Orbs of Scouring",
            UpgradeStep.Alchemy when !alchemy => "Orbs of Alchemy",
            _ => null
        };

    private static bool? ReadIdentificationState(LabelOnGround label)
        => ReadIdentificationStateFromRoot(LabelActionButtons.ReadVisibleLabelRoot(label));

    internal static bool? ReadIdentificationStateFromRoot(object? root)
    {
        if (!DynamicAccess.TryGetChildAtIndex(root, 0, out object? first)
            || !DynamicAccess.TryGetChildAtIndex(first, 1, out object? second)
            || !DynamicAccess.TryGetChildAtIndex(second, 3, out object? element))
            return null;
        if (DynamicAccess.TryGetDynamicValue(element, static value => value.TextNoTags, out object? raw) && raw is string text)
            return !string.Equals(text.Trim(), "Unidentified", StringComparison.OrdinalIgnoreCase);
        // Identified labels can expose modifier text in the child while the
        // parent's identification text becomes null after using Wisdom.
        if (DynamicAccess.TryGetChildAtIndex(element, 0, out object? modifier)
            && DynamicAccess.TryReadString(modifier, static value => value.TextNoTags, out string mods)
            && !mods.Contains("Unidentified", StringComparison.OrdinalIgnoreCase))
            return true;
        return null;
    }

    private static string? ReadModifierText(LabelOnGround label)
    {
        object? root = LabelActionButtons.ReadVisibleLabelRoot(label);
        return DynamicAccess.TryGetChildAtIndex(root, 0, out object? first)
            && DynamicAccess.TryGetChildAtIndex(first, 1, out object? second)
            && DynamicAccess.TryGetChildAtIndex(second, 3, out object? third)
            && DynamicAccess.TryGetChildAtIndex(third, 0, out object? element)
            && DynamicAccess.TryReadString(element, static value => value.TextNoTags, out string text) ? text : null;
    }

    private static string DescribeModifierElement(LabelOnGround label)
    {
        object? current = LabelActionButtons.ReadVisibleLabelRoot(label);
        int[] path = [0, 1, 3, 0];
        string description = current == null ? "label unavailable" : "label";
        foreach (int index in path)
        {
            int count = DynamicAccess.TryReadInt(current, static element => element.ChildCount, out int value) ? value : -1;
            description += $" -> child {index} (count={count})";
            if (!DynamicAccess.TryGetChildAtIndex(current, index, out current) || current == null)
                return description + " unavailable";
            if (index == 3)
            {
                string status = DynamicAccess.TryGetDynamicValue(current, static element => element.TextNoTags, out object? valueText)
                    ? valueText == null ? "<null>" : DescribeModifiers(valueText.ToString()) : "<unreadable>";
                description += $" [identification text={status}]";
            }
        }
        string plain = DynamicAccess.TryReadString(current, static element => element.TextNoTags, out string noTags) ? DescribeModifiers(noTags) : "<unreadable>";
        string raw = DynamicAccess.TryReadString(current, static element => element.Text, out string text) ? DescribeModifiers(text) : "<unreadable>";
        return $"{description}; TextNoTags={plain}; Text={raw}";
    }

    internal static bool MatchesTarget(string text, bool decks, int minDecks, bool threads, int minThreads,
        bool currency = false, int minCurrency = 1, bool unique = false, bool duplicated = false, bool lifeforce = false, int minLifeforce = 100)
    {
        if (!decks && !threads && !currency && !unique && !duplicated && !lifeforce)
            decks = true;
        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (lifeforce)
            {
                var quantity = System.Text.RegularExpressions.Regex.Match(line,
                    @"^Sacred\s+Fauna\s+drop\s+(\d+)\s*%\s+increased\s+Quantity\s+of\s+Lifeforce\s*$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                if (quantity.Success && int.TryParse(quantity.Groups[1].Value, out int value) && value >= Math.Clamp(minLifeforce, 50, 150))
                    return true;
            }
            if (duplicated && System.Text.RegularExpressions.Regex.IsMatch(line,
                @"^Items\s+dropped\s+by\s+Sacred\s+Fauna\s+are\s+Duplicated\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                return true;
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
            (UpgradeStep.Identify, MonsterRarity.Magic) when identified == true => UpgradeStep.Scour,
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
        if (step == UpgradeStep.Alchemy && !alchemy)
            (missing ??= []).Add("Orbs of Alchemy");
        return missing == null ? null : string.Join(", ", missing);
    }

    private readonly record struct CurrencyStock(bool Wisdom, bool Scour, bool Alchemy, int? WisdomCount, bool SawInventoryItems,
        bool Chaos, int? ChaosCount);
}
