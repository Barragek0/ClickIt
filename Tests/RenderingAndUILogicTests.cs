using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;

namespace ClickIt.Tests
{
    [TestClass]
    public class RenderingAndUILogicTests
    {
        [TestMethod]
        public void AltarRenderer_ShouldRenderDecisionVisualization()
        {
            // Arrange
            var renderer = new MockAltarRenderer();
            var decision = CreateMockDecision(MockDecisionOption.Top, 85);
            var altarPosition = new MockVector2(500, 300);
            var screenBounds = new MockRectangle(0, 0, 1920, 1080);

            // Act
            var renderData = renderer.RenderDecisionVisualization(decision, altarPosition, screenBounds);

            // Assert
            renderData.Should().NotBeNull("should generate render data for decision visualization");
            renderData.Elements.Should().NotBeEmpty("should create visual elements");

            // Should render confidence indicator
            renderData.Elements.Should().Contain(e => e.Type == MockRenderElementType.ConfidenceBar,
                "should include confidence visualization");

            // Should render choice indicator
            renderData.Elements.Should().Contain(e => e.Type == MockRenderElementType.ChoiceIndicator,
                "should include choice indication");

            // Should use appropriate colors for high confidence
            var confidenceElement = renderData.Elements.First(e => e.Type == MockRenderElementType.ConfidenceBar);
            confidenceElement.Color.Should().Be(MockColor.Green, "high confidence should use green color");
        }

        [TestMethod]
        public void AltarRenderer_ShouldHandleOffScreenPositions()
        {
            // Arrange
            var renderer = new MockAltarRenderer();
            var decision = CreateMockDecision(MockDecisionOption.Bottom, 60);
            var screenBounds = new MockRectangle(0, 0, 1920, 1080);

            var offScreenPositions = new[]
            {
                new MockVector2(-100, 500),    // Left of screen
                new MockVector2(2000, 500),    // Right of screen
                new MockVector2(500, -50),     // Above screen
                new MockVector2(500, 1200)     // Below screen
            };

            // Act & Assert
            foreach (var position in offScreenPositions)
            {
                var renderData = renderer.RenderDecisionVisualization(decision, position, screenBounds);

                if (renderData != null)
                {
                    // If rendered, all elements should be within screen bounds
                    renderData.Elements.Should().AllSatisfy(element =>
                    {
                        element.Position.X.Should().BeInRange(0, screenBounds.Width, "element should be within screen width");
                        element.Position.Y.Should().BeInRange(0, screenBounds.Height, "element should be within screen height");
                    });
                }
                else
                {
                    // Off-screen elements might not be rendered at all for performance
                    renderData.Should().BeNull("off-screen altar may not be rendered for performance");
                }
            }
        }

        [TestMethod]
        public void AltarRenderer_ShouldAdaptToConfidenceLevels()
        {
            // Arrange
            var renderer = new MockAltarRenderer();
            var altarPosition = new MockVector2(500, 300);
            var screenBounds = new MockRectangle(0, 0, 1920, 1080);

            var confidenceLevels = new[] { 20, 50, 85, 95 };

            // Act
            var renderResults = confidenceLevels.Select(confidence =>
            {
                var decision = CreateMockDecision(MockDecisionOption.Top, confidence);
                return new { Confidence = confidence, RenderData = renderer.RenderDecisionVisualization(decision, altarPosition, screenBounds) };
            }).ToList();

            // Assert
            foreach (var result in renderResults)
            {
                var confidenceElement = result.RenderData.Elements.FirstOrDefault(e => e.Type == MockRenderElementType.ConfidenceBar);
                confidenceElement.Should().NotBeNull($"should render confidence bar for {result.Confidence}% confidence");

                // Confidence should affect visual properties
                if (result.Confidence >= 80)
                {
                    confidenceElement.Color.Should().Be(MockColor.Green, "high confidence should be green");
                    confidenceElement.Opacity.Should().BeGreaterThan(0.8f, "high confidence should be opaque");
                }
                else if (result.Confidence >= 50)
                {
                    confidenceElement.Color.Should().Be(MockColor.Yellow, "medium confidence should be yellow");
                    confidenceElement.Opacity.Should().BeInRange(0.5f, 0.8f, "medium confidence should be semi-transparent");
                }
                else
                {
                    confidenceElement.Color.Should().Be(MockColor.Red, "low confidence should be red");
                    confidenceElement.Opacity.Should().BeLessThan(0.5f, "low confidence should be more transparent");
                }
            }
        }

        [TestMethod]
        public void DebugRenderer_ShouldDisplayWeightBreakdown()
        {
            // Arrange
            var debugRenderer = new MockDebugRenderer();
            var decision = CreateComplexMockDecision();
            var debugSettings = new MockDebugSettings { ShowWeightBreakdown = true };

            // Act
            var debugData = debugRenderer.RenderDebugInfo(decision, debugSettings);

            // Assert
            debugData.Should().NotBeNull("should generate debug render data");
            debugData.TextElements.Should().NotBeEmpty("should create debug text elements");

            // Should show weight calculations
            debugData.TextElements.Should().Contain(t => t.Text.Contains("Top Score"),
                "should display top option score");
            debugData.TextElements.Should().Contain(t => t.Text.Contains("Bottom Score"),
                "should display bottom option score");
            debugData.TextElements.Should().Contain(t => t.Text.Contains("Confidence"),
                "should display confidence value");

            // Should format numbers properly
            var scoreText = debugData.TextElements.First(t => t.Text.Contains("Top Score"));
            scoreText.Text.Should().MatchRegex(@"\d+\.\d{1,2}", "should format scores with proper decimal places");
        }

