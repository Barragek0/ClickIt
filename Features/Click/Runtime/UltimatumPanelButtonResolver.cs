using ExileCore.PoEMemory;

namespace ClickIt.Features.Click.Runtime
{
    internal static class UltimatumPanelButtonResolver
    {
        internal static bool TryResolveTakeRewardsButton(
            Element? takeRewardsElement,
            Action<string> debugLog,
            out Element resolved)
        {
            if (takeRewardsElement == null)
            {
                debugLog("[TryClickUltimatumPanelTakeRewards] Take Rewards button missing at UltimatumPanel.Child(1).Child(4).Child(0).");
                resolved = null!;
                return false;
            }

            if (!takeRewardsElement.IsValid || !takeRewardsElement.IsVisible)
            {
                debugLog($"[TryClickUltimatumPanelTakeRewards] Take Rewards button ignored - valid={takeRewardsElement.IsValid}, visible={takeRewardsElement.IsVisible}");
                resolved = null!;
                return false;
            }

            resolved = takeRewardsElement;
            return true;
        }

        internal static bool TryResolveConfirmButton(
            object? confirmObj,
            Action<string> debugLog,
            out Element resolved)
        {
            if (confirmObj == null)
            {
                debugLog("[TryClickUltimatumPanelConfirm] ConfirmButton missing.");
                resolved = null!;
                return false;
            }

            if (!UltimatumUiTreeResolver.TryExtractElement(confirmObj, out Element? confirmElement) || confirmElement == null)
            {
                debugLog("[TryClickUltimatumPanelConfirm] ConfirmButton is not an Element.");
                resolved = null!;
                return false;
            }

            if (!confirmElement.IsValid || !confirmElement.IsVisible)
            {
                debugLog($"[TryClickUltimatumPanelConfirm] ConfirmButton ignored - valid={confirmElement.IsValid}, visible={confirmElement.IsVisible}");
                resolved = null!;
                return false;
            }

            resolved = confirmElement;
            return true;
        }
    }
}