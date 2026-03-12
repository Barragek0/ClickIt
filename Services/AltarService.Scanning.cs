using ClickIt.Components;
using ClickIt.Definitions;
using ClickIt.Utils;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;

#nullable enable

namespace ClickIt.Services
{
    public partial class AltarService
    {
        public void ProcessAltarScanningLogic()
        {
            DebugInfo.LastScanTime = DateTime.Now;
            DebugInfo.ElementsFound = 0;
            DebugInfo.ComponentsProcessed = 0;
            DebugInfo.ComponentsAdded = 0;
            DebugInfo.ComponentsDuplicated = 0;
            DebugInfo.ModsMatched = 0;
            DebugInfo.ModsUnmatched = 0;
            DebugInfo.RecentUnmatchedMods.Clear();

            List<LabelOnGround> altarLabels = CollectAltarLabels();
            if (altarLabels.Count == 0)
            {
                ClearAltarComponents();
                return;
            }

            ProcessAltarLabels(altarLabels);
        }

        private List<LabelOnGround> CollectAltarLabels()
        {
            List<LabelOnGround> altarLabels = [];
            if (_settings.HighlightExarchAltars)
            {
                List<LabelOnGround> exarchLabels = GetAltarLabels(AltarType.SearingExarch);
                DebugInfo.LastScanExarchLabels = exarchLabels.Count;
                if (exarchLabels.Count > 0)
                {
                    altarLabels.AddRange(exarchLabels);
                }
            }

            if (_settings.HighlightEaterAltars)
            {
                List<LabelOnGround> eaterLabels = GetAltarLabels(AltarType.EaterOfWorlds);
                DebugInfo.LastScanEaterLabels = eaterLabels.Count;
                if (eaterLabels.Count > 0)
                {
                    altarLabels.AddRange(eaterLabels);
                }
            }

            return altarLabels;
        }

        private void ProcessAltarLabels(List<LabelOnGround> altarLabels)
        {
            var elementsToProcess = AltarScanner.CollectElementsFromLabels(altarLabels);
            DebugInfo.ElementsFound = elementsToProcess.Count;

            CleanupInvalidAltars();

            foreach ((Element? element, string path) in elementsToProcess)
            {
                if (element == null)
                    continue;

                DebugInfo.LastProcessedAltarType = DetermineAltarType(path).ToString();
                AltarType altarType = DetermineAltarType(path);
                PrimaryAltarComponent altarComponent = CreateAltarComponent(element, altarType);
                DebugInfo.ComponentsProcessed++;

                if (IsValidAltarComponent(altarComponent))
                {
                    bool wasAdded = AddAltarComponent(altarComponent);
                    WarmAddedAltarData(altarComponent, wasAdded);

                    if (wasAdded)
                        DebugInfo.ComponentsAdded++;
                    else
                        DebugInfo.ComponentsDuplicated++;
                }
            }
        }

        private void CleanupInvalidAltars()
        {
            _altarRepository.RemoveAltarComponentsByElement(altar =>
            {
                return altar.TopMods?.Element == null
                    || altar.BottomMods?.Element == null
                    || !altar.TopMods.Element.IsValid
                    || !altar.BottomMods.Element.IsValid;
            });
        }

        private static void PreCacheAltarData(PrimaryAltarComponent altar)
        {
            _ = altar.IsValidCached();
            _ = altar.GetTopModsRect();
            _ = altar.GetBottomModsRect();
        }

        private static void WarmAddedAltarData(PrimaryAltarComponent altar, bool wasAdded)
        {
            if (!wasAdded)
                return;

            PreCacheAltarData(altar);
        }

        private static AltarType DetermineAltarType(string path)
        {
            if (string.IsNullOrEmpty(path))
                return AltarType.Unknown;

            if (path.Contains(Constants.CleansingFireAltar, StringComparison.OrdinalIgnoreCase))
                return AltarType.SearingExarch;
            if (path.Contains(Constants.TangleAltar, StringComparison.OrdinalIgnoreCase))
                return AltarType.EaterOfWorlds;

            return AltarType.Unknown;
        }

        private PrimaryAltarComponent CreateAltarComponent(Element element, AltarType altarType)
        {
            var adapter = new ElementAdapter(element);
            return CreateAltarComponentFromAdapter(adapter, altarType);
        }

        internal PrimaryAltarComponent CreateAltarComponentFromAdapter(IElementAdapter elementAdapter, AltarType altarType)
        {
            if (elementAdapter == null || elementAdapter.Parent?.Parent == null)
                throw new InvalidOperationException("Failed to create valid altar component - missing required elements");

            IElementAdapter altarParentAdapter = elementAdapter.Parent.Parent;
            IElementAdapter? topAltarAdapter = altarParentAdapter.GetChildFromIndices(0, 1);
            IElementAdapter? bottomAltarAdapter = altarParentAdapter.GetChildFromIndices(1, 1);

            var topMods = topAltarAdapter != null ? new SecondaryAltarComponent(topAltarAdapter.Underlying, [], []) : null;
            var bottomMods = bottomAltarAdapter != null ? new SecondaryAltarComponent(bottomAltarAdapter.Underlying, [], []) : null;
            var topButton = topAltarAdapter != null ? new AltarButton(topAltarAdapter.Parent?.Underlying) : null;
            var bottomButton = bottomAltarAdapter != null ? new AltarButton(bottomAltarAdapter.Parent?.Underlying) : null;

            if (topMods == null || bottomMods == null || topButton == null || bottomButton == null)
                throw new InvalidOperationException("Failed to create valid altar component - missing required elements");

            PrimaryAltarComponent altarComponent = new(altarType, topMods, topButton, bottomMods, bottomButton);

            if (topAltarAdapter != null)
            {
                (string negativeModType, List<string> mods) = ExtractModsFromAdapter(topAltarAdapter);
                (List<string> upsides, List<string> downsides, bool hasUnmatched) = ProcessMods(mods, negativeModType);
                UpdateAltarComponentFromAdapter(true, altarComponent, topAltarAdapter, upsides, downsides, hasUnmatched);
            }

            if (bottomAltarAdapter != null)
            {
                (string negativeModType, List<string> mods) = ExtractModsFromAdapter(bottomAltarAdapter);
                (List<string> upsides, List<string> downsides, bool hasUnmatched) = ProcessMods(mods, negativeModType);
                UpdateAltarComponentFromAdapter(false, altarComponent, bottomAltarAdapter, upsides, downsides, hasUnmatched);
            }

            return altarComponent;
        }

        private (string negativeModType, List<string> mods) ExtractModsFromAdapter(IElementAdapter element)
        {
            string negativeModType = string.Empty;
            var mods = new List<string>();
            string altarMods = _altarMatcher.CleanAltarModsText(element.GetText(AltarModsTextReadLength));
            int lineCount = TextHelpers.CountLines(altarMods);
            for (int i = 0; i < lineCount; i++)
            {
                string line = TextHelpers.GetLine(altarMods, i);
                if (i == 0)
                {
                    negativeModType = line;
                }
                else if (line != null)
                {
                    mods.Add(line);
                }
            }

            return (negativeModType, mods);
        }

        private bool IsValidAltarComponent(PrimaryAltarComponent altarComponent)
        {
            bool isValid = altarComponent.TopMods != null && altarComponent.TopButton != null &&
                           altarComponent.BottomMods != null && altarComponent.BottomButton != null;
            if (!isValid)
            {
                DebugInfo.LastError = "Invalid altar component - missing parts";
            }

            return isValid;
        }
    }
}