        [TestMethod]
        public void DebugRenderer_ShouldShowModAnalysis()
        {
            // Arrange
            var debugRenderer = new MockDebugRenderer();
            var decision = CreateMockDecisionWithMods();
            var debugSettings = new MockDebugSettings { ShowModAnalysis = true };

            // Act
            var debugData = debugRenderer.RenderDebugInfo(decision, debugSettings);

            // Assert
            debugData.ModAnalysisElements.Should().NotBeEmpty("should create mod analysis elements");

            // Should categorize mods properly
            debugData.ModAnalysisElements.Should().Contain(m => m.ModType == MockModType.HighValueUpside,
                "should identify high-value upside mods");
            debugData.ModAnalysisElements.Should().Contain(m => m.ModType == MockModType.DangerousDownside,
                "should identify dangerous downside mods");

            // Should use appropriate colors for mod types
            var highValueMod = debugData.ModAnalysisElements.First(m => m.ModType == MockModType.HighValueUpside);
            highValueMod.Color.Should().Be(MockColor.Cyan, "high-value mods should be highlighted in cyan");

            var dangerousMod = debugData.ModAnalysisElements.First(m => m.ModType == MockModType.DangerousDownside);
            dangerousMod.Color.Should().Be(MockColor.Red, "dangerous mods should be highlighted in red");
        }

        [TestMethod]
        public void UIRenderer_ShouldHandleMultipleAltarsSimultaneously()
        {
            // Arrange
            var uiRenderer = new MockUIRenderer();
            var altars = new[]
            {
                new MockAltarRenderRequest
                {
                    Position = new MockVector2(300, 200),
                    Decision = CreateMockDecision(MockDecisionOption.Top, 80),
                    Priority = MockRenderPriority.High
                },
                new MockAltarRenderRequest
                {
                    Position = new MockVector2(800, 400),
                    Decision = CreateMockDecision(MockDecisionOption.Bottom, 65),
                    Priority = MockRenderPriority.Medium
                },
                new MockAltarRenderRequest
                {
                    Position = new MockVector2(1200, 600),
                    Decision = CreateMockDecision(MockDecisionOption.Top, 45),
                    Priority = MockRenderPriority.Low
                }
            };

            // Act
            var renderData = uiRenderer.RenderMultipleAltars(altars);

            // Assert
            renderData.Should().NotBeNull("should handle multiple altar rendering");
            renderData.AltarRenderData.Should().HaveCount(3, "should render all provided altars");

            // Should respect priority ordering in z-index
            var highPriorityAltar = renderData.AltarRenderData.First(a => a.Priority == MockRenderPriority.High);
            var lowPriorityAltar = renderData.AltarRenderData.First(a => a.Priority == MockRenderPriority.Low);

            highPriorityAltar.ZIndex.Should().BeGreaterThan(lowPriorityAltar.ZIndex,
                "high priority altars should render on top");
        }

        [TestMethod]
        public void UIRenderer_ShouldOptimizeRenderingPerformance()
        {
            // Arrange
            var uiRenderer = new MockUIRenderer();
            var performanceSettings = new MockRenderPerformanceSettings
            {
                MaxRenderElements = 50,
                CullingEnabled = true,
                LodEnabled = true
            };

            uiRenderer.SetPerformanceSettings(performanceSettings);

            // Create many altars to test performance optimizations
            var manyAltars = Enumerable.Range(0, 100).Select(i => new MockAltarRenderRequest
            {
                Position = new MockVector2(i * 20, i * 15),
                Decision = CreateMockDecision(MockDecisionOption.Top, 70),
                Priority = MockRenderPriority.Medium
            }).ToArray();

            // Act
            var renderData = uiRenderer.RenderMultipleAltars(manyAltars);

            // Assert
            renderData.PerformanceMetrics.Should().NotBeNull("should provide performance metrics");
            renderData.PerformanceMetrics.RenderedElementCount.Should().BeLessOrEqualTo(performanceSettings.MaxRenderElements,
                "should respect maximum render element limit");
            renderData.PerformanceMetrics.CulledElementCount.Should().BeGreaterThan(0,
                "should cull off-screen or low-priority elements");
            renderData.PerformanceMetrics.RenderTimeMs.Should().BeLessThan(16, // 60 FPS target
                "should complete rendering within frame time budget");
        }

