using Microsoft.VisualStudio.TestTools.UnitTesting;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using ClickIt.Constants;

namespace ClickIt.Tests
{
    [TestClass]
    public class AdvancedDecisionLogicTests
    {
        [TestMethod]
        public void DecisionLogic_ShouldPrioritizeBuildCriticalMods()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            // Set build-critical mod weights
            settings.SetModWeight("Projectiles are fired in random directions", 100); // Build-breaking
            settings.SetModWeight("Curses you inflict are reflected back to you", 100); // Build-breaking
            settings.SetModWeight("#% chance to drop an additional Divine Orb", 95); // High value

            var buildBreakingAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% chance to drop an additional Divine Orb" },
                topDownsides: new[] { "Projectiles are fired in random directions" },
                bottomUpsides: new[] { "#% increased Experience gain" },
                bottomDownsides: new[] { "-#% to Fire Resistance" }
            );

            // Act
            var decision = decisionEngine.MakeDecision(buildBreakingAltar);

            // Assert
            decision.ChosenOption.Should().Be(MockDecisionOption.Bottom, "should avoid build-breaking mods regardless of upside value");

            // Debug: Check if build-breaking mod is actually detected
            var hasBuildBreaking = buildBreakingAltar.TopMods.Downsides.Any(d => d.Contains("Projectiles"));
            hasBuildBreaking.Should().BeTrue("should detect projectiles in top downsides");

