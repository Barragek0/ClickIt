using System;
using System.Collections.Generic;
using System.Linq;

namespace ClickIt.Tests
{
    // Shared test utilities and lightweight mocks used across multiple test files.
    public class MockClickItSettings
    {
        public bool CorruptAllEssences { get; set; } = false;
        public int ClickDistance { get; set; } = 95;
        private readonly Dictionary<string, int> _modWeights = new Dictionary<string, int>();
        public Dictionary<string, int> ModTiers => _modWeights;

        public void SetModWeight(string modId, int weight) => _modWeights[modId] = weight;
        public int GetModTier(string modId) => _modWeights.TryGetValue(modId, out int value) ? value : 50;
    }

    public class MockAltarService
    {
        public MockAltarWeights CalculateWeights(MockAltarComponent altar, MockClickItSettings settings)
        {
            var calculator = new MockWeightCalculator(settings);
            return calculator.CalculateAltarWeights(altar);
        }

        public (string negativeModType, List<string> mods) ExtractModsFromElement(MockElement element)
        {
            return ("PlayerDropsItemsOnDeath", new List<string>
            {
                "#% increased Quantity of Items found in this Area",
                "Final Boss drops # additional Divine Orbs",
                "-#% to Chaos Resistance",
                "#% reduced Recovery Rate of Life, Mana and Energy Shield per Endurance Charge"
            });
        }

        public (List<string> upsides, List<string> downsides) ProcessModsData(string negativeModType, List<string> mods)
        {
            var upsides = mods.Where(m => !m.StartsWith("-") && !m.Contains("reduced")).ToList();
            var downsides = mods.Where(m => m.StartsWith("-") || m.Contains("reduced")).ToList();
            return (upsides, downsides);
        }

        public MockAltarDecision DetermineOptimalChoice(MockAltarComponent altar, MockAltarWeights weights)
        {
            return new MockAltarDecision
            {
                IsTopChoice = weights.TopWeight > weights.BottomWeight,
                Confidence = (int)Math.Abs(weights.TopWeight - weights.BottomWeight) * 10
            };
        }

        public bool ShouldProcessAltar(MockAltarLabel altar, bool isInClickableArea)
        {
            return isInClickableArea;
        }

        public bool CanProcessAltar(MockAltarComponent altar)
        {
            return altar.TopMods.Upsides.Any() || altar.TopMods.Downsides.Any() ||
                   altar.BottomMods.Upsides.Any() || altar.BottomMods.Downsides.Any();
        }