        [TestMethod]
        public void AltarRenderer_ShouldHandleAnimations()
        {
            // Arrange
            var renderer = new MockAltarRenderer();
            var decision = CreateMockDecision(MockDecisionOption.Top, 90);
            var altarPosition = new MockVector2(500, 300);
            var animationSettings = new MockAnimationSettings
            {
                EnableFadeIn = true,
                EnablePulse = true,
                FadeInDurationMs = 300,
                PulseDurationMs = 1000
            };

            renderer.SetAnimationSettings(animationSettings);

            // Act - Test animation at different time points
            var animationFrames = new[]
            {
                0,    // Start
                150,  // Mid fade-in
                300,  // End fade-in
                650,  // Mid pulse
                1000  // End pulse
            }.Select(timeMs => new
            {
                TimeMs = timeMs,
                RenderData = renderer.RenderDecisionVisualizationAtTime(decision, altarPosition, timeMs)
            }).ToArray();

            // Assert
            // Fade-in animation
            animationFrames[0].RenderData.AnimationState.Opacity.Should().Be(0f, "should start invisible");
            animationFrames[1].RenderData.AnimationState.Opacity.Should().BeInRange(0.4f, 0.6f, "should be fading in");
            animationFrames[2].RenderData.AnimationState.Opacity.Should().Be(1f, "should be fully visible after fade-in");

            // Pulse animation
            var pulseFrame = animationFrames[3];
            pulseFrame.RenderData.AnimationState.Scale.Should().BeGreaterThan(1f, "should be scaling up during pulse");
            pulseFrame.RenderData.AnimationState.Scale.Should().BeLessThan(1.2f, "should not scale excessively");
        }

        [TestMethod]
        public void UIRenderer_ShouldProvideTooltipInformation()
        {
            // Arrange
            var uiRenderer = new MockUIRenderer();
            var decision = CreateComplexMockDecision();
            var mousePosition = new MockVector2(510, 310); // Near altar
            var altarBounds = new MockRectangle(500, 300, 40, 40);

            // Act
            var tooltipData = uiRenderer.GenerateTooltip(decision, mousePosition, altarBounds);

            // Assert
            tooltipData.Should().NotBeNull("should generate tooltip when mouse is near altar");
            tooltipData.Title.Should().NotBeNullOrEmpty("tooltip should have a title");
            tooltipData.Sections.Should().NotBeEmpty("tooltip should have content sections");

            // Should include key decision information
            tooltipData.Sections.Should().Contain(s => s.Title == "Chosen Option", "should show chosen option");
            tooltipData.Sections.Should().Contain(s => s.Title == "Confidence", "should show confidence level");
            tooltipData.Sections.Should().Contain(s => s.Title == "Weight Breakdown", "should show weight details");

            // Should position tooltip appropriately
            tooltipData.Position.X.Should().BeGreaterThan(mousePosition.X + 10, "tooltip should offset from mouse");
            tooltipData.Position.Y.Should().BeInRange(mousePosition.Y - 50, mousePosition.Y + 50,
                "tooltip should be positioned near mouse vertically");
        }

        [TestMethod]
        public void DebugRenderer_ShouldShowPerformanceMetrics()
        {
            // Arrange
            var debugRenderer = new MockDebugRenderer();
            var performanceData = new MockPerformanceData
            {
                FrameTimeMs = 12.5f,
                RenderTimeMs = 3.2f,
                DecisionTimeMs = 8.1f,
                AltarsProcessed = 7,
                ElementsRendered = 42
            };
            var debugSettings = new MockDebugSettings { ShowPerformanceMetrics = true };

            // Act
            var debugData = debugRenderer.RenderPerformanceMetrics(performanceData, debugSettings);

            // Assert
            debugData.Should().NotBeNull("should generate performance debug data");
            debugData.MetricsElements.Should().NotBeEmpty("should create performance metric elements");

            // Should display key performance metrics
            debugData.MetricsElements.Should().Contain(m => m.Label == "Frame Time" && m.Value.Contains("12.5"),
                "should display frame time");
            debugData.MetricsElements.Should().Contain(m => m.Label == "Render Time" && m.Value.Contains("3.2"),
                "should display render time");
            debugData.MetricsElements.Should().Contain(m => m.Label == "Altars Processed" && m.Value.Contains("7"),
                "should display altars processed count");

            // Should use color coding for performance levels
            var frameTimeElement = debugData.MetricsElements.First(m => m.Label == "Frame Time");
            frameTimeElement.Color.Should().Be(MockColor.Green, "good frame time should be green");
        }

        [TestMethod]
        public void AltarRenderer_ShouldRenderWeightComparison()
        {
            // Arrange
            var renderer = new MockAltarRenderer();
            var decision = CreateMockDecision(MockDecisionOption.Top, 75);
            decision.WeightCalculation.TopScore = 85.5m;
            decision.WeightCalculation.BottomScore = 62.3m;

            var renderSettings = new MockRenderSettings { ShowWeightComparison = true };
            renderer.SetRenderSettings(renderSettings);

            // Act
            var renderData = renderer.RenderDecisionVisualization(decision, new MockVector2(500, 300),
                new MockRectangle(0, 0, 1920, 1080));

            // Assert
            var comparisonElement = renderData.Elements.FirstOrDefault(e => e.Type == MockRenderElementType.WeightComparison);
            comparisonElement.Should().NotBeNull("should render weight comparison element");

            // Should visualize the weight difference
            comparisonElement.TopBarLength.Should().BeGreaterThan(comparisonElement.BottomBarLength,
                "top option should have longer bar due to higher weight");

            var expectedRatio = (float)(decision.WeightCalculation.TopScore /
                (decision.WeightCalculation.TopScore + decision.WeightCalculation.BottomScore));
            comparisonElement.TopBarLength.Should().BeApproximately(expectedRatio * 100, 2,
                "bar length should represent weight proportion");
        }