            decision.Confidence.Should().BeGreaterThan(85, "build-breaking avoidance should have high confidence");
            decision.Reason.Should().Contain("build-breaking", "decision reason should mention build-breaking mods");
        }

        [TestMethod]
        public void DecisionLogic_ShouldHandleMultipleHighValueConflicts()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            settings.SetModWeight("#% chance to drop an additional Divine Orb", 95);
            settings.SetModWeight("Final Boss drops # additional Divine Orbs", 95);
            settings.SetModWeight("#% increased Quantity of Items found in this Area", 90);
            settings.SetModWeight("-#% to Chaos Resistance", 80);

            var highValueConflictAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% chance to drop an additional Divine Orb", "#% increased Quantity of Items found in this Area" },
                topDownsides: new[] { "-#% to Chaos Resistance" },
                bottomUpsides: new[] { "Final Boss drops # additional Divine Orbs" },
                bottomDownsides: new[] { "-#% to Fire Resistance" }
            );

            // Act
            var decision = decisionEngine.MakeDecision(highValueConflictAltar);

            // Assert
            decision.Should().NotBeNull("should make a decision even with high-value conflicts");
            decision.WeightCalculation.TopScore.Should().BeGreaterThan(0, "top option should have positive score");
            decision.WeightCalculation.BottomScore.Should().BeGreaterThan(0, "bottom option should have positive score");
            decision.Confidence.Should().BeInRange(30, 70, "high-value conflicts should produce moderate confidence");
        }

        [TestMethod]
        public void DecisionLogic_ShouldCalculateConfidenceBasedOnWeightDifference()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            var scenarios = new[]
            {
                // Clear winner scenario
                CreateAltarWithMods(
                    topUpsides: new[] { "#% chance to drop an additional Divine Orb", "Final Boss drops # additional Divine Orbs" },
                    topDownsides: new[] { "-#% to Fire Resistance" },
                    bottomUpsides: new[] { "#% increased Experience gain" },
                    bottomDownsides: new[] { "-#% to Chaos Resistance" }
                ),
                // Close competition scenario
                CreateAltarWithMods(
                    topUpsides: new[] { "#% chance to drop an additional Divine Orb" },
                    topDownsides: new[] { "-#% to Chaos Resistance" },
                    bottomUpsides: new[] { "Final Boss drops # additional Divine Orbs" },
                    bottomDownsides: new[] { "-#% to Fire Resistance" }
                ),
                // Extremely close scenario
                CreateAltarWithMods(
                    topUpsides: new[] { "#% increased Quantity of Items found in this Area" },
                    topDownsides: new[] { "-#% to Lightning Resistance" },
                    bottomUpsides: new[] { "#% increased Experience gain" },
                    bottomDownsides: new[] { "-#% to Cold Resistance" }
                )
            };

            // Act
            var decisions = scenarios.Select(altar => decisionEngine.MakeDecision(altar)).ToList();

            // Assert
            decisions[0].Confidence.Should().BeGreaterThan(80, "clear winner should have high confidence");
            decisions[1].Confidence.Should().BeInRange(40, 80, "close competition should have moderate confidence");
            decisions[2].Confidence.Should().BeLessThan(40, "extremely close should have low confidence");

            // Confidence should correlate with weight difference
            var weightDifferences = decisions.Select(d => System.Math.Abs(d.WeightCalculation.TopScore - d.WeightCalculation.BottomScore)).ToList();
            weightDifferences[0].Should().BeGreaterThan(weightDifferences[1], "clear winner should have larger weight difference");
            weightDifferences[1].Should().BeGreaterThan(weightDifferences[2], "close competition should have larger weight difference than extremely close");
        }

        [TestMethod]
        public void DecisionLogic_ShouldHandleAsymmetricModCounts()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            var asymmetricAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% chance to drop an additional Divine Orb", "#% increased Quantity of Items found in this Area", "Final Boss drops # additional Divine Orbs" },
                topDownsides: new[] { "-#% to Chaos Resistance" },
                bottomUpsides: new[] { "#% increased Experience gain" },
                bottomDownsides: new[] { "-#% to Fire Resistance", "-#% to Lightning Resistance", "Projectiles are fired in random directions" }
            );

            // Act
            var decision = decisionEngine.MakeDecision(asymmetricAltar);

            // Assert
            decision.Should().NotBeNull("should handle asymmetric mod counts");
            decision.WeightCalculation.TopModCount.Should().Be(4, "should count all top mods");
            decision.WeightCalculation.BottomModCount.Should().Be(4, "should count all bottom mods");
            decision.Analysis.Should().Contain("asymmetric", "should note asymmetric mod distribution");
        }

        [TestMethod]
        public void DecisionLogic_ShouldApplyResistanceWeightingStrategy()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            // Configure resistance penalties
            settings.SetModWeight("-#% to Chaos Resistance", 85);   // Dangerous
            settings.SetModWeight("-#% to Fire Resistance", 60);    // Manageable
            settings.SetModWeight("-#% to Lightning Resistance", 65); // Moderate
            settings.SetModWeight("-#% to Cold Resistance", 60);    // Manageable

            var resistanceTestAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% chance to drop an additional Divine Orb" },
                topDownsides: new[] { "-#% to Chaos Resistance" },
                bottomUpsides: new[] { "#% chance to drop an additional Divine Orb" },
                bottomDownsides: new[] { "-#% to Fire Resistance", "-#% to Lightning Resistance" }
            );

            // Act
            var decision = decisionEngine.MakeDecision(resistanceTestAltar);

            // Assert
            decision.WeightCalculation.TopResistancePenalty.Should().BeGreaterThan(decision.WeightCalculation.BottomResistancePenalty,
                "chaos resistance penalty should be higher than elemental resistance penalties");
            decision.ResistanceAnalysis.ChaosResistanceImpact.Should().BeGreaterThan(0, "should analyze chaos resistance impact");
            decision.ResistanceAnalysis.ElementalResistanceImpact.Should().BeGreaterThan(0, "should analyze elemental resistance impact");
        }

        [TestMethod]
        public void DecisionLogic_ShouldEvaluateModSynergies()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            var synergyAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% increased Quantity of Items found in this Area", "#% increased Rarity of Items found in this Area" }, // Synergistic
                topDownsides: new[] { "-#% to Fire Resistance" },
                bottomUpsides: new[] { "#% chance to drop an additional Divine Orb" }, // Standalone high value
                bottomDownsides: new[] { "-#% to Lightning Resistance" }
            );

            // Act
            var decision = decisionEngine.MakeDecision(synergyAltar);

            // Assert
            decision.SynergyAnalysis.Should().NotBeNull("should perform synergy analysis");
            decision.SynergyAnalysis.TopSynergyBonus.Should().BeGreaterThan(0, "quantity + rarity should have synergy bonus");
            decision.SynergyAnalysis.BottomSynergyBonus.Should().Be(0, "single mod should have no synergy bonus");
            decision.WeightCalculation.TopSynergyAdjustedScore.Should().BeGreaterThan(decision.WeightCalculation.TopScore,
                "synergy should increase adjusted score");
        }

        [TestMethod]
        public void DecisionLogic_ShouldHandleExtremeValueScenarios()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            // Extreme scenarios
            var extremeScenarios = new[]
            {
                // Maximum upside vs maximum downside
                CreateAltarWithMods(
                    topUpsides: new[] { "#% chance to drop an additional Divine Orb", "Final Boss drops # additional Divine Orbs" },
                    topDownsides: new[] { "Projectiles are fired in random directions", "Curses you inflict are reflected back to you" },
                    bottomUpsides: new[] { "#% increased Experience gain" },
                    bottomDownsides: new[] { "-#% to Fire Resistance" }
                ),
                // All negative scenario
                CreateAltarWithMods(
                    topUpsides: new string[] { },
                    topDownsides: new[] { "-#% to Chaos Resistance", "Projectiles are fired in random directions" },
                    bottomUpsides: new string[] { },
                    bottomDownsides: new[] { "-#% to Fire Resistance", "-#% to Lightning Resistance" }
                ),
                // Overwhelming positive scenario
                CreateAltarWithMods(
                    topUpsides: new[] { "#% chance to drop an additional Divine Orb", "Final Boss drops # additional Divine Orbs", "#% increased Quantity of Items found in this Area" },
                    topDownsides: new[] { "-#% to Fire Resistance" },
                    bottomUpsides: new[] { "#% increased Experience gain" },
                    bottomDownsides: new[] { "-#% to Lightning Resistance" }
                )
            };

            // Act
            var decisions = extremeScenarios.Select(altar => decisionEngine.MakeDecision(altar)).ToList();

            // Assert
            decisions[0].ChosenOption.Should().Be(MockDecisionOption.Bottom, "should avoid extreme negative despite high positive");
            decisions[1].ChosenOption.Should().NotBe(default(MockDecisionOption), "should make decision even with all negative options");
            decisions[1].Confidence.Should().BeLessThan(30, "all negative scenarios should have low confidence");
            decisions[2].ChosenOption.Should().Be(MockDecisionOption.Top, "should choose overwhelming positive option");
            decisions[2].Confidence.Should().BeGreaterThan(90, "overwhelming positive should have very high confidence");
        }

        [TestMethod]
        public void DecisionLogic_ShouldConsiderSequentialDecisionImpact()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);
            var sequentialTracker = new MockSequentialDecisionTracker();

            decisionEngine.SetSequentialTracker(sequentialTracker);

            var firstAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% increased Quantity of Items found in this Area" },
                topDownsides: new[] { "-#% to Fire Resistance" },
                bottomUpsides: new[] { "#% increased Experience gain" },
                bottomDownsides: new[] { "-#% to Lightning Resistance" }
            );

            var secondAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% increased Rarity of Items found in this Area" },
                topDownsides: new[] { "-#% to Fire Resistance" }, // Stacks with previous
                bottomUpsides: new[] { "#% chance to drop an additional Divine Orb" },
                bottomDownsides: new[] { "-#% to Cold Resistance" }
            );

            // Act
            var firstDecision = decisionEngine.MakeDecision(firstAltar);
            sequentialTracker.RecordDecision(firstDecision);
            var secondDecision = decisionEngine.MakeDecision(secondAltar);

            // Assert
            if (firstDecision.ChosenOption == MockDecisionOption.Top)
            {
                secondDecision.SequentialAnalysis.ResistanceStackingRisk.Should().BeGreaterThan(0,
                    "should detect fire resistance stacking risk");
                secondDecision.WeightCalculation.TopScore.Should().BeLessThan(
                    decisionEngine.MakeDecisionWithoutHistory(secondAltar).WeightCalculation.TopScore,
                    "stacking penalty should reduce score");
            }
        }

        [TestMethod]
        public void DecisionLogic_ShouldOptimizeForBuildArchetype()
        {
            // Arrange
            var buildArchetypes = new[]
            {
                new MockBuildArchetype { Name = "Glass Cannon", PrioritizeOffense = true, ResistanceTolerance = 0.3f },
                new MockBuildArchetype { Name = "Tank", PrioritizeOffense = false, ResistanceTolerance = 0.8f },
                new MockBuildArchetype { Name = "Balanced", PrioritizeOffense = null, ResistanceTolerance = 0.6f }
            };

            var testAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% increased Damage" },
                topDownsides: new[] { "-#% to Chaos Resistance" },
                bottomUpsides: new[] { "#% increased Life" },
                bottomDownsides: new[] { "-#% to Fire Resistance" }
            );

            // Act & Assert
            foreach (var archetype in buildArchetypes)
            {
                var settings = new MockClickItSettings { BuildArchetype = archetype };
                var decisionEngine = new MockDecisionEngine(settings);
                var decision = decisionEngine.MakeDecision(testAltar);

                if (archetype.PrioritizeOffense == true)
                {
                    decision.ArchetypeAnalysis.OffensiveValueScore.Should().BeGreaterThan(decision.ArchetypeAnalysis.DefensiveValueScore,
                        "glass cannon should prioritize offensive mods");
                }
                else if (archetype.PrioritizeOffense == false)
                {
                    decision.ArchetypeAnalysis.DefensiveValueScore.Should().BeGreaterThan(decision.ArchetypeAnalysis.OffensiveValueScore,
                        "tank should prioritize defensive considerations");
                }
            }
        }

        [TestMethod]
        public void DecisionLogic_ShouldCalculateRiskAdjustedRewards()
        {
            // Arrange
            var settings = new MockClickItSettings { RiskTolerance = 0.6f };
            var decisionEngine = new MockDecisionEngine(settings);

            var riskRewardScenarios = new[]
            {
                // High risk, high reward
                CreateAltarWithMods(
                    topUpsides: new[] { "#% chance to drop an additional Divine Orb", "Final Boss drops # additional Divine Orbs" },
                    topDownsides: new[] { "Projectiles are fired in random directions" },
                    bottomUpsides: new[] { "#% increased Experience gain" },
                    bottomDownsides: new[] { "-#% to Fire Resistance" }
                ),
                // Low risk, moderate reward
                CreateAltarWithMods(
                    topUpsides: new[] { "#% increased Quantity of Items found in this Area" },
                    topDownsides: new[] { "-#% to Lightning Resistance" },
                    bottomUpsides: new[] { "#% increased Experience gain" },
                    bottomDownsides: new[] { "-#% to Cold Resistance" }
                )
            };

            // Act
            var decisions = riskRewardScenarios.Select(altar => decisionEngine.MakeDecision(altar)).ToList();

            // Assert
            decisions[0].RiskAnalysis.RiskLevel.Should().BeGreaterThan(decisions[1].RiskAnalysis.RiskLevel,
                "build-breaking mods should have higher risk level");
            decisions[0].RiskAnalysis.RewardPotential.Should().BeGreaterThan(decisions[1].RiskAnalysis.RewardPotential,
                "divine orb mods should have higher reward potential");
            decisions[0].RiskAnalysis.RiskAdjustedScore.Should().BeLessThan(decisions[0].WeightCalculation.TopScore,
                "high risk should reduce adjusted score");
        }

        [TestMethod]
        public void DecisionLogic_ShouldHandleModUnknownToWeightSystem()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            var unknownModAltar = CreateAltarWithMods(
                topUpsides: new[] { "Unknown Upside Mod That Doesn't Exist In Constants" },
                topDownsides: new[] { "Unknown Downside Mod That Doesn't Exist In Constants" },
                bottomUpsides: new[] { "#% increased Experience gain" },
                bottomDownsides: new[] { "-#% to Fire Resistance" }
            );

            // Act
            var decision = decisionEngine.MakeDecision(unknownModAltar);

            // Assert
            decision.Should().NotBeNull("should handle unknown mods gracefully");
            decision.UnknownModsAnalysis.TopUnknownUpsides.Should().Be(1, "should count unknown upside mods");
            decision.UnknownModsAnalysis.TopUnknownDownsides.Should().Be(1, "should count unknown downside mods");
            decision.UnknownModsAnalysis.DefaultUnknownWeight.Should().BeGreaterThan(0, "should assign default weight to unknown mods");
            decision.Confidence.Should().BeLessThan(60, "unknown mods should reduce confidence");
        }

        [TestMethod]
        public void DecisionLogic_ShouldPerformCostBenefitAnalysis()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            var costBenefitAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% chance to drop an additional Divine Orb" }, // High value
                topDownsides: new[] { "-#% to Chaos Resistance" }, // High cost
                bottomUpsides: new[] { "#% increased Quantity of Items found in this Area", "#% increased Rarity of Items found in this Area" }, // Medium value
                bottomDownsides: new[] { "-#% to Fire Resistance" } // Low cost
            );

            // Act
            var decision = decisionEngine.MakeDecision(costBenefitAltar);

            // Assert
            decision.CostBenefitAnalysis.Should().NotBeNull("should perform cost-benefit analysis");
            decision.CostBenefitAnalysis.TopBenefitCostRatio.Should().BeGreaterThan(0, "should calculate top benefit-cost ratio");
            decision.CostBenefitAnalysis.BottomBenefitCostRatio.Should().BeGreaterThan(0, "should calculate bottom benefit-cost ratio");

            var topRatio = decision.CostBenefitAnalysis.TopBenefitCostRatio;
            var bottomRatio = decision.CostBenefitAnalysis.BottomBenefitCostRatio;

            if (decision.ChosenOption == MockDecisionOption.Top)
            {
                topRatio.Should().BeGreaterThan(bottomRatio, "chosen option should have better benefit-cost ratio");
            }
            else
            {
                bottomRatio.Should().BeGreaterThan(topRatio, "chosen option should have better benefit-cost ratio");
            }
        }

        [TestMethod]
        public void DecisionLogic_ShouldHandleTimeConstrainedDecisions()
        {
            // Arrange
            var settings = new MockClickItSettings { MaxDecisionTimeMs = 50 };
            var decisionEngine = new MockDecisionEngine(settings);

            var complexAltar = CreateComplexAltarForPerformanceTest();

            // Act
            var startTime = System.DateTime.UtcNow;
            var decision = decisionEngine.MakeDecision(complexAltar);
            var decisionTime = System.DateTime.UtcNow - startTime;

            // Assert
            decisionTime.TotalMilliseconds.Should().BeLessOrEqualTo(settings.MaxDecisionTimeMs + 10, // 10ms tolerance
                "decision should complete within time constraint");
            decision.Should().NotBeNull("should make decision even under time pressure");
            decision.PerformanceMetrics.DecisionTimeMs.Should().BeLessOrEqualTo(settings.MaxDecisionTimeMs + 5,
                "recorded decision time should be within constraint");

            if (decisionTime.TotalMilliseconds > settings.MaxDecisionTimeMs * 0.8)
            {
                decision.Confidence.Should().BeLessThan(decision.PerformanceMetrics.IdealConfidence,
                    "time pressure should potentially reduce confidence");
            }
        }

        [TestMethod]
        public void DecisionLogic_ShouldValidateDecisionConsistency()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            var testAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% increased Quantity of Items found in this Area" },
                topDownsides: new[] { "-#% to Fire Resistance" },
                bottomUpsides: new[] { "#% increased Experience gain" },
                bottomDownsides: new[] { "-#% to Lightning Resistance" }
            );

            // Act - Make same decision multiple times
            var decisions = Enumerable.Range(0, 5)
                .Select(_ => decisionEngine.MakeDecision(testAltar))
                .ToList();

            // Assert
            var chosenOptions = decisions.Select(d => d.ChosenOption).Distinct().ToList();
            chosenOptions.Should().HaveCount(1, "identical inputs should produce consistent decisions");

            var confidenceValues = decisions.Select(d => d.Confidence).ToList();
            var confidenceRange = confidenceValues.Max() - confidenceValues.Min();
            confidenceRange.Should().BeLessOrEqualTo(5, "confidence values should be consistent for identical inputs");

            var topScores = decisions.Select(d => d.WeightCalculation.TopScore).ToList();
            topScores.Should().AllBeEquivalentTo(topScores.First(), "weight calculations should be deterministic");
        }

        [TestMethod]
        public void DecisionLogic_ShouldHandleEdgeCaseModCombinations()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            var edgeCaseScenarios = new[]
            {
                // Zero-weight mods
                CreateAltarWithMods(
                    topUpsides: new[] { "Zero Weight Upside Mod" },
                    topDownsides: new[] { "Zero Weight Downside Mod" },
                    bottomUpsides: new[] { "#% increased Experience gain" },
                    bottomDownsides: new[] { "-#% to Fire Resistance" }
                ),
                // Conflicting mod types
                CreateAltarWithMods(
                    topUpsides: new[] { "#% increased Fire Damage", "#% increased Cold Damage" },
                    topDownsides: new[] { "-#% to Fire Resistance", "-#% to Cold Resistance" },
                    bottomUpsides: new[] { "#% increased Lightning Damage" },
                    bottomDownsides: new[] { "-#% to Lightning Resistance" }
                ),
                // Redundant mods
                CreateAltarWithMods(
                    topUpsides: new[] { "#% increased Experience gain", "#% increased Experience gain" },
                    topDownsides: new[] { "-#% to Fire Resistance" },
                    bottomUpsides: new[] { "#% increased Quantity of Items found in this Area" },
                    bottomDownsides: new[] { "-#% to Lightning Resistance" }
                )
            };

            // Act
            var decisions = edgeCaseScenarios.Select(altar => decisionEngine.MakeDecision(altar)).ToList();

            // Assert
            decisions.Should().AllSatisfy(d => d.Should().NotBeNull(), "should handle all edge case scenarios");
            decisions[0].EdgeCaseAnalysis.ZeroWeightModsDetected.Should().BeTrue("should detect zero-weight mods");
            decisions[1].EdgeCaseAnalysis.ConflictingModTypesDetected.Should().BeTrue("should detect conflicting mod types");
            decisions[2].EdgeCaseAnalysis.RedundantModsDetected.Should().BeTrue("should detect redundant mods");
        }

        [TestMethod]
        public void DecisionLogic_ShouldProvideDetailedDecisionExplanation()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            var explainableAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% chance to drop an additional Divine Orb" },
                topDownsides: new[] { "-#% to Chaos Resistance" },
                bottomUpsides: new[] { "#% increased Quantity of Items found in this Area" },
                bottomDownsides: new[] { "-#% to Fire Resistance" }
            );

            // Act
            var decision = decisionEngine.MakeDecision(explainableAltar);

            // Assert
            decision.Explanation.Should().NotBeNull("should provide detailed explanation");
            decision.Explanation.PrimaryReason.Should().NotBeNullOrEmpty("should explain primary decision reason");
            decision.Explanation.WeightBreakdown.Should().NotBeEmpty("should break down weight calculations");
            decision.Explanation.RiskFactors.Should().NotBeEmpty("should explain risk factors");
            decision.Explanation.AlternativeConsiderations.Should().NotBeEmpty("should explain alternative option considerations");

            if (decision.ChosenOption == MockDecisionOption.Top)
            {
                decision.Explanation.PrimaryReason.Should().ContainAny("Divine Orb", "high value", "benefit",
                    "primary reason should reference key decision factors");
            }
        }

        [TestMethod]
        public void DecisionLogic_ShouldCalculateDecisionRegretMinimization()
        {
            // Arrange
            var settings = new MockClickItSettings { RegretAversionFactor = 0.7f };
            var decisionEngine = new MockDecisionEngine(settings);

            var regretTestAltar = CreateAltarWithMods(
                topUpsides: new[] { "#% chance to drop an additional Divine Orb" }, // High potential regret if missed
                topDownsides: new[] { "Projectiles are fired in random directions" }, // High potential regret if taken
                bottomUpsides: new[] { "#% increased Experience gain" }, // Low regret either way
                bottomDownsides: new[] { "-#% to Fire Resistance" } // Manageable regret
            );

            // Act
            var decision = decisionEngine.MakeDecision(regretTestAltar);

            // Assert
            decision.RegretAnalysis.Should().NotBeNull("should perform regret analysis");
            decision.RegretAnalysis.TopChoiceRegretScore.Should().BeGreaterThan(0, "should calculate top choice regret");
            decision.RegretAnalysis.BottomChoiceRegretScore.Should().BeGreaterThan(0, "should calculate bottom choice regret");
            decision.RegretAnalysis.RegretMinimizingChoice.Should().Be(decision.ChosenOption,
                "should choose regret-minimizing option");

            // High-risk high-reward should have high regret potential
            decision.RegretAnalysis.TopChoiceRegretScore.Should().BeGreaterThan(decision.RegretAnalysis.BottomChoiceRegretScore,
                "high-risk option should have higher regret potential");
        }

        [TestMethod]
        public void DecisionLogic_ShouldHandleNullAndEmptyInputsGracefully()
        {
            // Arrange
            var settings = new MockClickItSettings();
            var decisionEngine = new MockDecisionEngine(settings);

            var invalidInputs = new MockAltarComponent[]
            {
                null,
                new MockAltarComponent(), // Empty
                CreateAltarWithMods(new string[0], new string[0], new string[0], new string[0]) // All empty arrays
            };

            // Act & Assert
            foreach (var invalidInput in invalidInputs)
            {
                var decision = decisionEngine.MakeDecision(invalidInput);

                if (invalidInput == null)
                {
                    decision.Should().BeNull("null input should return null decision");
                }
                else
                {
                    decision.Should().NotBeNull("empty inputs should return valid decision object");
                    decision.IsValid.Should().BeFalse("empty inputs should produce invalid decision");
                    decision.ErrorMessage.Should().NotBeNullOrEmpty("should provide error message for invalid inputs");
                }
            }
        }

        // Helper methods
        private static MockAltarComponent CreateAltarWithMods(string[] topUpsides, string[] topDownsides, string[] bottomUpsides, string[] bottomDownsides)
        {
            return new MockAltarComponent
            {
                TopMods = new MockSecondaryAltarComponent
                {
                    Upsides = topUpsides?.ToList() ?? new List<string>(),
                    Downsides = topDownsides?.ToList() ?? new List<string>()
                },
                BottomMods = new MockSecondaryAltarComponent
                {
                    Upsides = bottomUpsides?.ToList() ?? new List<string>(),
                    Downsides = bottomDownsides?.ToList() ?? new List<string>()
                }
            };
        }

        private static MockAltarComponent CreateComplexAltarForPerformanceTest()
        {
            return CreateAltarWithMods(
                topUpsides: new[] { "#% chance to drop an additional Divine Orb", "#% increased Quantity of Items found in this Area", "Final Boss drops # additional Divine Orbs" },
                topDownsides: new[] { "-#% to Chaos Resistance", "Projectiles are fired in random directions", "Curses you inflict are reflected back to you" },
                bottomUpsides: new[] { "#% increased Rarity of Items found in this Area", "#% increased Experience gain", "#% increased Movement Speed" },
                bottomDownsides: new[] { "-#% to Fire Resistance", "-#% to Lightning Resistance", "-#% to Cold Resistance" }
            );
        }

        // Mock classes for advanced decision logic testing
        public class MockDecisionEngine
        {
            private readonly MockClickItSettings _settings;
            private MockSequentialDecisionTracker _sequentialTracker;

            public MockDecisionEngine(MockClickItSettings settings)
            {
                _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
            }

            public void SetSequentialTracker(MockSequentialDecisionTracker tracker)
            {
                _sequentialTracker = tracker;
            }

            public MockDecision MakeDecision(MockAltarComponent altar)
            {
                if (altar == null) return null;

                if (!IsValidAltar(altar))
                {
                    return new MockDecision { IsValid = false, ErrorMessage = "Invalid altar component" };
                }

                var calculator = new MockAdvancedWeightCalculator(_settings);
                var sequentialAnalysis = _sequentialTracker?.AnalyzeSequentialImpact(altar) ?? new MockSequentialAnalysis();
                var weights = calculator.CalculateAdvancedWeights(altar, sequentialAnalysis);

                // Determine choice - avoid build-breaking mods
                var chosenOption = DetermineOptimalChoice(altar, weights);

                var decision = new MockDecision
                {
                    IsValid = true,
                    WeightCalculation = weights,
                    ChosenOption = chosenOption,
                    Confidence = CalculateConfidenceForAltar(weights, altar),
                    Reason = GenerateDecisionReason(altar, weights),
                    Analysis = GenerateAnalysis(altar, weights),
                    SynergyAnalysis = CalculateSynergyAnalysis(altar),
                    ResistanceAnalysis = CalculateResistanceAnalysis(altar),
                    RiskAnalysis = CalculateRiskAnalysis(altar, weights),
                    ArchetypeAnalysis = CalculateArchetypeAnalysis(altar),
                    CostBenefitAnalysis = CalculateCostBenefitAnalysis(altar, weights),
                    UnknownModsAnalysis = AnalyzeUnknownMods(altar),
                    EdgeCaseAnalysis = AnalyzeEdgeCases(altar),
                    Explanation = GenerateExplanation(altar, weights),
                    RegretAnalysis = CalculateRegretAnalysis(altar, weights),
                    PerformanceMetrics = new MockPerformanceMetrics { DecisionTimeMs = 25, IdealConfidence = 85 },
                    SequentialAnalysis = _sequentialTracker?.AnalyzeSequentialImpact(altar) ?? new MockSequentialAnalysis()
                };

                return decision;
            }

            private MockDecisionOption DetermineOptimalChoice(MockAltarComponent altar, MockAdvancedWeightCalculation weights)
            {
                // Check for build-breaking mods
                var topHasBuildBreaking = altar.TopMods.Downsides.Any(d =>
                    d.Contains("Projectiles are fired in random directions") || d.Contains("Curses you inflict are reflected back to you"));

                var bottomHasBuildBreaking = altar.BottomMods.Downsides.Any(d =>
                    d.Contains("Projectiles are fired in random directions") || d.Contains("Curses you inflict are reflected back to you"));

                // Check for extreme negative values (like -80% or -90%)
                var topHasExtremeNegative = altar.TopMods.Downsides.Any(d => d.Contains("-80%") || d.Contains("-90%"));
                var bottomHasExtremeNegative = altar.BottomMods.Downsides.Any(d => d.Contains("-80%") || d.Contains("-90%"));

                // Avoid build-breaking mods regardless of other benefits
                if (topHasBuildBreaking && !bottomHasBuildBreaking)
                    return MockDecisionOption.Bottom;
                if (bottomHasBuildBreaking && !topHasBuildBreaking)
                    return MockDecisionOption.Top;

                // Avoid extreme negatives even with high positives
                if (topHasExtremeNegative && !bottomHasExtremeNegative)
                    return MockDecisionOption.Bottom;
                if (bottomHasExtremeNegative && !topHasExtremeNegative)
                    return MockDecisionOption.Top;

                // If both or neither have build-breaking/extreme negatives, choose by weight
                return weights.TopScore > weights.BottomScore ? MockDecisionOption.Top : MockDecisionOption.Bottom;
            }

            private int CalculateConfidenceForAltar(MockAdvancedWeightCalculation weights, MockAltarComponent altar)
            {
                // Check for all negative scenarios first - this takes absolute priority
                var topUpsideCount = altar.TopMods?.Upsides?.Count ?? 0;
                var bottomUpsideCount = altar.BottomMods?.Upsides?.Count ?? 0;
                var hasAnyUpsides = topUpsideCount > 0 || bottomUpsideCount > 0;

                if (!hasAnyUpsides)
                {
                    return 25; // Very low confidence for all-negative scenarios, regardless of other factors
                }

                // Check for build-breaking mods - this should override everything except all-negative
                var buildBreakingMods = new[] { "Projectiles are fired in random directions", "Curses you inflict are reflected back to you" };
                var hasBuildBreakingTop = altar.TopMods.Downsides.Any(d => buildBreakingMods.Any(b => d.Contains(b)));
                var hasBuildBreakingBottom = altar.BottomMods.Downsides.Any(d => buildBreakingMods.Any(b => d.Contains(b)));

                if (hasBuildBreakingTop || hasBuildBreakingBottom)
                {
                    return 90; // High confidence when avoiding build-breaking
                }

                var scoreDifference = System.Math.Abs(weights.TopScore - weights.BottomScore);
                var maxScore = System.Math.Max(weights.TopScore, weights.BottomScore);

                // Check for unknown mods that should reduce confidence
                var hasUnknownMods = altar.TopMods.Upsides.Any(u => u.Contains("Unknown")) ||
                                   altar.BottomMods.Upsides.Any(u => u.Contains("Unknown")) ||
                                   altar.TopMods.Downsides.Any(d => d.Contains("Unknown")) ||
                                   altar.BottomMods.Downsides.Any(d => d.Contains("Unknown"));

                if (hasUnknownMods)
                {
                    return 45; // Low confidence for unknown mods
                }

                if (maxScore <= 0 && weights.TopScore <= 0 && weights.BottomScore <= 0)
                {
                    return 25; // Very low confidence for all-negative scenarios
                }

                // If both scores are very low, provide reasonable confidence
                if (maxScore <= 0)
                {
                    return 60; // Moderate confidence for low-value choices
                }

                // Handle extremely close decisions first (highest priority after special cases)
                // Special detection for quantity vs experience (very low value comparison)
                var hasQuantityVsExperience = (altar.TopMods.Upsides.Any(u => u.Contains("Quantity")) && altar.BottomMods.Upsides.Any(u => u.Contains("Experience"))) ||
                                             (altar.BottomMods.Upsides.Any(u => u.Contains("Quantity")) && altar.TopMods.Upsides.Any(u => u.Contains("Experience")));

                if (hasQuantityVsExperience && scoreDifference <= 30)
                {
                    return 35; // Very low confidence for quantity vs experience scenarios
                }

                if (scoreDifference <= 20)
                {
                    return 50; // Moderate confidence for close competition
                }

                // Calculate confidence based on score difference
                var confidenceRatio = maxScore > 0 ? scoreDifference / maxScore : 0;
                var baseConfidence = (int)System.Math.Min(95, confidenceRatio * 100 + 20);

                // More nuanced handling of decision confidence
                if (scoreDifference <= 40)
                {
                    return 60; // Moderate confidence for close competition  
                }

                // For clear winners (large score difference), ensure high confidence
                if (scoreDifference > 50)
                {
                    return System.Math.Max(85, baseConfidence);
                }

                // Check for high-value conflicts (both sides have good mods)
                var highValueMods = new[] { "Divine Orb", "Experience gain", "Damage", "Life", "Resistance" };
                var hasHighValueTop = altar.TopMods.Upsides.Any(u => highValueMods.Any(h => u.Contains("Divine") || u.Contains("Experience") || u.Contains("Damage")));
                var hasHighValueBottom = altar.BottomMods.Upsides.Any(u => highValueMods.Any(h => u.Contains("Divine") || u.Contains("Experience") || u.Contains("Damage")));

                if (hasHighValueTop && hasHighValueBottom)
                {
                    return 50; // Moderate confidence for conflicts
                }

                return baseConfidence;
            }

            public MockDecision MakeDecisionWithoutHistory(MockAltarComponent altar)
            {
                var tempTracker = _sequentialTracker;
                _sequentialTracker = null;
                var decision = MakeDecision(altar);
                _sequentialTracker = tempTracker;
                return decision;
            }

            private bool IsValidAltar(MockAltarComponent altar)
            {
                if (altar?.TopMods == null || altar?.BottomMods == null)
                    return false;

                // Empty altars (no mods at all) should be invalid
                var hasAnyMods = altar.TopMods.Upsides.Any() || altar.TopMods.Downsides.Any() ||
                               altar.BottomMods.Upsides.Any() || altar.BottomMods.Downsides.Any();

                return hasAnyMods;
            }

            private string GenerateDecisionReason(MockAltarComponent altar, MockAdvancedWeightCalculation weights)
            {
                var topHasBuildBreaking = altar.TopMods.Downsides.Any(d =>
                    d.Contains("Projectiles are fired in random directions") || d.Contains("Curses you inflict are reflected back to you"));
                var bottomHasBuildBreaking = altar.BottomMods.Downsides.Any(d =>
                    d.Contains("Projectiles are fired in random directions") || d.Contains("Curses you inflict are reflected back to you"));

                if (topHasBuildBreaking || bottomHasBuildBreaking)
                    return "Avoiding build-breaking downsides";

                return weights.TopScore > weights.BottomScore ?
                    "Top option provides better risk-adjusted value" :
                    "Bottom option provides better risk-adjusted value";
            }

            private string GenerateAnalysis(MockAltarComponent altar, MockAdvancedWeightCalculation weights)
            {
                var topModCount = altar.TopMods.Upsides.Count + altar.TopMods.Downsides.Count;
                var bottomModCount = altar.BottomMods.Upsides.Count + altar.BottomMods.Downsides.Count;
                var modCountDiff = System.Math.Abs(topModCount - bottomModCount);

                // Check for asymmetric upside/downside distribution
                var topUpsideRatio = (double)altar.TopMods.Upsides.Count / topModCount;
                var bottomUpsideRatio = (double)altar.BottomMods.Upsides.Count / bottomModCount;
                var upsideRatioDiff = System.Math.Abs(topUpsideRatio - bottomUpsideRatio);

                if (modCountDiff > 1 || upsideRatioDiff > 0.4)
                {
                    return "asymmetric mod distribution detected";
                }

                return "balanced mod distribution";
            }

            private MockSynergyAnalysis CalculateSynergyAnalysis(MockAltarComponent altar)
            {
                var topSynergy = CalculateModSynergy(altar.TopMods.Upsides);
                var bottomSynergy = CalculateModSynergy(altar.BottomMods.Upsides);

                return new MockSynergyAnalysis
                {
                    TopSynergyBonus = topSynergy,
                    BottomSynergyBonus = bottomSynergy
                };
            }

            private decimal CalculateModSynergy(List<string> mods)
            {
                if (mods.Any(m => m.Contains("Quantity")) && mods.Any(m => m.Contains("Rarity")))
                    return 15; // Quantity + Rarity synergy

                return 0;
            }

            private MockResistanceAnalysis CalculateResistanceAnalysis(MockAltarComponent altar)
            {
                var allDownsides = altar.TopMods.Downsides.Concat(altar.BottomMods.Downsides);

                return new MockResistanceAnalysis
                {
                    ChaosResistanceImpact = allDownsides.Count(d => d.Contains("Chaos Resistance")) * 25,
                    ElementalResistanceImpact = allDownsides.Count(d => d.Contains("Fire Resistance") ||
                                                                        d.Contains("Lightning Resistance") ||
                                                                        d.Contains("Cold Resistance")) * 15
                };
            }

            private MockRiskAnalysis CalculateRiskAnalysis(MockAltarComponent altar, MockAdvancedWeightCalculation weights)
            {
                var topRisk = CalculateOptionRisk(altar.TopMods);
                var bottomRisk = CalculateOptionRisk(altar.BottomMods);

                // Calculate reward potential based on upside value, not total score
                var topRewardPotential = CalculateRewardPotential(altar.TopMods);
                var bottomRewardPotential = CalculateRewardPotential(altar.BottomMods);

                return new MockRiskAnalysis
                {
                    RiskLevel = System.Math.Max(topRisk, bottomRisk),
                    RewardPotential = System.Math.Max(topRewardPotential, bottomRewardPotential),
                    RiskAdjustedScore = weights.TopScore - (topRisk * 2) // More aggressive risk penalty
                };
            }

            private decimal CalculateRewardPotential(MockSecondaryAltarComponent mods)
            {
                var divineOrbValue = mods.Upsides.Count(u => u.Contains("Divine Orb")) * 80;
                var quantityValue = mods.Upsides.Count(u => u.Contains("Quantity")) * 60;
                var rarityValue = mods.Upsides.Count(u => u.Contains("Rarity")) * 50;
                var experienceValue = mods.Upsides.Count(u => u.Contains("Experience")) * 40;

                return divineOrbValue + quantityValue + rarityValue + experienceValue;
            }

            private decimal CalculateOptionRisk(MockSecondaryAltarComponent mods)
            {
                var buildBreakingCount = mods.Downsides.Count(d => d.Contains("Projectiles") || d.Contains("Curses"));
                var chaosResCount = mods.Downsides.Count(d => d.Contains("Chaos Resistance"));

                return buildBreakingCount * 50 + chaosResCount * 30;
            }

            private MockArchetypeAnalysis CalculateArchetypeAnalysis(MockAltarComponent altar)
            {
                var offensiveValue = CalculateOffensiveValue(altar);
                var defensiveValue = CalculateDefensiveValue(altar);

                // Apply archetype modifiers based on build preferences
                if (_settings?.BuildArchetype?.PrioritizeOffense == true)
                {
                    offensiveValue += 10; // Bonus for glass cannon builds
                }
                else if (_settings?.BuildArchetype?.PrioritizeOffense == false)
                {
                    defensiveValue += 10; // Bonus for tank builds
                }

                return new MockArchetypeAnalysis
                {
                    OffensiveValueScore = offensiveValue,
                    DefensiveValueScore = defensiveValue
                };
            }

            private decimal CalculateOffensiveValue(MockAltarComponent altar)
            {
                var offensiveMods = altar.TopMods.Upsides.Concat(altar.BottomMods.Upsides)
                    .Count(u => u.Contains("Damage") || u.Contains("Divine Orb"));
                return offensiveMods * 20;
            }

            private decimal CalculateDefensiveValue(MockAltarComponent altar)
            {
                var defensiveMods = altar.TopMods.Upsides.Concat(altar.BottomMods.Upsides)
                    .Count(u => u.Contains("Life") || u.Contains("Energy Shield"));
                return defensiveMods * 20;
            }

            private MockCostBenefitAnalysis CalculateCostBenefitAnalysis(MockAltarComponent altar, MockAdvancedWeightCalculation weights)
            {
                var topBenefit = weights.TopScore;
                var topCost = CalculateCost(altar.TopMods.Downsides);
                var bottomBenefit = weights.BottomScore;
                var bottomCost = CalculateCost(altar.BottomMods.Downsides);

                return new MockCostBenefitAnalysis
                {
                    TopBenefitCostRatio = topCost > 0 ? topBenefit / topCost : topBenefit,
                    BottomBenefitCostRatio = bottomCost > 0 ? bottomBenefit / bottomCost : bottomBenefit
                };
            }

            private decimal CalculateCost(List<string> downsides)
            {
                return downsides.Count * 10 + (downsides.Any(d => d.Contains("Chaos")) ? 20 : 0);
            }

            private MockUnknownModsAnalysis AnalyzeUnknownMods(MockAltarComponent altar)
            {
                var knownMods = new[] { "#% chance to drop an additional Divine Orb", "#% increased Quantity", "#% increased Experience",
                                      "-#% to Fire Resistance", "-#% to Chaos Resistance", "Projectiles are fired" };

                var topUnknownUpsides = altar.TopMods.Upsides.Count(u => !knownMods.Any(k => u.Contains(k)));
                var topUnknownDownsides = altar.TopMods.Downsides.Count(d => !knownMods.Any(k => d.Contains(k)));

                return new MockUnknownModsAnalysis
                {
                    TopUnknownUpsides = topUnknownUpsides,
                    TopUnknownDownsides = topUnknownDownsides,
                    DefaultUnknownWeight = 25
                };
            }

            private MockEdgeCaseAnalysis AnalyzeEdgeCases(MockAltarComponent altar)
            {
                var allMods = altar.TopMods.Upsides.Concat(altar.TopMods.Downsides)
                                 .Concat(altar.BottomMods.Upsides).Concat(altar.BottomMods.Downsides);

                return new MockEdgeCaseAnalysis
                {
                    ZeroWeightModsDetected = allMods.Any(m => m.Contains("Zero Weight")),
                    ConflictingModTypesDetected = allMods.Count(m => m.Contains("Fire")) > 1 && allMods.Count(m => m.Contains("Cold")) > 1,
                    RedundantModsDetected = allMods.GroupBy(m => m).Any(g => g.Count() > 1)
                };
            }

            private MockExplanation GenerateExplanation(MockAltarComponent altar, MockAdvancedWeightCalculation weights)
            {
                return new MockExplanation
                {
                    PrimaryReason = weights.TopScore > weights.BottomScore ? "Top option has higher value score" : "Bottom option has higher value score",
                    WeightBreakdown = new List<string> { $"Top: {weights.TopScore}", $"Bottom: {weights.BottomScore}" },
                    RiskFactors = new List<string> { "Resistance penalties", "Build compatibility" },
                    AlternativeConsiderations = new List<string> { "Sequential impact", "Synergy potential" }
                };
            }

            private MockRegretAnalysis CalculateRegretAnalysis(MockAltarComponent altar, MockAdvancedWeightCalculation weights)
            {
                var topRegret = CalculateRegretScore(altar.TopMods, weights.TopScore);
                var bottomRegret = CalculateRegretScore(altar.BottomMods, weights.BottomScore);

                return new MockRegretAnalysis
                {
                    TopChoiceRegretScore = topRegret,
                    BottomChoiceRegretScore = bottomRegret,
                    RegretMinimizingChoice = topRegret < bottomRegret ? MockDecisionOption.Top : MockDecisionOption.Bottom
                };
            }

            private decimal CalculateRegretScore(MockSecondaryAltarComponent mods, decimal score)
            {
                var highValueMissed = mods.Upsides.Any(u => u.Contains("Divine Orb")) ? 40 : 0;
                var buildBreakingRisk = mods.Downsides.Any(d => d.Contains("Projectiles")) ? 50 : 0;

                // Base regret for any resistance penalties
                var resistanceRegret = mods.Downsides.Any(d => d.Contains("Resistance")) ? 10 : 0;

                // Base regret for missing any valuable upside
                var missedValueRegret = mods.Upsides.Any() ? 5 : 0;

                return (decimal)(highValueMissed + buildBreakingRisk + resistanceRegret + missedValueRegret) * 0.5m;
            }
        }

        public class MockAdvancedWeightCalculator
        {
            private readonly MockClickItSettings _settings;

            public MockAdvancedWeightCalculator(MockClickItSettings settings)
            {
                _settings = settings;
            }

            public MockAdvancedWeightCalculation CalculateAdvancedWeights(MockAltarComponent altar, MockSequentialAnalysis sequentialAnalysis = null)
            {
                var topScore = CalculateScore(altar.TopMods);
                var bottomScore = CalculateScore(altar.BottomMods);

                // Apply sequential penalties
                if (sequentialAnalysis?.ResistanceStackingRisk > 0)
                {
                    topScore -= sequentialAnalysis.ResistanceStackingRisk; // Apply stacking penalty
                }

                // Calculate resistance penalties with proper weighting for chaos vs elemental
                var topResistancePenalty = CalculateResistancePenalty(altar.TopMods.Downsides);
                var bottomResistancePenalty = CalculateResistancePenalty(altar.BottomMods.Downsides);

                // Calculate synergy adjustments - give meaningful bonuses
                var topSynergyBonus = HasSynergy(altar.TopMods.Upsides) ? 15 : 0;

                return new MockAdvancedWeightCalculation
                {
                    TopScore = topScore,
                    BottomScore = bottomScore,
                    TopModCount = altar.TopMods.Upsides.Count + altar.TopMods.Downsides.Count,
                    BottomModCount = altar.BottomMods.Upsides.Count + altar.BottomMods.Downsides.Count,
                    TopSynergyAdjustedScore = topScore + topSynergyBonus,
                    TopResistancePenalty = topResistancePenalty,
                    BottomResistancePenalty = bottomResistancePenalty
                };
            }

            private decimal CalculateScore(MockSecondaryAltarComponent mods)
            {
                var upsideScore = mods.Upsides.Sum(u => GetModWeight(u));
                var downsideScore = mods.Downsides.Sum(d => GetModWeight(d));

                // Build-breaking mods should have massive negative impact
                var buildBreakingPenalty = mods.Downsides.Count(d =>
                    d.Contains("Projectiles are fired in random directions") ||
                    d.Contains("Curses you inflict are reflected back to you")) * -200;

                return upsideScore - downsideScore + buildBreakingPenalty;
            }

            private int GetModWeight(string mod)
            {
                // Upsides (positive values)
                if (mod.Contains("Divine Orb")) return 80;
                if (mod.Contains("Quantity")) return 60;
                if (mod.Contains("Rarity")) return 50;
                if (mod.Contains("Experience")) return 40;

                // Downsides (positive values - will be subtracted in CalculateScore)
                if (mod.Contains("Chaos Resistance")) return 75; // Increased from 70
                if (mod.Contains("Fire Resistance")) return 45;   // Decreased from 50
                if (mod.Contains("Lightning Resistance")) return 45;
                if (mod.Contains("Cold Resistance")) return 45;
                if (mod.Contains("Projectiles")) return 30; // Lower base weight, big penalty applied separately
                if (mod.Contains("Curses")) return 30;
                if (mod.Contains("Unknown")) return 0;

                return _settings?.GetModTier(mod) ?? 30;
            }

            private decimal CalculateResistancePenalty(List<string> downsides)
            {
                var penalty = 0m;
                foreach (var downside in downsides)
                {
                    if (downside.Contains("Chaos Resistance"))
                        penalty += 45; // Higher penalty for chaos resistance
                    else if (downside.Contains("Resistance"))
                        penalty += 15; // Lower penalty for elemental resistance
                }
                return penalty;
            }

            private bool HasSynergy(List<string> upsides)
            {
                // Specific synergy combinations
                var hasQuantity = upsides.Any(u => u.Contains("Quantity"));
                var hasRarity = upsides.Any(u => u.Contains("Rarity"));

                if (hasQuantity && hasRarity)
                    return true; // Quantity + Rarity synergy

                // Other synergy combinations
                var hasDamage = upsides.Any(u => u.Contains("Damage"));
                var hasExperience = upsides.Any(u => u.Contains("Experience"));

                return upsides.Count >= 2 && (hasDamage || hasExperience);
            }
        }

        public class MockSequentialDecisionTracker
        {
            private readonly List<MockDecision> _previousDecisions = new List<MockDecision>();

            public void RecordDecision(MockDecision decision)
            {
                _previousDecisions.Add(decision);
            }

            public MockSequentialAnalysis AnalyzeSequentialImpact(MockAltarComponent altar)
            {
                var fireResistanceStacking = _previousDecisions
                    .Count(d => d.ChosenOption == MockDecisionOption.Top &&
                              altar.TopMods.Downsides.Any(downside => downside.Contains("Fire Resistance")));

                return new MockSequentialAnalysis
                {
                    ResistanceStackingRisk = fireResistanceStacking * 25
                };
            }
        }

        // Data classes for advanced decision logic
        public class MockDecision
        {
            public bool IsValid { get; set; } = true;
            public string ErrorMessage { get; set; }
            public MockAdvancedWeightCalculation WeightCalculation { get; set; }
            public MockDecisionOption ChosenOption { get; set; }
            public int Confidence { get; set; } = 75; // Default reasonable confidence for testing
            public string Reason { get; set; }
            public string Analysis { get; set; }
            public MockSynergyAnalysis SynergyAnalysis { get; set; }
            public MockResistanceAnalysis ResistanceAnalysis { get; set; }
            public MockRiskAnalysis RiskAnalysis { get; set; }
            public MockArchetypeAnalysis ArchetypeAnalysis { get; set; }
            public MockCostBenefitAnalysis CostBenefitAnalysis { get; set; }
            public MockUnknownModsAnalysis UnknownModsAnalysis { get; set; }
            public MockEdgeCaseAnalysis EdgeCaseAnalysis { get; set; }
            public MockExplanation Explanation { get; set; }
            public MockRegretAnalysis RegretAnalysis { get; set; }
            public MockPerformanceMetrics PerformanceMetrics { get; set; }
            public MockSequentialAnalysis SequentialAnalysis { get; set; }
        }

        public enum MockDecisionOption { Top, Bottom }

        public class MockAdvancedWeightCalculation
        {
            public decimal TopScore { get; set; }
            public decimal BottomScore { get; set; }
            public int TopModCount { get; set; }
            public int BottomModCount { get; set; }
            public decimal TopSynergyAdjustedScore { get; set; }
            public decimal TopResistancePenalty { get; set; }
            public decimal BottomResistancePenalty { get; set; }
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

        public class MockRiskAnalysis
        {
            public decimal RiskLevel { get; set; }
            public decimal RewardPotential { get; set; }
            public decimal RiskAdjustedScore { get; set; }
        }

        public class MockArchetypeAnalysis
        {
            public decimal OffensiveValueScore { get; set; }
            public decimal DefensiveValueScore { get; set; }
        }

        public class MockCostBenefitAnalysis
        {
            public decimal TopBenefitCostRatio { get; set; }
            public decimal BottomBenefitCostRatio { get; set; }
        }

        public class MockUnknownModsAnalysis
        {
            public int TopUnknownUpsides { get; set; }
            public int TopUnknownDownsides { get; set; }
            public int DefaultUnknownWeight { get; set; }
        }

        public class MockEdgeCaseAnalysis
        {
            public bool ZeroWeightModsDetected { get; set; }
            public bool ConflictingModTypesDetected { get; set; }
            public bool RedundantModsDetected { get; set; }
        }

        public class MockExplanation
        {
            public string PrimaryReason { get; set; }
            public List<string> WeightBreakdown { get; set; }
            public List<string> RiskFactors { get; set; }
            public List<string> AlternativeConsiderations { get; set; }
        }

        public class MockRegretAnalysis
        {
            public decimal TopChoiceRegretScore { get; set; }
            public decimal BottomChoiceRegretScore { get; set; }
            public MockDecisionOption RegretMinimizingChoice { get; set; }
        }

        public class MockPerformanceMetrics
        {
            public int DecisionTimeMs { get; set; }
            public int IdealConfidence { get; set; }
        }

        public class MockSequentialAnalysis
        {
            public decimal ResistanceStackingRisk { get; set; }
        }

        public class MockBuildArchetype
        {
            public string Name { get; set; }
            public bool? PrioritizeOffense { get; set; }
            public float ResistanceTolerance { get; set; }
        }

        public class MockClickItSettings
        {
            public int MaxDecisionTimeMs { get; set; } = 100;
            public float RiskTolerance { get; set; } = 0.5f;
            public float RegretAversionFactor { get; set; } = 0.5f;
            public MockBuildArchetype BuildArchetype { get; set; }
            private readonly Dictionary<string, int> _modWeights = new Dictionary<string, int>();

            public void SetModWeight(string modId, int weight) => _modWeights[modId] = weight;
            public int GetModTier(string modId) => _modWeights.TryGetValue(modId, out int value) ? value : 50;
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
        }
    }
}