        public MockAltarData ProcessNestedElementStructure(List<MockElement> elements)
        {
            return new MockAltarData
            {
                TopMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "Complex upside mod with nested structure" },
                    Downsides = new List<string> { "Complex downside mod" }
                },
                BottomMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "Another complex upside" },
                    Downsides = new List<string> { "Another complex downside" }
                }
            };
        }
    }

    public class MockWeightCalculator
    {
        private readonly MockClickItSettings _settings;

        public MockWeightCalculator(MockClickItSettings settings)
        {
            _settings = settings;
        }

        public MockAltarWeights CalculateAltarWeights(MockAltarComponent altar)
        {
            var topUpsideWeight = CalculateUpsideWeight(altar.TopMods.Upsides);
            var topDownsideWeight = CalculateDownsideWeight(altar.TopMods.Downsides);
            var bottomUpsideWeight = CalculateUpsideWeight(altar.BottomMods.Upsides);
            var bottomDownsideWeight = CalculateDownsideWeight(altar.BottomMods.Downsides);

            return new MockAltarWeights
            {
                TopUpsideWeight = topUpsideWeight,
                TopDownsideWeight = topDownsideWeight,
                BottomUpsideWeight = bottomUpsideWeight,
                BottomDownsideWeight = bottomDownsideWeight,
                TopUpside1Weight = GetModWeightSafely(altar.TopMods.Upsides, 0),
                TopDownside1Weight = GetModWeightSafely(altar.TopMods.Downsides, 0),
                BottomUpside1Weight = GetModWeightSafely(altar.BottomMods.Upsides, 0),
                BottomDownside1Weight = GetModWeightSafely(altar.BottomMods.Downsides, 0),
                TopUpside2Weight = GetModWeightSafely(altar.TopMods.Upsides, 1),
                TopDownside2Weight = GetModWeightSafely(altar.TopMods.Downsides, 1),
                BottomUpside2Weight = GetModWeightSafely(altar.BottomMods.Upsides, 1),
                BottomDownside2Weight = GetModWeightSafely(altar.BottomMods.Downsides, 1),
                TopUpside3Weight = GetModWeightSafely(altar.TopMods.Upsides, 2),
                TopDownside3Weight = GetModWeightSafely(altar.TopMods.Downsides, 2),
                BottomUpside3Weight = GetModWeightSafely(altar.BottomMods.Upsides, 2),
                BottomDownside3Weight = GetModWeightSafely(altar.BottomMods.Downsides, 2),
                TopUpside4Weight = GetModWeightSafely(altar.TopMods.Upsides, 3),
                TopDownside4Weight = GetModWeightSafely(altar.TopMods.Downsides, 3),
                BottomUpside4Weight = GetModWeightSafely(altar.BottomMods.Upsides, 3),
                BottomDownside4Weight = GetModWeightSafely(altar.BottomMods.Downsides, 3),
                TopWeight = topDownsideWeight > 0 ? Math.Round(topUpsideWeight / topDownsideWeight, 2) : 0,
                BottomWeight = bottomDownsideWeight > 0 ? Math.Round(bottomUpsideWeight / bottomDownsideWeight, 2) : 0
            };
        }

        private int GetModWeightSafely(List<string> mods, int index)
        {
            return mods.Count > index ? _settings.GetModTier(mods[index]) : 0;
        }

        public bool HasWeightOverrides(MockAltarWeights weights)
        {
            return weights.GetTopUpsideWeights().Any(w => w >= 90) || weights.GetTopDownsideWeights().Any(w => w >= 90) ||
                   weights.GetBottomUpsideWeights().Any(w => w >= 90) || weights.GetBottomDownsideWeights().Any(w => w >= 90);
        }

        private decimal CalculateUpsideWeight(List<string> upsides)
        {
            return upsides?.Sum(u => _settings.GetModTier(u)) ?? 0;
        }

        private decimal CalculateDownsideWeight(List<string> downsides)
        {
            return (downsides?.Sum(d => _settings.GetModTier(d)) ?? 0) + 1;
        }
    }

    public class MockLabelFilterService
    {
        private readonly MockClickItSettings _settings;

        public MockLabelFilterService(MockClickItSettings settings)
        {
            _settings = settings;
        }

        public List<MockLabel> GetFilteredLabels(List<MockLabel> labels)
        {
            return labels.Where(ShouldClickLabel).ToList();
        }

        public bool ShouldClickLabel(MockLabel label)
        {
            return !string.IsNullOrEmpty(label.Path) &&
                   (label.Path.Contains("Delve") || label.Path.Contains("Harvest") ||
                    label.Path.Contains("Altar") || label.Path.Contains("Crafting"));
        }

        public bool IsWithinClickDistance(MockLabel label)
        {
            return label.Distance <= _settings.ClickDistance;
        }
    }

    public class MockAreaService
    {
        private MockRectangle _healthArea;
        private MockRectangle _manaArea;
        private MockRectangle _buffsArea;
        private MockRectangle _fullArea;

        public void UpdateScreenAreas(MockRectangle windowRect)
        {
            _fullArea = windowRect;
            _healthArea = new MockRectangle(windowRect.Width / 3, windowRect.Height * 0.78f, windowRect.Width / 3.4f, windowRect.Height * 0.22f);
            _manaArea = new MockRectangle(windowRect.Width * 0.71f, windowRect.Height * 0.78f, windowRect.Width * 0.29f, windowRect.Height * 0.22f);
            _buffsArea = new MockRectangle(0, 0, windowRect.Width / 2, 120);
        }

        public bool PointIsInClickableArea(MockVector2 point)
        {
            return IsInRectangle(point, _fullArea) &&
                   !IsInRectangle(point, _healthArea) &&
                   !IsInRectangle(point, _manaArea) &&
                   !IsInRectangle(point, _buffsArea);
        }

        private static bool IsInRectangle(MockVector2 point, MockRectangle rect)
        {
            return point.X >= rect.X && point.X <= rect.X + rect.Width &&
                   point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
        }
    }

    public class MockInputHandler
    {
        public bool IsValidClickPosition(MockVector2 position, MockRectangle windowRect)
        {
            var areaService = new MockAreaService();
            areaService.UpdateScreenAreas(windowRect);
            return areaService.PointIsInClickableArea(position);
        }

        public MockVector2 CalculateClickPosition(MockLabel label, MockVector2 windowOffset)
        {
            var random = new Random(0); // deterministic for tests
            return new MockVector2(
                label.Position.X + windowOffset.X + random.Next(-2, 3),
                label.Position.Y + windowOffset.Y + random.Next(-2, 3)
            );
        }

        public bool CanPerformClickOn(MockLabel label)
        {
            return !string.IsNullOrEmpty(label.Path);
        }
    }

    public class MockEssenceService
    {
        private readonly MockClickItSettings _settings;

        public MockEssenceService(MockClickItSettings settings)
        {
            _settings = settings;
        }

        public bool ShouldCorruptEssence(MockElement element)
        {
            return _settings.CorruptAllEssences;
        }

        public MockVector2 GetCorruptionClickPosition(MockElement element, MockVector2 windowOffset)
        {
            return new MockVector2(windowOffset.X + 50, windowOffset.Y + 50);
        }
    }

    public static class MockElementService
    {
        public static MockElement GetElementByString(MockElement root, string searchString)
        {
            return root?.Text?.Contains(searchString) == true ? root : null;
        }

        public static List<MockElement> GetElementsByStringContains(MockElement root, string searchString)
        {
            var results = new List<MockElement>();
            if (root?.Text?.Contains(searchString) == true)
            {
                results.Add(root);
            }
            return results;
        }

        public static bool IsValidElement(MockElement element)
        {
            return element != null && !string.IsNullOrEmpty(element.Text);
        }

        public static MockElement CreateAltarElement(string negativeModType, string[] upsides, string[] downsides)
        {
            return new MockElement { Text = $"valuedefault {negativeModType} {string.Join(" ", upsides)} {string.Join(" ", downsides)}" };
        }

        public static MockElement CreateEssenceElement(string[] essenceTypes, bool hasCorruption)
        {
            return new MockElement { Text = $"{string.Join(" ", essenceTypes)} {(hasCorruption ? "corruption" : "")}" };
        }

        public static MockElement CreateBasicElement()
        {
            return new MockElement { Text = "valuedefault basic element" };
        }

        public static MockElement CreateNestedAltarElement(int depth, string negativeModType, string[] upsides, string[] downsides)
        {
            return new MockElement
            {
                Text = $"valuedefault nested_{depth} {negativeModType} {string.Join(" ", upsides)} {string.Join(" ", downsides)}",
                Depth = depth
            };
        }

        public static MockElement CreateElementAtPosition(float x, float y)
        {
            return new MockElement
            {
                Text = "valuedefault positioned element",
                Position = new MockVector2(x, y)
            };
        }

        public static MockVector2 GetElementCenter(MockElement element)
        {
            return element.Position ?? new MockVector2(0, 0);
        }

        public static bool IsElementVisible(MockElement element)
        {
            return element != null && !string.IsNullOrEmpty(element.Text);
        }
    }

    // Common factory helpers used by multiple tests
    public static class TestFactories
    {
        public static MockAltarComponent CreateTestAltarComponent()
        {
            return new MockAltarComponent
            {
                TopMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "#% chance to drop an additional Divine Orb", "#% increased Quantity of Items found in this Area" },
                    Downsides = new List<string> { "-#% to Chaos Resistance", "#% reduced Recovery Rate of Life, Mana and Energy Shield per Endurance Charge" }
                },
                BottomMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "Final Boss drops # additional Divine Orbs", "#% increased Experience gain" },
                    Downsides = new List<string> { "Projectiles are fired in random directions", "-#% to Fire Resistance" }
                }
            };
        }

        public static MockAltarComponent CreateComplexTestAltarComponent()
        {
            return new MockAltarComponent
            {
                TopMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "#% chance to drop an additional Divine Orb", "Final Boss drops # additional Divine Orbs" },
                    Downsides = new List<string> { "Projectiles are fired in random directions", "Curses you inflict are reflected back to you" }
                },
                BottomMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string> { "#% increased Quantity of Items found in this Area", "#% increased Experience gain" },
                    Downsides = new List<string> { "-#% to Chaos Resistance", "#% reduced Recovery Rate of Life, Mana and Energy Shield per Endurance Charge" }
                }
            };
        }

        public static MockAltarComponent CreateEmptyAltarComponent()
        {
            return new MockAltarComponent
            {
                TopMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string>(),
                    Downsides = new List<string>()
                },
                BottomMods = new MockSecondaryAltarComponent
                {
                    Upsides = new List<string>(),
                    Downsides = new List<string>()
                }
            };
        }

        public static List<MockLabel> CreateMockLabels()
        {
            return new List<MockLabel>
            {
                CreateMockLabel(100, 100, "DelveMineral"),
                CreateMockLabel(200, 200, "CleansingFireAltar"),
                CreateMockLabel(300, 300, "Harvest/Extractor"),
                CreateMockLabel(800, 950, "TangleAltar") // In health area
            };
        }

        public static MockLabel CreateMockLabel(float x, float y, string path)
        {
            return new MockLabel
            {
                Position = new MockVector2(x, y),
                Path = path,
                Distance = 50,
                MockElement = MockElementService.CreateBasicElement()
            };
        }

        public static MockLabel CreateMockLabelWithDistance(float distance, string path)
        {
            return new MockLabel
            {
                Position = new MockVector2(500, 300),
                Path = path,
                Distance = distance,
                MockElement = MockElementService.CreateBasicElement()
            };
        }

        public static MockAltarLabel CreateMockAltarLabel(float x, float y, string path)
        {
            return new MockAltarLabel
            {
                Position = new MockVector2(x, y),
                Path = path,
                AltarType = path.Contains("CleansingFireAltar") ? "SearingExarch" : "EaterOfWorlds"
            };
        }
    }

    // Data classes
    public class MockAltarWeights
    {
        public decimal TopUpsideWeight { get; set; }
        public decimal TopDownsideWeight { get; set; }
        public decimal BottomUpsideWeight { get; set; }
        public decimal BottomDownsideWeight { get; set; }
        public decimal TopDownside1Weight { get; set; }
        public decimal TopDownside2Weight { get; set; }
        public decimal TopDownside3Weight { get; set; }
        public decimal TopDownside4Weight { get; set; }
        public decimal TopDownside5Weight { get; set; }
        public decimal TopDownside6Weight { get; set; }
        public decimal TopDownside7Weight { get; set; }
        public decimal TopDownside8Weight { get; set; }
        public decimal BottomDownside1Weight { get; set; }
        public decimal BottomDownside2Weight { get; set; }
        public decimal BottomDownside3Weight { get; set; }
        public decimal BottomDownside4Weight { get; set; }
        public decimal BottomDownside5Weight { get; set; }
        public decimal BottomDownside6Weight { get; set; }
        public decimal BottomDownside7Weight { get; set; }
        public decimal BottomDownside8Weight { get; set; }
        public decimal TopUpside1Weight { get; set; }
        public decimal TopUpside2Weight { get; set; }
        public decimal TopUpside3Weight { get; set; }
        public decimal TopUpside4Weight { get; set; }
        public decimal TopUpside5Weight { get; set; }
        public decimal TopUpside6Weight { get; set; }
        public decimal TopUpside7Weight { get; set; }
        public decimal TopUpside8Weight { get; set; }
        public decimal BottomUpside1Weight { get; set; }
        public decimal BottomUpside2Weight { get; set; }
        public decimal BottomUpside3Weight { get; set; }
        public decimal BottomUpside4Weight { get; set; }
        public decimal BottomUpside5Weight { get; set; }
        public decimal BottomUpside6Weight { get; set; }
        public decimal BottomUpside7Weight { get; set; }
        public decimal BottomUpside8Weight { get; set; }
        public decimal TopWeight { get; set; }
        public decimal BottomWeight { get; set; }

        public decimal[] GetTopDownsideWeights()
        {
            return new[] { TopDownside1Weight, TopDownside2Weight, TopDownside3Weight, TopDownside4Weight,
                           TopDownside5Weight, TopDownside6Weight, TopDownside7Weight, TopDownside8Weight };
        }

        public decimal[] GetBottomDownsideWeights()
        {
            return new[] { BottomDownside1Weight, BottomDownside2Weight, BottomDownside3Weight, BottomDownside4Weight,
                           BottomDownside5Weight, BottomDownside6Weight, BottomDownside7Weight, BottomDownside8Weight };
        }

        public decimal[] GetTopUpsideWeights()
        {
            return new[] { TopUpside1Weight, TopUpside2Weight, TopUpside3Weight, TopUpside4Weight,
                           TopUpside5Weight, TopUpside6Weight, TopUpside7Weight, TopUpside8Weight };
        }

        public decimal[] GetBottomUpsideWeights()
        {
            return new[] { BottomUpside1Weight, BottomUpside2Weight, BottomUpside3Weight, BottomUpside4Weight,
                           BottomUpside5Weight, BottomUpside6Weight, BottomUpside7Weight, BottomUpside8Weight };
        }
    }

    public class MockAltarComponent
    {
        public MockSecondaryAltarComponent TopMods { get; set; }
        public MockSecondaryAltarComponent BottomMods { get; set; }
    }

    public class MockSecondaryAltarComponent
    {
        public List<string> Upsides { get; set; } = new List<string>();
        public List<string> Downsides { get; set; } = new List<string>();

        public string GetUpsideByIndex(int index)
        {
            if (index < 0 || index >= 4) return "";
            return Upsides.Count > index ? Upsides[index] : "";
        }

        public string GetDownsideByIndex(int index)
        {
            if (index < 0 || index >= 4) return "";
            return Downsides.Count > index ? Downsides[index] : "";
        }

        public string FirstUpside => GetUpsideByIndex(0);
        public string SecondUpside => GetUpsideByIndex(1);
        public string ThirdUpside => GetUpsideByIndex(2);
        public string FourthUpside => GetUpsideByIndex(3);
        public string FifthUpside => GetUpsideByIndex(4);
        public string SixthUpside => GetUpsideByIndex(5);
        public string SeventhUpside => GetUpsideByIndex(6);
        public string EighthUpside => GetUpsideByIndex(7);
        public string FirstDownside => GetDownsideByIndex(0);
        public string SecondDownside => GetDownsideByIndex(1);
        public string ThirdDownside => GetDownsideByIndex(2);
        public string FourthDownside => GetDownsideByIndex(3);
        public string FifthDownside => GetDownsideByIndex(4);
        public string SixthDownside => GetDownsideByIndex(5);
        public string SeventhDownside => GetDownsideByIndex(6);
        public string EighthDownside => GetDownsideByIndex(7);
    }

    public class MockLabel
    {
        public MockVector2 Position { get; set; }
        public string Path { get; set; }
        public float Distance { get; set; }
        public MockElement MockElement { get; set; }
    }

    public class MockAltarLabel
    {
        public MockVector2 Position { get; set; }
        public string Path { get; set; }
        public string AltarType { get; set; }
    }

    public class MockElement
    {
        public string Text { get; set; }
        public int Depth { get; set; }
        public MockVector2 Position { get; set; }
    }

    public class MockVector2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public MockVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X}, {Y})";
    }

    public class MockRectangle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public MockRectangle(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public class MockElementResult
    {
        public MockLabel Label { get; set; }
        public MockElement Element { get; set; }
    }

    public class MockAltarDecision
    {
        public bool IsTopChoice { get; set; }
        public int Confidence { get; set; }
    }

    public class MockAltarData
    {
        public MockSecondaryAltarComponent TopMods { get; set; }
        public MockSecondaryAltarComponent BottomMods { get; set; }
    }
}
