using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using Newtonsoft.Json;
using System.Linq;
using System.Numerics;
using ClickIt.Definitions;

namespace ClickIt
{
    /**
            * This file contains ClickItSettings rendering and helper logic that has not yet been moved into the planned panel-specific partials.
            * Panel declaration order remains driven by ClickItSettings.cs property declarations, so renderer extraction is safe as long as those declarations stay stable.
            */
    public partial class ClickItSettings : ISettings
    {
        private MechanicToggleTableEntry[]? _mechanicTableEntriesCache;
        private Dictionary<string, ToggleNode>? _mechanicToggleNodeByIdCache;
        private int _itemTypeMetadataSnapshotSignature = int.MinValue;
        private string[] _itemTypeWhitelistMetadataSnapshot = [];
        private string[] _itemTypeBlacklistMetadataSnapshot = [];
        private int _strongboxMetadataSnapshotSignature = int.MinValue;
        private string[] _strongboxClickMetadataSnapshot = [];
        private string[] _strongboxDontClickMetadataSnapshot = [];

        private void DrawPanelSafe(string panelName, Action drawAction)
        {
            try
            {
                drawAction();
            }
            catch (Exception ex)
            {
                _lastSettingsUiError = $"{panelName}: {ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ClickItSettings UI Error] {_lastSettingsUiError}{Environment.NewLine}{ex}");

                ImGui.Separator();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Settings UI error caught");
                ImGui.TextWrapped(_lastSettingsUiError);

                if (ImGui.Button($"Throw Last UI Error##{panelName}"))
                {
                    throw new InvalidOperationException(_lastSettingsUiError, ex);
                }
            }
        }

        public bool IsLazyModeDisableHotkeyToggleModeEnabled()
        {
            return LazyModeDisableKeyToggleMode?.Value == true;
        }

        public bool IsClickHotkeyToggleModeEnabled()
        {
            return ClickHotkeyToggleMode?.Value == true;
        }

        public bool IsInitialUltimatumClickEnabled()
        {
            return ClickInitialUltimatum?.Value == true;
        }

        public bool IsOtherUltimatumClickEnabled()
        {
            return ClickUltimatumChoices?.Value == true;
        }

        public bool IsAnyUltimatumClickEnabled()
        {
            return IsInitialUltimatumClickEnabled() || IsOtherUltimatumClickEnabled();
        }

        public bool IsUltimatumTakeRewardButtonClickEnabled()
        {
            return ClickUltimatumTakeRewardButton?.Value != false;
        }

        public bool IsAnyDetailedDebugSectionEnabled()
        {
            return DebugShowStatus
                || DebugShowGameState
                || DebugShowPerformance
                || DebugShowClickFrequencyTarget
                || DebugShowAltarDetection
                || DebugShowAltarService
                || DebugShowLabels
                || DebugShowInventoryPickup
                || DebugShowHoveredItemMetadata
                || DebugShowPathfinding
                || DebugShowUltimatum
                || DebugShowClicking
                || DebugShowRuntimeDebugLogOverlay
                || DebugShowRecentErrors;
        }

        public bool IsOnlyPathfindingDetailedDebugSectionEnabled()
        {
            return DebugShowPathfinding
            && !DebugShowStatus
            && !DebugShowGameState
            && !DebugShowPerformance
            && !DebugShowClickFrequencyTarget
            && !DebugShowAltarDetection
            && !DebugShowAltarService
            && !DebugShowLabels
            && !DebugShowInventoryPickup
            && !DebugShowHoveredItemMetadata
            && !DebugShowUltimatum
            && !DebugShowClicking
            && !DebugShowRuntimeDebugLogOverlay
            && !DebugShowRecentErrors;
        }

        private static float CalculateItemTypeRowWidth()
        {
            float availableWidth = Math.Max(80f, ImGui.GetContentRegionAvail().X);
            const float arrowWidth = 28f;
            return Math.Max(40f, availableWidth - arrowWidth - 6f);
        }

