using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClickIt.Constants;
using ClickIt.Components;

#nullable enable

namespace ClickIt.Services
{
    /// <summary>
    /// Handles altar detection, processing, and decision-making logic
    /// </summary>
    public class AltarService
    {
        private readonly ClickItSettings _settings;
        private readonly TimeCache<List<LabelOnGround>>? _cachedLabels;
        private readonly List<PrimaryAltarComponent> _altarComponents = new();

        private const string CleansingFireAltar = "CleansingFireAltar";
        private const string TangleAltar = "TangleAltar";

        public AltarService(ClickItSettings settings, TimeCache<List<LabelOnGround>>? cachedLabels)
        {
            _settings = settings;
            _cachedLabels = cachedLabels;
        }

        public List<PrimaryAltarComponent> GetAltarComponents() => _altarComponents.ToList();

        public void ClearAltarComponents() => _altarComponents.Clear();

        public List<LabelOnGround> GetAltarLabels(ClickIt.AltarType type)
        {
            List<LabelOnGround> result = new();
            List<LabelOnGround>? cachedLabels = _cachedLabels?.Value;

            if (cachedLabels == null)
                return result;

            string typeStr = type == ClickIt.AltarType.SearingExarch ? CleansingFireAltar : TangleAltar;

            for (int i = 0; i < cachedLabels.Count; i++)
            {
                LabelOnGround label = cachedLabels[i];
                if (label.ItemOnGround?.Path == null || !label.Label.IsVisible)
                    continue;

                if (label.ItemOnGround.Path.Contains(typeStr))
                    result.Add(label);
            }

            return result;
        }

        public bool AddAltarComponent(PrimaryAltarComponent component)
        {
            string newKey = BuildAltarKey(component);
            bool exists = _altarComponents.Any(existingComp => BuildAltarKey(existingComp) == newKey);

            if (!exists)
            {
                _altarComponents.Add(component);
                return true;
            }

            return false;
        }

        public void UpdateComponentFromElementData(bool top, Element altarParent, PrimaryAltarComponent altarComponent,
            Element ElementToExtractDataFrom, ClickIt.AltarType altarType, Action<string, float> logMessage, Action<string, float> logError)
        {
            var (negativeModType, mods) = ExtractModsFromElement(ElementToExtractDataFrom, logMessage);
            var (upsides, downsides) = ProcessMods(mods, negativeModType, logMessage, logError);
            UpdateAltarComponent(top, altarComponent, ElementToExtractDataFrom, upsides, downsides, logMessage);
        }

        private (string negativeModType, List<string> mods) ExtractModsFromElement(Element element, Action<string, float> logMessage)
        {
            string negativeModType = "";
            List<string> mods = new();

            if (_settings.DebugMode)
            {
                logMessage(element.GetText(512), 0);
            }

            string altarMods = CleanAltarModsText(element.GetText(512));
            int lineCount = CountLines(element.GetText(512));

            for (int i = 0; i < lineCount; i++)
            {
                string line = GetLine(altarMods, i);
                if (i == 0)
                {
                    negativeModType = line;
                }
                else if (line != null)
                {
                    mods.Add(line);
                }

                if (_settings.DebugMode)
                {
                    logMessage("Altarmods (" + i + ") Added: " + line, 0);
                }
            }

            return (negativeModType, mods);
        }

        private static string CleanAltarModsText(string text)
        {
            string cleaned = text.Replace("<valuedefault>", "").Replace("{", "")
                .Replace("}", "").Replace("<enchanted>", "").Replace(" ", "")
                .Replace("gain:", "").Replace("gains:", "");

            return Regex.Replace(cleaned, @"<rgb\(\d+,\d+,\d+\)>", "");
        }

        private (List<string> upsides, List<string> downsides) ProcessMods(List<string> mods, string negativeModType,
            Action<string, float> logMessage, Action<string, float> logError)
        {
            List<string> upsides = new();
            List<string> downsides = new();

            foreach (string mod in mods)
            {
                if (TryMatchMod(mod, negativeModType, out bool isUpside, out string matchedId))
                {
                    if (isUpside)
                        upsides.Add(matchedId);
                    else
                        downsides.Add(matchedId);

                    if (_settings.DebugMode)
                    {
                        logMessage($"Added {(isUpside ? "upside" : "downside")}: {matchedId}", 0);
                    }
                }
                else if (_settings.DebugMode)
                {
                    string cleanedMod = new string(mod.Where(char.IsLetter).ToArray());
                    logError($"updateComponentFromElementData: Failed to match mod: '{mod}' (Cleaned: '{cleanedMod}') with NegativeModType: '{negativeModType}'", 10);
                }
            }

            return (upsides, downsides);
        }

