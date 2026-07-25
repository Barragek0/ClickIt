namespace ClickIt.Tests.Features.Click
{
    [TestClass]
    public class UltimatumPanelUiQueryTests
    {
        [TestMethod]
        public void TryGetVisiblePanel_ReturnsFalse_AndLogs_WhenPanelIsMissingAndLoggingEnabled()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanel(
                gameController: null,
                logFailures: true,
                logs.Add,
                out var panel);

            ok.Should().BeFalse();
            panel.Should().BeNull();
            logs.Should().ContainSingle();
            logs[0].Should().Contain("UltimatumPanel not available");
        }

        [TestMethod]
        public void TryGetVisiblePanel_ReturnsFalse_WithoutLogs_WhenPanelIsMissingAndLoggingDisabled()
        {
            List<string> logs = [];

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanel(
                gameController: null,
                logFailures: false,
                logs.Add,
                out var panel);

            ok.Should().BeFalse();
            panel.Should().BeNull();
            logs.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetVisiblePanel_ReturnsFalse_AndLogs_WhenVisiblePanelObjectIsWrongRuntimeType()
        {
            List<string> logs = [];
            FakeGameControllerShim gameController = CreateTypedGameControllerWithVisibleFakePanel();

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanel(
                gameController,
                logFailures: true,
                logs.Add,
                out var panel);

            ok.Should().BeFalse();
            panel.Should().BeNull();
            logs.Should().ContainSingle().Which.Should().Be("[TryHandleUltimatumPanelUi] UltimatumPanel object is not the expected runtime type.");
        }

        [TestMethod]
        public void TryGetVisiblePanel_ReturnsFalse_WithoutLogs_WhenVisiblePanelObjectIsWrongRuntimeTypeAndLoggingDisabled()
        {
            List<string> logs = [];
            FakeGameControllerShim gameController = CreateTypedGameControllerWithVisibleFakePanel();

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanel(
                gameController,
                logFailures: false,
                logs.Add,
                out var panel);

            ok.Should().BeFalse();
            panel.Should().BeNull();
            logs.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetVisiblePanelObject_ReturnsFalse_AndLogs_WhenPanelExistsButIsNotVisible()
        {
            List<string> logs = [];
            var gameController = new FakeGameController
            {
                IngameState = new FakeIngameState
                {
                    IngameUi = new FakeIngameUi
                    {
                        UltimatumPanel = new FakeUltimatumPanel { IsVisible = false }
                    }
                }
            };

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanelObject(
                gameController,
                logFailures: true,
                logs.Add,
                out object? panel);

            ok.Should().BeFalse();
            panel.Should().BeNull();
            logs.Should().ContainSingle().Which.Should().Be("[TryHandleUltimatumPanelUi] UltimatumPanel exists but is not visible.");
        }

        [TestMethod]
        public void TryGetVisiblePanelObject_ReturnsFalse_WithoutLogs_WhenPanelExistsButIsNotVisibleAndLoggingDisabled()
        {
            List<string> logs = [];
            var gameController = new FakeGameController
            {
                IngameState = new FakeIngameState
                {
                    IngameUi = new FakeIngameUi
                    {
                        UltimatumPanel = new FakeUltimatumPanel { IsVisible = false }
                    }
                }
            };

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanelObject(
                gameController,
                logFailures: false,
                logs.Add,
                out object? panel);

            ok.Should().BeFalse();
            panel.Should().BeNull();
            logs.Should().BeEmpty();
        }

        [TestMethod]
        public void TryGetVisiblePanelObject_ReturnsTrue_WhenReflectionFriendlyGraphHasVisiblePanel()
        {
            object marker = new FakeUltimatumPanel { IsVisible = true };
            var gameController = new FakeGameController
            {
                IngameState = new FakeIngameState
                {
                    IngameUi = new FakeIngameUi
                    {
                        UltimatumPanel = marker
                    }
                }
            };

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanelObject(
                gameController,
                logFailures: true,
                static _ => { },
                out object? panel);

            ok.Should().BeTrue();
            panel.Should().BeSameAs(marker);
        }

        [TestMethod]
        public void TryGetVisiblePanelObject_ReturnsFalse_AndLogs_WhenPanelVisibilityCannotBeRead()
        {
            List<string> logs = [];
            var gameController = new FakeGameController
            {
                IngameState = new FakeIngameState
                {
                    IngameUi = new FakeIngameUi
                    {
                        UltimatumPanel = new object()
                    }
                }
            };

            bool ok = UltimatumPanelUiQuery.TryGetVisiblePanelObject(
                gameController,
                logFailures: true,
                logs.Add,
                out object? panel);

            ok.Should().BeFalse();
            panel.Should().BeNull();
            logs.Should().ContainSingle().Which.Should().Be("[TryHandleUltimatumPanelUi] UltimatumPanel exists but is not visible.");
        }

        public sealed class FakeGameController
        {
            public object? IngameState { get; set; }
        }

        public sealed class FakeIngameState
        {
            public object? IngameUi { get; set; }
        }

        public sealed class FakeIngameUi
        {
            public object? UltimatumPanel { get; set; }
        }

        public sealed class FakeUltimatumPanel
        {
            public bool IsVisible { get; set; }
        }

        public sealed class FakeGameControllerShim : GameController
        {
            public FakeGameControllerShim()
                : base(null!, null!, null!, null!)
            {
            }

            public new object? IngameState { get; set; }
        }

        private static FakeGameControllerShim CreateTypedGameControllerWithVisibleFakePanel()
        {
            FakeGameControllerShim gameController = (FakeGameControllerShim)RuntimeHelpers.GetUninitializedObject(typeof(FakeGameControllerShim));
            gameController.IngameState = new FakeIngameState
            {
                IngameUi = new FakeIngameUi
                {
                    UltimatumPanel = new FakeUltimatumPanel { IsVisible = true }
                }
            };

            return gameController;
        }
    }
}