        private static Vector4 GetUltimatumPriorityRowColor(int index, int totalCount)
        {
            return UltimatumModifiersConstants.GetPriorityGradientColor(index, totalCount, 0.30f);
        }

        private static bool DrawUltimatumArrowButton(ImGuiDir direction, string id, bool enabled)
        {
            if (!enabled)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.5f);
            }

            bool clicked = ImGui.ArrowButton(id, direction);

            if (!enabled)
            {
                ImGui.PopStyleVar();
                return false;
            }

            return clicked;
        }

        public IReadOnlyList<string> GetMechanicPriorityOrder()
        {
            EnsureMechanicPrioritiesInitialized();

            if (HasMatchingMechanicPrioritySnapshot())
            {
                return _mechanicPrioritySnapshot;
            }

            _mechanicPrioritySnapshot = MechanicPriorityOrder.ToArray();
            return _mechanicPrioritySnapshot;
        }

        public IReadOnlyCollection<string> GetMechanicPriorityIgnoreDistanceIds()
        {
            EnsureMechanicPrioritiesInitialized();

            if (HasMatchingMechanicIgnoreDistanceSnapshot())
            {
                return _mechanicIgnoreDistanceSnapshot;
            }

            _mechanicIgnoreDistanceSnapshot = MechanicPriorityIgnoreDistanceIds.OrderBy(static x => x, PriorityComparer).ToArray();
            return _mechanicIgnoreDistanceSnapshot;
        }

        public IReadOnlyDictionary<string, int> GetMechanicPriorityIgnoreDistanceWithinById()
        {
            EnsureMechanicPrioritiesInitialized();

            if (HasMatchingMechanicIgnoreDistanceWithinSnapshot())
            {
                return _mechanicIgnoreDistanceWithinMapSnapshot;
            }

            _mechanicIgnoreDistanceWithinSnapshot = MechanicPriorityIgnoreDistanceWithinById
                .OrderBy(static x => x.Key, PriorityComparer)
                .ToArray();
            _mechanicIgnoreDistanceWithinMapSnapshot = new Dictionary<string, int>(
                _mechanicIgnoreDistanceWithinSnapshot.ToDictionary(static x => x.Key, static x => x.Value, PriorityComparer),
                PriorityComparer);
            return _mechanicIgnoreDistanceWithinMapSnapshot;
        }

        private bool HasMatchingMechanicPrioritySnapshot()
        {
            if (_mechanicPrioritySnapshot == null)
                return false;
            if (_mechanicPrioritySnapshot.Length != MechanicPriorityOrder.Count)
                return false;

            for (int i = 0; i < MechanicPriorityOrder.Count; i++)
            {
                if (!string.Equals(_mechanicPrioritySnapshot[i], MechanicPriorityOrder[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private bool HasMatchingMechanicIgnoreDistanceSnapshot()
        {
            if (_mechanicIgnoreDistanceSnapshot == null)
                return false;
            if (_mechanicIgnoreDistanceSnapshot.Length != MechanicPriorityIgnoreDistanceIds.Count)
                return false;

            var current = MechanicPriorityIgnoreDistanceIds.OrderBy(static x => x, PriorityComparer).ToArray();
            for (int i = 0; i < current.Length; i++)
            {
                if (!string.Equals(current[i], _mechanicIgnoreDistanceSnapshot[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        private bool HasMatchingMechanicIgnoreDistanceWithinSnapshot()
        {
            if (_mechanicIgnoreDistanceWithinSnapshot == null)
                return false;
            if (_mechanicIgnoreDistanceWithinSnapshot.Length != MechanicPriorityIgnoreDistanceWithinById.Count)
                return false;

            var current = MechanicPriorityIgnoreDistanceWithinById
                .OrderBy(static x => x.Key, PriorityComparer)
                .ToArray();
            for (int i = 0; i < current.Length; i++)
            {
                if (!string.Equals(current[i].Key, _mechanicIgnoreDistanceWithinSnapshot[i].Key, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (current[i].Value != _mechanicIgnoreDistanceWithinSnapshot[i].Value)
                    return false;
            }

            return true;
        }

    }
}