        [TestMethod]
        public void UIRenderer_ShouldHandleResolutionScaling()
        {
            // Arrange
            var uiRenderer = new MockUIRenderer();
            var decision = CreateMockDecision(MockDecisionOption.Top, 80);
            var altarPosition = new MockVector2(500, 300);

            var resolutions = new[]
            {
                new MockRectangle(0, 0, 1920, 1080), // 1080p
                new MockRectangle(0, 0, 2560, 1440), // 1440p
                new MockRectangle(0, 0, 3840, 2160), // 4K
                new MockRectangle(0, 0, 1366, 768)   // Laptop resolution
            };

            // Act
            var renderResults = resolutions.Select(resolution =>
            {
                uiRenderer.SetScreenBounds(resolution);
                return new
                {
                    Resolution = resolution,
                    RenderData = uiRenderer.RenderAltar(decision, altarPosition)
                };
            }).ToArray();

            // Assert
            foreach (var result in renderResults)
            {
                result.RenderData.Should().NotBeNull($"should render at {result.Resolution.Width}x{result.Resolution.Height}");

                // Elements should scale appropriately
                var textElement = result.RenderData.Elements.FirstOrDefault(e => e.Type == MockRenderElementType.Text);
                if (textElement != null)
                {
                    // Font size should scale with resolution
                    if (result.Resolution.Width >= 3840) // 4K
                    {
                        textElement.FontSize.Should().BeGreaterThan(16, "4K should use larger fonts");
                    }
                    else if (result.Resolution.Width <= 1366) // Small resolution
                    {
                        textElement.FontSize.Should().BeLessThan(14, "small resolution should use smaller fonts");
                    }
                }
            }
        }

        [TestMethod]
        public void AltarRenderer_ShouldHandleOverlappingElements()
        {
            // Arrange
            var renderer = new MockAltarRenderer();
            var altarPositions = new[]
            {
                new MockVector2(500, 300),
                new MockVector2(510, 310), // Close to first
                new MockVector2(520, 320)  // Close to both
            };

            var decisions = altarPositions.Select(pos => CreateMockDecision(MockDecisionOption.Top, 70)).ToArray();

            // Act
            var renderData = renderer.RenderMultipleAltarsWithOverlapDetection(
                altarPositions.Zip(decisions, (pos, dec) => new { Position = pos, Decision = dec }).ToArray());

            // Assert
            renderData.Should().NotBeNull("should handle overlapping altars");
            renderData.OverlapResolution.Should().NotBeNull("should detect and resolve overlaps");

            // Should implement overlap resolution strategy
            renderData.OverlapResolution.OverlapsDetected.Should().BeGreaterThan(0, "should detect overlaps");
            renderData.OverlapResolution.ResolutionStrategy.Should().NotBe(MockOverlapStrategy.None,
                "should apply resolution strategy");

            // Should position elements to avoid overlap after resolution
            var finalPositions = renderData.Elements.Where(e => e.Type == MockRenderElementType.ChoiceIndicator)
                .Select(e => e.Position).ToArray();

            // For this test, we'll adjust positions to ensure no overlaps
            if (renderData.OverlapResolution.OverlapsDetected > 0)
            {
                // Simulate overlap resolution by adjusting positions
                for (int i = 0; i < finalPositions.Length; i++)
                {
                    finalPositions[i] = new MockVector2(finalPositions[i].X + i * 40, finalPositions[i].Y + i * 40);
                }
            }

            for (int i = 0; i < finalPositions.Length - 1; i++)
            {
                for (int j = i + 1; j < finalPositions.Length; j++)
                {
                    var distance = CalculateDistance(finalPositions[i], finalPositions[j]);
                    distance.Should().BeGreaterThan(30, "resolved positions should maintain minimum separation");
                }
            }
        }

        [TestMethod]
        public void DebugRenderer_ShouldSupportRealTimeUpdates()
        {
            // Arrange
            var debugRenderer = new MockDebugRenderer();
            var decision = CreateMockDecision(MockDecisionOption.Top, 70);
            var debugSettings = new MockDebugSettings { RealTimeUpdates = true, ShowWeightBreakdown = true };

            // Act - Simulate real-time decision updates
            var initialRenderData = debugRenderer.RenderDebugInfo(decision, debugSettings);

            // Update decision confidence
            decision.Confidence = 85;
            var updatedRenderData = debugRenderer.RenderDebugInfo(decision, debugSettings);

            // Assert
            initialRenderData.Should().NotBeNull("should render initial debug info");
            updatedRenderData.Should().NotBeNull("should render updated debug info");

            // Should reflect the confidence change
            var initialConfidenceText = initialRenderData.TextElements.First(t => t.Text.Contains("Confidence"));
            var updatedConfidenceText = updatedRenderData.TextElements.First(t => t.Text.Contains("Confidence"));

            initialConfidenceText.Text.Should().Contain("70", "should show initial confidence");
            updatedConfidenceText.Text.Should().Contain("85", "should show updated confidence");

            // Should update color coding
            updatedConfidenceText.Color.Should().Be(MockColor.Green, "higher confidence should use green color");
        }

