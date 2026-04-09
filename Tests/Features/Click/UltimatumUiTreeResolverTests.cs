namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumUiTreeResolverTests
    {
        [TestMethod]
        public void GetUltimatumOptions_ReturnsEmptyAndLogs_WhenLabelIsNull()
        {
            List<string> diagnostics = [];

            List<(Element OptionElement, string ModifierName)> options = UltimatumUiTreeResolver.GetUltimatumOptions(null!, diagnostics);

            options.Should().BeEmpty();
            diagnostics.Should().ContainSingle().Which.Should().Be("Tree fail: label.Label is null.");
        }

        [TestMethod]
        public void GetUltimatumBeginButton_ReturnsNullAndLogs_WhenLabelIsNull()
        {
            List<string> diagnostics = [];

            Element? beginButton = UltimatumUiTreeResolver.GetUltimatumBeginButton(null!, diagnostics);

            beginButton.Should().BeNull();
            diagnostics.Should().ContainSingle().Which.Should().Be("Tree fail: label.Label is null.");
        }

        [TestMethod]
        public void TryExtractElement_ReturnsTrue_ForElementInstance()
        {
            Element element = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            bool ok = UltimatumUiTreeResolver.TryExtractElement(element, out Element? extracted);

            ok.Should().BeTrue();
            extracted.Should().BeSameAs(element);
        }

        [TestMethod]
        public void TryExtractElement_ReturnsFalse_ForNonElementObject()
        {
            bool ok = UltimatumUiTreeResolver.TryExtractElement(new object(), out Element? extracted);

            ok.Should().BeFalse();
            extracted.Should().BeNull();
        }

        [TestMethod]
        public void TryExtractElement_ReturnsTrue_ForProbeWithElementMember()
        {
            Element element = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            bool ok = UltimatumUiTreeResolver.TryExtractElement(
                new ReflectionFriendlyOption { Element = element },
                out Element? extracted);

            ok.Should().BeTrue();
            extracted.Should().BeSameAs(element);
        }

        [TestMethod]
        public void TryExtractElement_ReturnsTrue_ForProbeWithOptionElementMember()
        {
            Element element = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            bool ok = UltimatumUiTreeResolver.TryExtractElement(
                new ReflectionFriendlyChoiceOption { OptionElement = element },
                out Element? extracted);

            ok.Should().BeTrue();
            extracted.Should().BeSameAs(element);
        }

        [TestMethod]
        public void ExtractUltimatumModifierNames_NormalizesStringAndObjectEntries()
        {
            IReadOnlyList<string> names = UltimatumUiTreeResolver.ExtractUltimatumModifierNames(
                new object?[]
                {
                    "  Ruin\r\nII  ",
                    new ModifierNameProbe("  Stalking Ruin\nIII  ")
                });

            names.Should().Equal("Ruin II", "Stalking Ruin III");
        }

        [TestMethod]
        public void ExtractUltimatumModifierNames_ConvertsNullEntries_ToEmptyModifierNames()
        {
            IReadOnlyList<string> names = UltimatumUiTreeResolver.ExtractUltimatumModifierNames(
                new object?[]
                {
                    null,
                    new ModifierNameProbe("  Razor Dance  ")
                });

            names.Should().Equal(string.Empty, "Razor Dance");
        }

        [TestMethod]
        public void ExtractUltimatumModifierNames_ReturnsEmptyAndLogs_WhenModifiersMissing()
        {
            List<string> diagnostics = [];

            IReadOnlyList<string> names = UltimatumUiTreeResolver.ExtractUltimatumModifierNames(
                modifiersObj: null,
                diagnostics,
                missingModifiersMessage: "ChoicePanel: Modifiers missing.");

            names.Should().BeEmpty();
            diagnostics.Should().ContainSingle().Which.Should().Be("ChoicePanel: Modifiers missing.");
        }

        [TestMethod]
        public void ExtractUltimatumModifierNames_ReturnsEmpty_WhenModifierSequenceIsEmpty()
        {
            IReadOnlyList<string> names = UltimatumUiTreeResolver.ExtractUltimatumModifierNames(Array.Empty<object?>());

            names.Should().BeEmpty();
        }

        [TestMethod]
        public void ResolveUltimatumChoiceModifierName_PrefersPanelModifierName_WhenAvailable()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            string modifierName = UltimatumUiTreeResolver.ResolveUltimatumModifierName(option, 0, ["Ruin III"]);

            modifierName.Should().Be("Ruin III");
        }

        [TestMethod]
        public void GetUltimatumChoicePanelModifierNames_ReadsDynamicModifiers_FromReflectionFriendlyChoicePanel()
        {
            List<string> diagnostics = [];

            IReadOnlyList<string> names = InvokePrivate<IReadOnlyList<string>>(
                "GetUltimatumChoicePanelModifierNames",
                new ReflectionFriendlyChoicePanel
                {
                    Modifiers = new object?[]
                    {
                        "  Ruin\r\nII  ",
                        new ModifierNameProbe("  Stalking Ruin\nIII  ")
                    }
                },
                diagnostics);

            names.Should().Equal("Ruin II", "Stalking Ruin III");
            diagnostics.Should().BeEmpty();
        }

        [TestMethod]
        public void TryResolveChoicePanelObject_ReturnsVisibleReflectionFriendlyPanel()
        {
            List<string> diagnostics = [];
            var panel = new ReflectionFriendlyChoicePanel { IsVisible = true };

            object? resolved = InvokePrivateWithOutResult(
                "TryResolveChoicePanelObject",
                panel,
                diagnostics,
                out bool ok);

            ok.Should().BeTrue();
            resolved.Should().BeSameAs(panel);
            diagnostics.Should().BeEmpty();
        }

        [TestMethod]
        public void TryResolveChoicePanelObject_ReturnsFalse_WhenReflectionFriendlyPanelIsNotVisible()
        {
            List<string> diagnostics = [];

            object? resolved = InvokePrivateWithOutResult(
                "TryResolveChoicePanelObject",
                new ReflectionFriendlyChoicePanel { IsVisible = false },
                diagnostics,
                out bool ok);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            diagnostics.Should().ContainSingle().Which.Should().Be("ChoicePanel fail: panel object exists but is not visible.");
        }

        [TestMethod]
        public void TryResolveChoicePanelObject_ReturnsFalse_WhenPanelVisibilityIsUnavailable()
        {
            List<string> diagnostics = [];

            object? resolved = InvokePrivateWithOutResult(
                "TryResolveChoicePanelObject",
                new ReflectionFriendlyVisibilitylessPanel(),
                diagnostics,
                out bool ok);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            diagnostics.Should().ContainSingle().Which.Should().Be("ChoicePanel fail: panel visibility unavailable.");
        }

        [TestMethod]
        public void CollectChoicePanelOptions_UsesModifierNamesAndSkipsNonElementObjects()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            SeedElementAddress(option, 0x1234);

            List<string> diagnostics = [];
            List<(Element OptionElement, string ModifierName)> results = [];

            InvokePrivate(
                "CollectChoicePanelOptions",
                new object?[] { new object(), option },
                (IReadOnlyList<string>)["Ignored", "Ruin III"],
                diagnostics,
                results);

            diagnostics.Should().Contain(diagnostic => diagnostic == "ChoicePanel option[0] is not an Element.");
            results.Should().ContainSingle();
            results[0].OptionElement.Should().BeSameAs(option);
            results[0].ModifierName.Should().Be("Ruin III");
        }

        [TestMethod]
        public void CollectChoicePanelOptions_UsesFallbackName_WhenChoiceTextAndPriorityNameAreMissing()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            SeedElementAddress(option, 0x5678);

            List<string> diagnostics = [];
            List<(Element OptionElement, string ModifierName)> results = [];

            InvokePrivate(
                "CollectChoicePanelOptions",
                new object?[] { new ReflectionFriendlyChoiceOption { OptionElement = option } },
                Array.Empty<string>(),
                diagnostics,
                results);

            results.Should().ContainSingle();
            results[0].OptionElement.Should().BeSameAs(option);
            results[0].ModifierName.Should().Be("Unknown Option 1");
            diagnostics.Should().Contain(diagnostic => diagnostic.Contains("modifier='Unknown Option 1'", StringComparison.Ordinal));
        }

        [TestMethod]
        public void TryGetUltimatumOptionsFromChoicePanelObject_UsesReflectionFriendlyRootPath()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            SeedElementAddress(option, 0x1234);

            List<string> diagnostics = [];
            object? resultsObj = InvokePrivateWithOutResult(
                "TryGetUltimatumOptionsFromChoicePanelObject",
                CreateRootNode(
                    new ReflectionFriendlyChoicePanel
                    {
                        IsVisible = true,
                        Modifiers = new object?[] { "Ruin III" },
                        ChoiceElements = new object?[] { option }
                    }),
                diagnostics,
                out bool ok);

            ok.Should().BeTrue();
            List<(Element OptionElement, string ModifierName)> results = (List<(Element OptionElement, string ModifierName)>)resultsObj!;
            results.Should().ContainSingle();
            results[0].OptionElement.Should().BeSameAs(option);
            results[0].ModifierName.Should().Be("Ruin III");
        }

        [TestMethod]
        public void TryGetUltimatumOptionsFromChoicePanelObject_ReturnsFalseAndLogs_WhenChoiceElementsMissing()
        {
            List<string> diagnostics = [];
            object? resultsObj = InvokePrivateWithOutResult(
                "TryGetUltimatumOptionsFromChoicePanelObject",
                CreateRootNode(new ReflectionFriendlyChoicePanel { IsVisible = true }),
                diagnostics,
                out bool ok);

            ok.Should().BeFalse();
            List<(Element OptionElement, string ModifierName)> results = (List<(Element OptionElement, string ModifierName)>)resultsObj!;
            results.Should().BeEmpty();
            diagnostics.Should().Contain(diagnostic => diagnostic == "ChoicePanel fail: ChoiceElements missing.");
        }

        [TestMethod]
        public void TryGetPrimaryTreeBranch_ReturnsBranch_FromReflectionFriendlyRoot()
        {
            Element branch = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            List<string> diagnostics = [];

            object? resolved = InvokePrivateWithOutResult(
                "TryGetPrimaryTreeBranch",
                new ReflectionFriendlyNode
                {
                    Children = new object?[]
                    {
                        new ReflectionFriendlyNode
                        {
                            Children = new object?[] { branch }
                        }
                    }
                },
                diagnostics,
                out bool ok);

            ok.Should().BeTrue();
            resolved.Should().BeSameAs(branch);
            diagnostics.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetTreeOptionContainer_ReturnsContainer_FromReflectionFriendlyRoot()
        {
            Element container = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            List<string> diagnostics = [];

            object? resolved = InvokePrivateWithOutResult(
                "TryGetTreeOptionContainer",
                new ReflectionFriendlyNode
                {
                    Children = new object?[]
                    {
                        new ReflectionFriendlyNode
                        {
                            Children = new object?[]
                            {
                                new ReflectionFriendlyNode
                                {
                                    Children = new object?[]
                                    {
                                        null,
                                        null,
                                        new ReflectionFriendlyNode
                                        {
                                            Children = new object?[] { container }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                diagnostics,
                out bool ok);

            ok.Should().BeTrue();
            resolved.Should().BeSameAs(container);
            diagnostics.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetChoicePanelElement_ReturnsPanel_FromReflectionFriendlyRoot()
        {
            var panel = new ReflectionFriendlyChoicePanel { IsVisible = true };
            List<string> diagnostics = [];

            object? resolved = InvokePrivateWithOutResult(
                "TryGetChoicePanelElement",
                CreateRootNode(panel),
                diagnostics,
                out bool ok);

            ok.Should().BeTrue();
            resolved.Should().BeSameAs(panel);
            diagnostics.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetChoicePanelElement_ReturnsPanel_FromMethodBackedRoot()
        {
            var panel = new ReflectionFriendlyChoicePanel { IsVisible = true };
            List<string> diagnostics = [];

            object? resolved = InvokePrivateWithOutResult(
                "TryGetChoicePanelElement",
                CreateMethodBackedRootNode(panel),
                diagnostics,
                out bool ok);

            ok.Should().BeTrue();
            resolved.Should().BeSameAs(panel);
            diagnostics.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetChoicePanelElement_ReturnsFalseAndLogs_WhenPanelPathIsMissing()
        {
            List<string> diagnostics = [];

            object? resolved = InvokePrivateWithOutResult(
                "TryGetChoicePanelElement",
                new ReflectionFriendlyNode
                {
                    Children = new object?[]
                    {
                        new ReflectionFriendlyNode
                        {
                            Children = new object?[]
                            {
                                new ReflectionFriendlyNode
                                {
                                    Children = Array.Empty<object?>()
                                }
                            }
                        }
                    }
                },
                diagnostics,
                out bool ok);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            diagnostics.Should().ContainSingle().Which.Should().Be("ChoicePanel fail: Label->Child(0)->Child(0)->Child(2) is null.");
        }

        [TestMethod]
        public void TryGetTreeNode_ReturnsFalseAndLogs_WhenChildIsMissing()
        {
            List<string> diagnostics = [];

            object? resolved = InvokePrivateTreeNode(
                new ReflectionFriendlyNode { Children = Array.Empty<object?>() },
                diagnostics,
                "root->Child(3)",
                3,
                out bool ok);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            diagnostics.Should().ContainSingle().Which.Should().Be("Tree fail: root->Child(3) is null.");
        }

        [TestMethod]
        public void TryGetElementChildNode_ReturnsFalseAndLogs_WhenChildIsNotElement()
        {
            List<string> diagnostics = [];

            Element? resolved = InvokePrivateElementChildNode(
                new ReflectionFriendlyNode
                {
                    Children = new object?[]
                    {
                        new ReflectionFriendlyTextNode { Text = "not an element" }
                    }
                },
                diagnostics,
                "branch->Child(0)",
                0,
                out bool ok);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
            diagnostics.Should().ContainSingle().Which.Should().Be("Tree fail: branch->Child(0) is not an Element.");
        }

        [TestMethod]
        public void TryGetChildNode_ReturnsTrue_ForGetChildAtIndexProbe()
        {
            object marker = new object();

            object? child = InvokePrivateChildNode(
                new GetChildAtIndexNode(marker),
                0,
                out bool ok);

            ok.Should().BeTrue();
            child.Should().BeSameAs(marker);
        }

        [TestMethod]
        public void TryGetChildNode_ReturnsFalse_ForNegativeIndex()
        {
            object? child = InvokePrivateChildNode(new ReflectionFriendlyNode(), -1, out bool ok);

            ok.Should().BeFalse();
            child.Should().BeNull();
        }

        [TestMethod]
        public void TryGetNodeFromIndices_ReturnsTrue_ForReflectionFriendlyPath()
        {
            object marker = new object();

            object? resolved = InvokePrivateNodeFromIndices(
                new ReflectionFriendlyNode
                {
                    Children = new object?[]
                    {
                        new ReflectionFriendlyNode
                        {
                            Children = new object?[] { marker }
                        }
                    }
                },
                [0, 0],
                out bool ok);

            ok.Should().BeTrue();
            resolved.Should().BeSameAs(marker);
        }

        [TestMethod]
        public void TryGetNodeFromIndices_ReturnsFalse_WhenAnySegmentIsMissing()
        {
            object? resolved = InvokePrivateNodeFromIndices(
                new ReflectionFriendlyNode
                {
                    Children = new object?[]
                    {
                        new ReflectionFriendlyNode
                        {
                            Children = Array.Empty<object?>()
                        }
                    }
                },
                [0, 1],
                out bool ok);

            ok.Should().BeFalse();
            resolved.Should().BeNull();
        }

        [TestMethod]
        public void GetNormalizedNodeText_ReadsLabelAndCollapsesWhitespace()
        {
            string text = InvokePrivate<string>(
                "GetNormalizedNodeText",
                new LabelTextProbe { Label = "  Razor\r\n  Dance   II  " },
                256);

            text.Should().Be("Razor Dance II");
        }

        [TestMethod]
        public void ResolveTooltipNameNode_ReturnsNull_WhenTooltipPathIsIncomplete()
        {
            object? tooltipName = InvokePrivate<object?>(
                "ResolveTooltipNameNode",
                new ReflectionFriendlyOption
                {
                    Tooltip = new ReflectionFriendlyNode
                    {
                        Children = new object?[]
                        {
                            null,
                            new ReflectionFriendlyNode
                            {
                                Children = new object?[] { null, null }
                            }
                        }
                    }
                });

            tooltipName.Should().BeNull();
        }

        [TestMethod]
        public void TryGetModifierNameFromChildren_ReturnsFalse_WhenChildrenContainNoReadableText()
        {
            object?[] args =
            [
                new ReflectionFriendlyOption
                {
                    Children = new object?[]
                    {
                        new ReflectionFriendlyNode
                        {
                            Children = new object?[] { new object() }
                        }
                    }
                },
                null
            ];

            bool ok = (bool)GetMethod("TryGetModifierNameFromChildren").Invoke(null, args)!;

            ok.Should().BeFalse();
            args[1].Should().Be(string.Empty);
        }

        [TestMethod]
        public void ResolveModifierNameFromNode_UsesProbeTextBeforeTooltipFallback()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            string modifierName = InvokePrivate<string>(
                "ResolveModifierNameFromNode",
                new ReflectionFriendlyOption
                {
                    Element = option,
                    Text = "  Ruin\r\nII  "
                },
                option);

            modifierName.Should().Be("Ruin II");
        }

        [TestMethod]
        public void ResolveModifierNameFromNode_UsesChildText_WhenDirectTextIsMissing()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            string modifierName = InvokePrivate<string>(
                "ResolveModifierNameFromNode",
                new ReflectionFriendlyOption
                {
                    Element = option,
                    Children = new object?[]
                    {
                        new ReflectionFriendlyNode
                        {
                            Children = new object?[]
                            {
                                new ReflectionFriendlyTextNode { Text = "  Stalking Ruin\nIII  " }
                            }
                        }
                    }
                },
                option);

            modifierName.Should().Be("Stalking Ruin III");
        }

        [TestMethod]
        public void ResolveModifierNameFromNode_UsesTooltipFallback_WhenDirectAndChildTextAreMissing()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));

            string modifierName = InvokePrivate<string>(
                "ResolveModifierNameFromNode",
                new ReflectionFriendlyOption
                {
                    Element = option,
                    Tooltip = new ReflectionFriendlyNode
                    {
                        Children = new object?[]
                        {
                            null,
                            new ReflectionFriendlyNode
                            {
                                Children = new object?[]
                                {
                                    null,
                                    null,
                                    null,
                                    new ReflectionFriendlyTextNode { Text = "  Razor Dance\nII  " }
                                }
                            }
                        }
                    }
                },
                option);

            modifierName.Should().Be("Razor Dance II");
        }

        [TestMethod]
        public void CollectTreeOptions_UsesProbeTextAndFallbackName_FromReflectionFriendlyRoot()
        {
            Element option1 = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            Element option2 = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            Element option3 = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            SeedElementAddress(option1, 0x1111);
            SeedElementAddress(option2, 0x2222);
            SeedElementAddress(option3, 0x3333);

            List<string> diagnostics = [];
            List<(Element OptionElement, string ModifierName)> results = [];

            List<(Element OptionElement, string ModifierName)> resolved = InvokePrivate<List<(Element OptionElement, string ModifierName)>>(
                "CollectTreeOptions",
                CreateTreeRoot(
                    new ReflectionFriendlyOption
                    {
                        Element = option1,
                        Text = "  Ruin\r\nII  "
                    },
                    new ReflectionFriendlyOption
                    {
                        Element = option2,
                        Children = new object?[]
                        {
                            new ReflectionFriendlyNode
                            {
                                Children = new object?[]
                                {
                                    new ReflectionFriendlyTextNode { Text = "  Stalking Ruin\nIII  " }
                                }
                            }
                        }
                    },
                    new ReflectionFriendlyOption
                    {
                        Element = option3
                    }),
                diagnostics,
                results);

            resolved.Should().HaveCount(3);
            resolved[0].OptionElement.Should().BeSameAs(option1);
            resolved[0].ModifierName.Should().Be("Ruin II");
            resolved[1].OptionElement.Should().BeSameAs(option2);
            resolved[1].ModifierName.Should().Be("Stalking Ruin III");
            resolved[2].OptionElement.Should().BeSameAs(option3);
            resolved[2].ModifierName.Should().Be("Unknown Option 3");
            diagnostics.Should().Contain(diagnostic => diagnostic.Contains("using fallback name 'Unknown Option 3'", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CollectTreeOptions_UsesOptionElementWrapper_FromMethodBackedTree()
        {
            Element option = (Element)RuntimeHelpers.GetUninitializedObject(typeof(Element));
            SeedElementAddress(option, 0x9999);
            List<string> diagnostics = [];
            List<(Element OptionElement, string ModifierName)> results = [];

            List<(Element OptionElement, string ModifierName)> resolved = InvokePrivate<List<(Element OptionElement, string ModifierName)>>(
                "CollectTreeOptions",
                CreateMethodBackedTreeRoot(
                    new ReflectionFriendlyChoiceOption
                    {
                        OptionElement = option,
                        Text = "  Ruin\r\nIV  "
                    }),
                diagnostics,
                results);

            resolved.Should().ContainSingle();
            resolved[0].OptionElement.Should().BeSameAs(option);
            resolved[0].ModifierName.Should().Be("Ruin IV");
            diagnostics.Should().Contain(diagnostic => diagnostic.Contains("modifier='Ruin IV'", StringComparison.Ordinal));
        }

        private static ReflectionFriendlyNode CreateRootNode(object panel)
            => new()
            {
                Children = new object?[]
                {
                    new ReflectionFriendlyNode
                    {
                        Children = new object?[]
                        {
                            new ReflectionFriendlyNode
                            {
                                Children = new object?[] { null, null, panel }
                            }
                        }
                    }
                }
            };

        private static MethodBackedNode CreateMethodBackedRootNode(object panel)
            => new([
                new MethodBackedNode([
                    new MethodBackedNode([null, null, panel])
                ])
            ]);

        private static ReflectionFriendlyNode CreateTreeRoot(params object?[] options)
            => new()
            {
                Children = new object?[]
                {
                    new ReflectionFriendlyNode
                    {
                        Children = new object?[]
                        {
                            new ReflectionFriendlyNode
                            {
                                Children = new object?[]
                                {
                                    null,
                                    null,
                                    new ReflectionFriendlyNode
                                    {
                                        Children = new object?[]
                                        {
                                            new ReflectionFriendlyNode
                                            {
                                                Children = options
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

        private static MethodBackedNode CreateMethodBackedTreeRoot(params object?[] options)
            => new([
                new MethodBackedNode([
                    new MethodBackedNode([
                        null,
                        null,
                        new MethodBackedNode([
                            new MethodBackedNode(options)
                        ])
                    ])
                ])
            ]);

        private static void InvokePrivate(string methodName, params object?[] arguments)
            => GetMethod(methodName).Invoke(null, arguments);

        private static T InvokePrivate<T>(string methodName, params object?[] arguments)
            => (T)GetMethod(methodName).Invoke(null, arguments)!;

        private static object? InvokePrivateWithOutResult(string methodName, object? source, List<string> diagnostics, out bool ok)
        {
            object?[] args = [source, diagnostics, null];
            ok = (bool)GetMethod(methodName).Invoke(null, args)!;
            return args[2];
        }

        private static object? InvokePrivateTreeNode(object? parent, List<string> diagnostics, string path, int index, out bool ok)
        {
            object?[] args = [parent, diagnostics, path, index, null];
            ok = (bool)GetMethod("TryGetTreeNode").Invoke(null, args)!;
            return args[4];
        }

        private static Element? InvokePrivateElementChildNode(object? parent, List<string> diagnostics, string path, int index, out bool ok)
        {
            object?[] args = [parent, diagnostics, path, index, null];
            ok = (bool)GetMethod("TryGetElementChildNode").Invoke(null, args)!;
            return (Element?)args[4];
        }

        private static object? InvokePrivateChildNode(object? node, int index, out bool ok)
        {
            object?[] args = [node, index, null];
            ok = (bool)GetMethod("TryGetChildNode").Invoke(null, args)!;
            return args[2];
        }

        private static object? InvokePrivateNodeFromIndices(object? source, int[] indices, out bool ok)
        {
            object?[] args = [source, indices, null];
            ok = (bool)GetMethod("TryGetNodeFromIndices").Invoke(null, args)!;
            return args[2];
        }

        private static MethodInfo GetMethod(string methodName)
            => typeof(UltimatumUiTreeResolver).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!
               ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");

        private static void SeedElementAddress(Element element, long address)
        {
            if (RuntimeMemberAccessor.TrySetMember(element, "_address", address)
                || RuntimeMemberAccessor.TrySetMember(element, "address", address)
                || RuntimeMemberAccessor.TrySetMember(element, "<Address>k__BackingField", address))
                return;


            throw new InvalidOperationException("Unable to seed an Element address for diagnostics.");
        }

        private sealed class ModifierNameProbe(string value)
        {
            public override string ToString() => value;
        }

        public sealed class ReflectionFriendlyChoicePanel
        {
            public bool IsVisible { get; set; }

            public object? Modifiers { get; set; }

            public object? ChoiceElements { get; set; }
        }

        public sealed class ReflectionFriendlyVisibilitylessPanel
        {
            public object? ChoiceElements { get; set; }
        }

        public sealed class ReflectionFriendlyOption
        {
            public Element? Element { get; set; }

            public string? Text { get; set; }

            public object? Tooltip { get; set; }

            public IList? Children { get; set; }
        }

        public sealed class ReflectionFriendlyChoiceOption
        {
            public Element? OptionElement { get; set; }

            public string? Text { get; set; }
        }

        public sealed class ReflectionFriendlyNode
        {
            public IList? Children { get; set; }
        }

        public sealed class MethodBackedNode(object?[] children)
        {
            private readonly object?[] _children = children;

            public object? Child(int index)
                => index >= 0 && index < _children.Length ? _children[index] : null;
        }

        public sealed class ReflectionFriendlyTextNode
        {
            public string? Text { get; set; }
        }

        public sealed class LabelTextProbe
        {
            public string? Label { get; set; }
        }

        public sealed class GetChildAtIndexNode(object? child)
        {
            private readonly object? _child = child;

            public object? GetChildAtIndex(int index)
                => index == 0 ? _child : null;
        }
    }
}