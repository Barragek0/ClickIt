using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
            string NegativeModType = "";
            List<string> mods = new();
            List<string> upsides = new();
            List<string> downsides = new();

            if (_settings.DebugMode)
            {
                logMessage(ElementToExtractDataFrom.GetText(512), 0);
            }

            string AltarMods = ElementToExtractDataFrom.GetText(512).Replace("<valuedefault>", "").Replace("{", "")
                .Replace("}", "").Replace("<enchanted>", "").Replace(" ", "").Replace("gain:", "")
                .Replace("gains:", "");

            AltarMods = Regex.Replace(AltarMods, @"<rgb\(\d+,\d+,\d+\)>", "");

            for (int i = 0; i < CountLines(ElementToExtractDataFrom.GetText(512)); i++)
            {
                if (i == 0)
                {
                    NegativeModType = GetLine(AltarMods, 0);
                }
                else if (GetLine(AltarMods, i) != null)
                {
                    mods.Add(GetLine(AltarMods, i));
                }

                if (_settings.DebugMode)
                {
                    logMessage("Altarmods (" + i + ") Added: " + GetLine(AltarMods, i), 0);
                }
            }

            foreach (string mod in mods.ToList())
            {
                bool found = false;
                string cleanedMod = new string(mod.Where(char.IsLetter).ToArray());
                string cleanedNegativeModType = new string(NegativeModType.Where(char.IsLetter).ToArray());

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
                        if (cleanedId.Equals(cleanedMod, StringComparison.OrdinalIgnoreCase))
                        {
                            string modTarget = "";
                            if (cleanedNegativeModType.Contains("Mapboss")) modTarget = "Boss";
                            else if (cleanedNegativeModType.Contains("EldritchMinions")) modTarget = "Minion";
                            else if (cleanedNegativeModType.Contains("Player")) modTarget = "Player";

                            if (Type.Equals(modTarget, StringComparison.OrdinalIgnoreCase))
                            {
                                if (searchList.IsUpside)
                                {
                                    upsides.Add(Id);
                                }
                                else
                                {
                                    downsides.Add(Id);
                                }
                                found = true;

                                if (_settings.DebugMode)
                                {
                                    logMessage($"Added {(searchList.IsUpside ? "upside" : "downside")}: {Id} (Type: {Type})", 0);
                                }
                                break;
                            }
                        }
                    }
                    if (found) break;
                }

                if (!found && _settings.DebugMode)
                {
                    logError($"updateComponentFromElementData: Failed to match mod: '{mod}' (Cleaned: '{cleanedMod}') with NegativeModType: '{NegativeModType}'", 10);
                }
            }

            if (_settings.DebugMode)
            {
                logMessage("Setting up altar component", 0);
            }

            if (top)
            {
                altarComponent.TopButton = new AltarButton(ElementToExtractDataFrom.Parent);
                altarComponent.TopMods = new SecondaryAltarComponent(ElementToExtractDataFrom, upsides, downsides);
                if (_settings.DebugMode)
                {
                    logMessage("Updated top altar component: " + altarComponent.TopMods, 0);
                    logMessage("Upside1: " + altarComponent.TopMods.FirstUpside, 0);
                    logMessage("Upside2: " + altarComponent.TopMods.SecondUpside, 0);
                    logMessage("Downside1: " + altarComponent.TopMods.FirstDownside, 0);
                    logMessage("Downside2: " + altarComponent.TopMods.SecondDownside, 0);
                }
            }
            else
            {
                altarComponent.BottomButton = new AltarButton(ElementToExtractDataFrom.Parent);
                altarComponent.BottomMods = new SecondaryAltarComponent(ElementToExtractDataFrom, upsides, downsides);
                if (_settings.DebugMode)
                {
                    logMessage("Updated bottom altar component: " + altarComponent.BottomMods, 0);
                    logMessage("Upside1: " + altarComponent.BottomMods.FirstUpside, 0);
                    logMessage("Upside2: " + altarComponent.BottomMods.SecondUpside, 0);
                    logMessage("Downside1: " + altarComponent.BottomMods.FirstDownside, 0);
                    logMessage("Downside2: " + altarComponent.BottomMods.SecondDownside, 0);
                }
            }
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
            List<LabelOnGround> altarLabels = new();
            bool highlightExarch = _settings.HighlightExarchAltars;
            bool highlightEater = _settings.HighlightEaterAltars;

            if (highlightExarch)
            {
                List<LabelOnGround> l = GetAltarLabels(ClickIt.AltarType.SearingExarch);
                if (l.Count > 0)
                {
                    altarLabels.AddRange(l);
                }
            }

            if (highlightEater)
            {
                List<LabelOnGround> l = GetAltarLabels(ClickIt.AltarType.EaterOfWorlds);
                if (l.Count > 0)
                {
                    altarLabels.AddRange(l);
                }
            }

            if (altarLabels.Count == 0)
            {
                ClearAltarComponents();
                return;
            }

            bool debug = _settings.DebugMode;

            for (int i = 0; i < altarLabels.Count; i++)
            {
                LabelOnGround label = altarLabels[i];
                if (label == null)
                {
                    continue;
                }

                List<Element> elements = Services.ElementService.GetElementsByStringContains(label.Label, "valuedefault");
                if (elements == null || elements.Count == 0)
                {
                    continue;
                }

                string path = label.ItemOnGround?.Path ?? string.Empty;

                for (int j = 0; j < elements.Count; j++)
                {
                    Element element = elements[j];
                    if (element == null || !element.IsVisible)
                    {
                        if (debug)
                        {
                            logError("Element is null", 10);
                        }
                        continue;
                    }

                    if (debug && path.Contains(CleansingFireAltar))
                    {
                        logMessage("CleansingFireAltar", 0);
                    }
                    else if (debug && path.Contains(TangleAltar))
                    {
                        logMessage("TangleAltar", 0);
                    }

                    ClickIt.AltarType altarType;
                    if (path.Contains(CleansingFireAltar))
                        altarType = ClickIt.AltarType.SearingExarch;
                    else if (path.Contains(TangleAltar))
                        altarType = ClickIt.AltarType.EaterOfWorlds;
                    else
                        altarType = ClickIt.AltarType.Unknown;

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

                    if (altarComponent.TopMods == null || altarComponent.TopButton == null || altarComponent.BottomMods == null || altarComponent.BottomButton == null)
                    {
                        if (debug)
                        {
                            logError("Part of altarcomponent is null", 10);
                            logError("part1: " + altarComponent.TopMods, 10);
                            logError("part2: " + altarComponent.TopButton, 10);
                            logError("part3: " + altarComponent.BottomMods, 10);
                            logError("part4: " + altarComponent.BottomButton, 10);
                        }
                        continue;
                    }

                    bool wasAdded = AddAltarComponent(altarComponent);
                    if (debug)
                    {
                        logMessage(wasAdded ? "New altar added to altarcomponents list" : "Altar already added to altarcomponents list", 0);
                    }
                }
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