        private static bool TryMatchMod(string mod, string negativeModType, out bool isUpside, out string matchedId)
        {
            isUpside = false;
            matchedId = string.Empty;

            string cleanedMod = new string(mod.Where(char.IsLetter).ToArray());
            string cleanedNegativeModType = new string(negativeModType.Where(char.IsLetter).ToArray());
            string modTarget = GetModTarget(cleanedNegativeModType);

            var searchLists = new[]
            {
                new { List = AltarModsConstants.UpsideMods, IsUpside = true },
                new { List = AltarModsConstants.DownsideMods, IsUpside = false }
            };

            foreach (var searchList in searchLists)
            {
                foreach (var (Id, _, Type, _) in searchList.List)
                {
                    string cleanedId = new string(Id.Where(char.IsLetter).ToArray());
                    if (cleanedId.Equals(cleanedMod, StringComparison.OrdinalIgnoreCase) &&
                        Type.Equals(modTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        isUpside = searchList.IsUpside;
                        matchedId = Id;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string GetModTarget(string cleanedNegativeModType)
        {
            if (cleanedNegativeModType.Contains("Mapboss")) return "Boss";
            if (cleanedNegativeModType.Contains("EldritchMinions")) return "Minion";
            if (cleanedNegativeModType.Contains("Player")) return "Player";
            return "";
        }

        private void UpdateAltarComponent(bool top, PrimaryAltarComponent altarComponent, Element element,
            List<string> upsides, List<string> downsides, Action<string, float> logMessage)
        {
            if (_settings.DebugMode)
            {
                logMessage("Setting up altar component", 0);
            }

            if (top)
            {
                altarComponent.TopButton = new AltarButton(element.Parent);
                altarComponent.TopMods = new SecondaryAltarComponent(element, upsides, downsides);
                LogAltarComponentDetails("top", altarComponent.TopMods, logMessage);
            }
            else
            {
                altarComponent.BottomButton = new AltarButton(element.Parent);
                altarComponent.BottomMods = new SecondaryAltarComponent(element, upsides, downsides);
                LogAltarComponentDetails("bottom", altarComponent.BottomMods, logMessage);
            }
        }

        private void LogAltarComponentDetails(string position, SecondaryAltarComponent mods, Action<string, float> logMessage)
        {
            if (!_settings.DebugMode) return;

            logMessage($"Updated {position} altar component: " + mods, 0);
            logMessage("Upside1: " + mods.FirstUpside, 0);
            logMessage("Upside2: " + mods.SecondUpside, 0);
            logMessage("Downside1: " + mods.FirstDownside, 0);
            logMessage("Downside2: " + mods.SecondDownside, 0);
        }

        private static string GetLine(string text, int lineNo)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length >= lineNo ? lines[lineNo] : "ERROR: Could not read line.";
        }

        private static int CountLines(string text)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length;
        }

        public void ProcessAltarScanningLogic(Action<string, float> logMessage, Action<string, float> logError)
        {
            List<LabelOnGround> altarLabels = CollectAltarLabels();

            if (altarLabels.Count == 0)
            {
                ClearAltarComponents();
                return;
            }

            ProcessAltarLabels(altarLabels, logMessage, logError);
        }

        private List<LabelOnGround> CollectAltarLabels()
        {
            List<LabelOnGround> altarLabels = new();

            if (_settings.HighlightExarchAltars)
            {
                List<LabelOnGround> exarchLabels = GetAltarLabels(ClickIt.AltarType.SearingExarch);
                if (exarchLabels.Count > 0)
                {
                    altarLabels.AddRange(exarchLabels);
                }
            }

            if (_settings.HighlightEaterAltars)
            {
                List<LabelOnGround> eaterLabels = GetAltarLabels(ClickIt.AltarType.EaterOfWorlds);
                if (eaterLabels.Count > 0)
                {
                    altarLabels.AddRange(eaterLabels);
                }
            }

            return altarLabels;
        }

        private void ProcessAltarLabels(List<LabelOnGround> altarLabels, Action<string, float> logMessage, Action<string, float> logError)
        {
            foreach (LabelOnGround label in altarLabels)
            {
                if (label == null) continue;

                List<Element> elements = Services.ElementService.GetElementsByStringContains(label.Label, "valuedefault");
                if (elements == null || elements.Count == 0) continue;

                string path = label.ItemOnGround?.Path ?? string.Empty;
                ProcessElementsForLabel(elements, path, logMessage, logError);
            }
        }

        private void ProcessElementsForLabel(List<Element> elements, string path, Action<string, float> logMessage, Action<string, float> logError)
        {
            foreach (Element element in elements)
            {
                if (!IsValidElement(element, logError)) continue;

                LogAltarType(path, logMessage);
                ClickIt.AltarType altarType = DetermineAltarType(path);
                PrimaryAltarComponent altarComponent = CreateAltarComponent(element, altarType, logMessage, logError);

                if (IsValidAltarComponent(altarComponent, logError))
                {
                    bool wasAdded = AddAltarComponent(altarComponent);
                    LogAltarAddition(wasAdded, logMessage);
                }
            }
        }

        private bool IsValidElement(Element element, Action<string, float> logError)
        {
            if (element == null || !element.IsVisible)
            {
                if (_settings.DebugMode)
                {
                    logError("Element is null", 10);
                }
                return false;
            }
            return true;
        }

        private void LogAltarType(string path, Action<string, float> logMessage)
        {
            if (!_settings.DebugMode) return;

            if (path.Contains(CleansingFireAltar))
            {
                logMessage("CleansingFireAltar", 0);
            }
            else if (path.Contains(TangleAltar))
            {
                logMessage("TangleAltar", 0);
            }
        }

        private static ClickIt.AltarType DetermineAltarType(string path)
        {
            if (path.Contains(CleansingFireAltar))
                return ClickIt.AltarType.SearingExarch;
            else if (path.Contains(TangleAltar))
                return ClickIt.AltarType.EaterOfWorlds;
            else
                return ClickIt.AltarType.Unknown;
        }

        private PrimaryAltarComponent CreateAltarComponent(Element element, ClickIt.AltarType altarType, Action<string, float> logMessage, Action<string, float> logError)
        {
            PrimaryAltarComponent altarComponent = new(altarType,
                new SecondaryAltarComponent(new Element(), new List<string>(), new List<string>()), new AltarButton(new Element()),
                new SecondaryAltarComponent(new Element(), new List<string>(), new List<string>()), new AltarButton(new Element()));

            Element altarParent = element.Parent.Parent;
            Element? topAltarElement = altarParent.GetChildFromIndices(0, 1);
            Element? bottomAltarElement = altarParent.GetChildFromIndices(1, 1);

            if (topAltarElement != null)
            {
                UpdateComponentFromElementData(true, altarParent, altarComponent, topAltarElement, altarType, logMessage, logError);
            }

            if (bottomAltarElement != null)
            {
                UpdateComponentFromElementData(false, altarParent, altarComponent, bottomAltarElement, altarType, logMessage, logError);
            }

            return altarComponent;
        }

        private bool IsValidAltarComponent(PrimaryAltarComponent altarComponent, Action<string, float> logError)
        {
            bool isValid = altarComponent.TopMods != null && altarComponent.TopButton != null &&
                          altarComponent.BottomMods != null && altarComponent.BottomButton != null;

            if (!isValid && _settings.DebugMode)
            {
                logError("Part of altarcomponent is null", 10);
                logError("part1: " + altarComponent.TopMods, 10);
                logError("part2: " + altarComponent.TopButton, 10);
                logError("part3: " + altarComponent.BottomMods, 10);
                logError("part4: " + altarComponent.BottomButton, 10);
            }

            return isValid;
        }

        private void LogAltarAddition(bool wasAdded, Action<string, float> logMessage)
        {
            if (_settings.DebugMode)
            {
                logMessage(wasAdded ? "New altar added to altarcomponents list" : "Altar already added to altarcomponents list", 0);
            }
        }

        private static string BuildAltarKey(PrimaryAltarComponent comp)
        {
            string t1 = comp.TopMods?.FirstUpside ?? string.Empty;
            string t2 = comp.TopMods?.SecondUpside ?? string.Empty;
            string td1 = comp.TopMods?.FirstDownside ?? string.Empty;
            string td2 = comp.TopMods?.SecondDownside ?? string.Empty;
            string b1 = comp.BottomMods?.FirstUpside ?? string.Empty;
            string b2 = comp.BottomMods?.SecondUpside ?? string.Empty;
            string bd1 = comp.BottomMods?.FirstDownside ?? string.Empty;
            string bd2 = comp.BottomMods?.SecondDownside ?? string.Empty;

            return string.Concat(t1, "|", t2, "|", td1, "|", td2, "|", b1, "|", b2, "|", bd1, "|", bd2);
        }
    }
}