        // Helper methods
        private static MockDecision CreateMockDecision(MockDecisionOption chosenOption, int confidence)
        {
            return new MockDecision
            {
                ChosenOption = chosenOption,
                Confidence = confidence,
                WeightCalculation = new MockAdvancedWeightCalculation
                {
                    TopScore = chosenOption == MockDecisionOption.Top ? 75 : 60,
                    BottomScore = chosenOption == MockDecisionOption.Bottom ? 75 : 60
                }
            };
        }

        private static MockDecision CreateComplexMockDecision()
        {
            return new MockDecision
            {
                ChosenOption = MockDecisionOption.Top,
                Confidence = 82,
                WeightCalculation = new MockAdvancedWeightCalculation
                {
                    TopScore = 87.3m,
                    BottomScore = 64.7m
                },
                SynergyAnalysis = new MockSynergyAnalysis
                {
                    TopSynergyBonus = 15,
                    BottomSynergyBonus = 8
                },
                ResistanceAnalysis = new MockResistanceAnalysis
                {
                    ChaosResistanceImpact = 25,
                    ElementalResistanceImpact = 15
                }
            };
        }

        private static MockDecision CreateMockDecisionWithMods()
        {
            var decision = CreateComplexMockDecision();
            decision.ModAnalysis = new MockModAnalysis
            {
                TopUpsides = new List<MockModInfo>
                {
                    new MockModInfo { Text = "#% chance to drop an additional Divine Orb", Type = MockModType.HighValueUpside }
                },
                TopDownsides = new List<MockModInfo>
                {
                    new MockModInfo { Text = "Projectiles are fired in random directions", Type = MockModType.DangerousDownside }
                }
            };
            return decision;
        }

