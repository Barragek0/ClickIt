using ImGuiNET;
using System.Numerics;
using ClickIt.Definitions;

namespace ClickIt
{
    public partial class ClickItSettings
    {
        private void DrawPanelSafe(string panelName, Action drawAction)
        {
            try
            {
                drawAction();
            }
            catch (Exception ex)
            {
                UiState.LastSettingsUiError = $"{panelName}: {ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[ClickItSettings UI Error] {UiState.LastSettingsUiError}{Environment.NewLine}{ex}");

                ImGui.Separator();
                ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), "Settings UI error caught");
                ImGui.TextWrapped(UiState.LastSettingsUiError);

                if (ImGui.Button($"Throw Last UI Error##{panelName}"))
                {
                    throw new InvalidOperationException(UiState.LastSettingsUiError, ex);
                }
            }
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
    }
}