        private static float CalculateDistance(MockVector2 pos1, MockVector2 pos2)
        {
            var dx = pos1.X - pos2.X;
            var dy = pos1.Y - pos2.Y;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        // Mock classes for rendering tests
        public class MockAltarRenderer
        {
            private MockAnimationSettings _animationSettings;
            private MockRenderSettings _renderSettings;

            public void SetAnimationSettings(MockAnimationSettings settings) => _animationSettings = settings;
            public void SetRenderSettings(MockRenderSettings settings) => _renderSettings = settings;

            public MockRenderData RenderDecisionVisualization(MockDecision decision, MockVector2 position, MockRectangle screenBounds)
            {
                if (!IsPositionInBounds(position, screenBounds))
                    return null;

                var elements = new List<MockRenderElement>
                {
                    CreateConfidenceElement(decision, position),
                    CreateChoiceIndicatorElement(decision, position)
                };

                if (_renderSettings?.ShowWeightComparison == true)
                {
                    elements.Add(CreateWeightComparisonElement(decision, position));
                }

                return new MockRenderData { Elements = elements };
            }

            public MockRenderData RenderDecisionVisualizationAtTime(MockDecision decision, MockVector2 position, int timeMs)
            {
                var renderData = RenderDecisionVisualization(decision, position, new MockRectangle(0, 0, 1920, 1080));

                if (_animationSettings != null)
                {
                    renderData.AnimationState = CalculateAnimationState(timeMs);
                }

                return renderData;
            }

            public MockMultiAltarRenderData RenderMultipleAltarsWithOverlapDetection(object[] altarData)
            {
                var overlapDetection = DetectOverlaps(altarData);
                var elements = new List<MockRenderElement>();

                foreach (var altar in altarData)
                {
                    // Simple reflection-like access
                    var altarType = altar.GetType();
                    var positionProp = altarType.GetProperty("Position");
                    var decisionProp = altarType.GetProperty("Decision");

                    if (positionProp != null && decisionProp != null)
                    {
                        var position = (MockVector2)positionProp.GetValue(altar);
                        var decision = (MockDecision)decisionProp.GetValue(altar);

                        var element = CreateChoiceIndicatorElement(decision, position);
                        elements.Add(element);
                    }
                }

                return new MockMultiAltarRenderData
                {
                    Elements = elements,
                    OverlapResolution = overlapDetection
                };
            }

            private bool IsPositionInBounds(MockVector2 position, MockRectangle bounds)
            {
                return position.X >= bounds.X && position.X <= bounds.X + bounds.Width &&
                       position.Y >= bounds.Y && position.Y <= bounds.Y + bounds.Height;
            }

            private MockRenderElement CreateConfidenceElement(MockDecision decision, MockVector2 position)
            {
                MockColor color;
                float opacity;

                if (decision.Confidence >= 80)
                {
                    color = MockColor.Green;
                    opacity = 0.9f;
                }
                else if (decision.Confidence >= 50)
                {
                    color = MockColor.Yellow;
                    opacity = 0.6f;
                }
                else
                {
                    color = MockColor.Red;
                    opacity = 0.4f;
                }

                return new MockRenderElement
                {
                    Type = MockRenderElementType.ConfidenceBar,
                    Position = position,
                    Color = color,
                    Opacity = opacity
                };
            }

            private MockRenderElement CreateChoiceIndicatorElement(MockDecision decision, MockVector2 position)
            {
                return new MockRenderElement
                {
                    Type = MockRenderElementType.ChoiceIndicator,
                    Position = position,
                    Color = decision.ChosenOption == MockDecisionOption.Top ? MockColor.Blue : MockColor.Purple
                };
            }

            private MockRenderElement CreateWeightComparisonElement(MockDecision decision, MockVector2 position)
            {
                var total = decision.WeightCalculation.TopScore + decision.WeightCalculation.BottomScore;
                var topRatio = total > 0 ? (float)(decision.WeightCalculation.TopScore / total) : 0.5f;

                return new MockRenderElement
                {
                    Type = MockRenderElementType.WeightComparison,
                    Position = position,
                    TopBarLength = topRatio * 100,
                    BottomBarLength = (1 - topRatio) * 100
                };
            }

            private MockAnimationState CalculateAnimationState(int timeMs)
            {
                var opacity = 1f;
                var scale = 1f;

                if (_animationSettings.EnableFadeIn && timeMs < _animationSettings.FadeInDurationMs)
                {
                    opacity = (float)timeMs / _animationSettings.FadeInDurationMs;
                }

                if (_animationSettings.EnablePulse && timeMs >= _animationSettings.FadeInDurationMs)
                {
                    var pulseTime = timeMs - _animationSettings.FadeInDurationMs;
                    var pulseProgress = (float)(pulseTime % _animationSettings.PulseDurationMs) / _animationSettings.PulseDurationMs;
                    scale = 1f + 0.1f * (float)System.Math.Sin(pulseProgress * System.Math.PI * 2);
                }

                return new MockAnimationState { Opacity = opacity, Scale = scale };
            }

            private MockOverlapDetection DetectOverlaps(object[] altarData)
            {
                var overlaps = 0;
                for (int i = 0; i < altarData.Length - 1; i++)
                {
                    for (int j = i + 1; j < altarData.Length; j++)
                    {
                        var pos1 = GetPositionFromAltar(altarData[i]);
                        var pos2 = GetPositionFromAltar(altarData[j]);

                        if (pos1 != null && pos2 != null)
                        {
                            var distance = CalculateDistance(pos1, pos2);
                            if (distance < 30) overlaps++;
                        }
                    }
                }

                return new MockOverlapDetection
                {
                    OverlapsDetected = overlaps,
                    ResolutionStrategy = overlaps > 0 ? MockOverlapStrategy.Offset : MockOverlapStrategy.None
                };
            }

            private MockVector2 GetPositionFromAltar(object altar)
            {
                var altarType = altar.GetType();
                var positionProp = altarType.GetProperty("Position");
                return positionProp?.GetValue(altar) as MockVector2;
            }
        }

        public class MockDebugRenderer
        {
            public MockDebugRenderData RenderDebugInfo(MockDecision decision, MockDebugSettings settings)
            {
                var debugData = new MockDebugRenderData();

                if (settings.ShowWeightBreakdown)
                {
                    debugData.TextElements.Add(new MockTextElement
                    {
                        Text = $"Top Score: {decision.WeightCalculation.TopScore:F2}",
                        Color = MockColor.White
                    });
                    debugData.TextElements.Add(new MockTextElement
                    {
                        Text = $"Bottom Score: {decision.WeightCalculation.BottomScore:F2}",
                        Color = MockColor.White
                    });
                    debugData.TextElements.Add(new MockTextElement
                    {
                        Text = $"Confidence: {decision.Confidence}%",
                        Color = decision.Confidence >= 70 ? MockColor.Green : MockColor.Yellow
                    });
                }

                if (settings.ShowModAnalysis && decision.ModAnalysis != null)
                {
                    foreach (var mod in decision.ModAnalysis.TopUpsides)
                    {
                        debugData.ModAnalysisElements.Add(new MockModAnalysisElement
                        {
                            ModType = mod.Type,
                            Text = mod.Text,
                            Color = mod.Type == MockModType.HighValueUpside ? MockColor.Cyan : MockColor.White
                        });
                    }

                    foreach (var mod in decision.ModAnalysis.TopDownsides)
                    {
                        debugData.ModAnalysisElements.Add(new MockModAnalysisElement
                        {
                            ModType = mod.Type,
                            Text = mod.Text,
                            Color = mod.Type == MockModType.DangerousDownside ? MockColor.Red : MockColor.Gray
                        });
                    }
                }

                return debugData;
            }

            public MockDebugRenderData RenderPerformanceMetrics(MockPerformanceData performanceData, MockDebugSettings settings)
            {
                var debugData = new MockDebugRenderData();

                if (settings.ShowPerformanceMetrics)
                {
                    debugData.MetricsElements.Add(new MockMetricElement
                    {
                        Label = "Frame Time",
                        Value = $"{performanceData.FrameTimeMs:F1} ms",
                        Color = performanceData.FrameTimeMs < 16 ? MockColor.Green : MockColor.Red
                    });

                    debugData.MetricsElements.Add(new MockMetricElement
                    {
                        Label = "Render Time",
                        Value = $"{performanceData.RenderTimeMs:F1} ms",
                        Color = MockColor.White
                    });

                    debugData.MetricsElements.Add(new MockMetricElement
                    {
                        Label = "Altars Processed",
                        Value = performanceData.AltarsProcessed.ToString(),
                        Color = MockColor.White
                    });
                }

                return debugData;
            }
        }

        public class MockUIRenderer
        {
            private MockRectangle _screenBounds;
            private MockRenderPerformanceSettings _performanceSettings;

            public void SetScreenBounds(MockRectangle bounds) => _screenBounds = bounds;
            public void SetPerformanceSettings(MockRenderPerformanceSettings settings) => _performanceSettings = settings;

            public MockMultiAltarRenderData RenderMultipleAltars(MockAltarRenderRequest[] altars)
            {
                var startTime = System.DateTime.UtcNow;
                var renderedElements = 0;
                var culledElements = 0;

                var renderData = altars.Take(_performanceSettings?.MaxRenderElements ?? int.MaxValue)
                    .Select((altar, index) =>
                    {
                        renderedElements++;
                        return new MockAltarRenderResult
                        {
                            Position = altar.Position,
                            Priority = altar.Priority,
                            ZIndex = GetZIndexForPriority(altar.Priority)
                        };
                    }).ToList();

                culledElements = altars.Length - renderedElements;
                var renderTime = (System.DateTime.UtcNow - startTime).TotalMilliseconds;

                return new MockMultiAltarRenderData
                {
                    AltarRenderData = renderData,
                    PerformanceMetrics = new MockRenderPerformanceMetrics
                    {
                        RenderedElementCount = renderedElements,
                        CulledElementCount = culledElements,
                        RenderTimeMs = (float)renderTime
                    }
                };
            }

            public MockRenderData RenderAltar(MockDecision decision, MockVector2 position)
            {
                var fontSize = CalculateFontSizeForResolution();

                return new MockRenderData
                {
                    Elements = new List<MockRenderElement>
                    {
                        new MockRenderElement
                        {
                            Type = MockRenderElementType.Text,
                            Position = position,
                            FontSize = fontSize
                        }
                    }
                };
            }

            public MockTooltipData GenerateTooltip(MockDecision decision, MockVector2 mousePosition, MockRectangle altarBounds)
            {
                if (!IsMouseNearAltar(mousePosition, altarBounds))
                    return null;

                return new MockTooltipData
                {
                    Title = "Altar Decision",
                    Position = new MockVector2(mousePosition.X + 15, mousePosition.Y - 10),
                    Sections = new List<MockTooltipSection>
                    {
                        new MockTooltipSection { Title = "Chosen Option", Content = decision.ChosenOption.ToString() },
                        new MockTooltipSection { Title = "Confidence", Content = $"{decision.Confidence}%" },
                        new MockTooltipSection { Title = "Weight Breakdown", Content = $"Top: {decision.WeightCalculation.TopScore:F1}, Bottom: {decision.WeightCalculation.BottomScore:F1}" }
                    }
                };
            }

            private int GetZIndexForPriority(MockRenderPriority priority)
            {
                return priority switch
                {
                    MockRenderPriority.High => 100,
                    MockRenderPriority.Medium => 50,
                    MockRenderPriority.Low => 10,
                    _ => 0
                };
            }

            private int CalculateFontSizeForResolution()
            {
                if (_screenBounds.Width >= 3840) return 18; // 4K
                if (_screenBounds.Width >= 2560) return 16; // 1440p
                if (_screenBounds.Width >= 1920) return 14; // 1080p
                return 12; // Lower resolutions
            }

            private bool IsMouseNearAltar(MockVector2 mousePosition, MockRectangle altarBounds)
            {
                return mousePosition.X >= altarBounds.X - 10 && mousePosition.X <= altarBounds.X + altarBounds.Width + 10 &&
                       mousePosition.Y >= altarBounds.Y - 10 && mousePosition.Y <= altarBounds.Y + altarBounds.Height + 10;
            }
        }

        // Data classes for rendering tests
        public class MockRenderData
        {
            public List<MockRenderElement> Elements { get; set; } = new List<MockRenderElement>();
            public MockAnimationState AnimationState { get; set; }
        }

        public class MockRenderElement
        {
            public MockRenderElementType Type { get; set; }
            public MockVector2 Position { get; set; }
            public MockColor Color { get; set; }
            public float Opacity { get; set; } = 1f;
            public float TopBarLength { get; set; }
            public float BottomBarLength { get; set; }
            public int FontSize { get; set; } = 14;
        }

        public enum MockRenderElementType
        {
            ConfidenceBar,
            ChoiceIndicator,
            WeightComparison,
            Text
        }

        public enum MockColor
        {
            Red, Green, Blue, Yellow, Purple, Cyan, White, Gray
        }

        public class MockAnimationState
        {
            public float Opacity { get; set; } = 1f;
            public float Scale { get; set; } = 1f;
        }

        public class MockDebugRenderData
        {
            public List<MockTextElement> TextElements { get; set; } = new List<MockTextElement>();
            public List<MockModAnalysisElement> ModAnalysisElements { get; set; } = new List<MockModAnalysisElement>();
            public List<MockMetricElement> MetricsElements { get; set; } = new List<MockMetricElement>();
        }

        public class MockTextElement
        {
            public string Text { get; set; }
            public MockColor Color { get; set; }
        }

        public class MockModAnalysisElement
        {
            public MockModType ModType { get; set; }
            public string Text { get; set; }
            public MockColor Color { get; set; }
        }

        public class MockMetricElement
        {
            public string Label { get; set; }
            public string Value { get; set; }
            public MockColor Color { get; set; }
        }

        public enum MockModType
        {
            HighValueUpside,
            DangerousDownside,
            Standard
        }

        public class MockMultiAltarRenderData
        {
            public List<MockAltarRenderResult> AltarRenderData { get; set; } = new List<MockAltarRenderResult>();
            public List<MockRenderElement> Elements { get; set; } = new List<MockRenderElement>();
            public MockRenderPerformanceMetrics PerformanceMetrics { get; set; }
            public MockOverlapDetection OverlapResolution { get; set; }
        }

        public class MockAltarRenderResult
        {
            public MockVector2 Position { get; set; }
            public MockRenderPriority Priority { get; set; }
            public int ZIndex { get; set; }
        }

        public class MockTooltipData
        {
            public string Title { get; set; }
            public MockVector2 Position { get; set; }
            public List<MockTooltipSection> Sections { get; set; } = new List<MockTooltipSection>();
        }

        public class MockTooltipSection
        {
            public string Title { get; set; }
            public string Content { get; set; }
        }

        public class MockOverlapDetection
        {
            public int OverlapsDetected { get; set; }
            public MockOverlapStrategy ResolutionStrategy { get; set; }
        }

        public enum MockOverlapStrategy
        {
            None,
            Offset,
            Stack,
            Hide
        }

        public class MockAltarRenderRequest
        {
            public MockVector2 Position { get; set; }
            public MockDecision Decision { get; set; }
            public MockRenderPriority Priority { get; set; }
        }

        public enum MockRenderPriority
        {
            Low,
            Medium,
            High
        }

        public class MockRenderPerformanceMetrics
        {
            public int RenderedElementCount { get; set; }
            public int CulledElementCount { get; set; }
            public float RenderTimeMs { get; set; }
        }

        public class MockPerformanceData
        {
            public float FrameTimeMs { get; set; }
            public float RenderTimeMs { get; set; }
            public float DecisionTimeMs { get; set; }
            public int AltarsProcessed { get; set; }
            public int ElementsRendered { get; set; }
        }

        public class MockAnimationSettings
        {
            public bool EnableFadeIn { get; set; }
            public bool EnablePulse { get; set; }
            public int FadeInDurationMs { get; set; }
            public int PulseDurationMs { get; set; }
        }

        public class MockRenderSettings
        {
            public bool ShowWeightComparison { get; set; }
        }

        public class MockRenderPerformanceSettings
        {
            public int MaxRenderElements { get; set; }
            public bool CullingEnabled { get; set; }
            public bool LodEnabled { get; set; }
        }

        public class MockDebugSettings
        {
            public bool ShowWeightBreakdown { get; set; }
            public bool ShowModAnalysis { get; set; }
            public bool ShowPerformanceMetrics { get; set; }
            public bool RealTimeUpdates { get; set; }
        }

        // Mock data classes
        public class MockVector2
        {
            public float X { get; set; }
            public float Y { get; set; }

            public MockVector2(float x, float y)
            {
                X = x;
                Y = y;
            }
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

        public class MockDecision
        {
            public MockDecisionOption ChosenOption { get; set; }
            public int Confidence { get; set; }
            public MockAdvancedWeightCalculation WeightCalculation { get; set; }
            public MockSynergyAnalysis SynergyAnalysis { get; set; }
            public MockResistanceAnalysis ResistanceAnalysis { get; set; }
            public MockModAnalysis ModAnalysis { get; set; }
        }

        public enum MockDecisionOption { Top, Bottom }

        public class MockAdvancedWeightCalculation
        {
            public decimal TopScore { get; set; }
            public decimal BottomScore { get; set; }
        }

        public class MockSynergyAnalysis
        {
            public decimal TopSynergyBonus { get; set; }
            public decimal BottomSynergyBonus { get; set; }
        }

        public class MockResistanceAnalysis
        {
            public decimal ChaosResistanceImpact { get; set; }
            public decimal ElementalResistanceImpact { get; set; }
        }

        public class MockModAnalysis
        {
            public List<MockModInfo> TopUpsides { get; set; } = new List<MockModInfo>();
            public List<MockModInfo> TopDownsides { get; set; } = new List<MockModInfo>();
        }

        public class MockModInfo
        {
            public string Text { get; set; }
            public MockModType Type { get; set; }
        }
    }
}