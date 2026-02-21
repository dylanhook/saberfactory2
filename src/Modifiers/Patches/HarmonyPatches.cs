using System.Collections.Generic;
using HarmonyLib;
using HMUI;
using IPA.Utilities;

namespace SaberFactory2.HarmonyPatches
{
    [HarmonyPatch(typeof(FlowCoordinator), "Activate")]
    public class ActivateFlowCoordinatorPatch
    {
        public static void Prefix()
        {
            Editor.Editor.Instance?.Close(true);
        }
    }

    [HarmonyPatch(typeof(FlowCoordinator), "Deactivate")]
    public class DeactivateFlowCoordinatorPatch
    {
        public static void Prefix()
        {
            Editor.Editor.Instance?.Close(true);
        }
    }

    [HarmonyPatch(typeof(GameplaySetupViewController), "RefreshContent")]
    public class GameplaySetupViewPatch
    {
        private const string TabName = "Sabers";
        public static bool EntryEnabled;
        public static int SaberPanelIdx = 4;
        public static void Postfix(TextSegmentedControl ____selectionSegmentedControl)
        {
            if (!EntryEnabled)
            {
                return;
            }
            var texts = ____selectionSegmentedControl.GetField<IReadOnlyList<string>, TextSegmentedControl>("_texts");
            var list = new List<string>(texts) { TabName };
            SaberPanelIdx = list.Count - 1;
            ____selectionSegmentedControl.SetTexts(list);
        }
    }

    [HarmonyPatch(typeof(GameplaySetupViewController), "SetActivePanel")]
    public class GameplaySetupViewSelectionPatch
    {
        public static bool Prefix(int panelIdx, int ____activePanelIdx, TextSegmentedControl ____selectionSegmentedControl)
        {
            if (!GameplaySetupViewPatch.EntryEnabled)
            {
                return true;
            }
            if (panelIdx == GameplaySetupViewPatch.SaberPanelIdx)
            {
                var cell =
                    ____selectionSegmentedControl.GetField<List<SegmentedControlCell>, SegmentedControl>("_cells")[
                        GameplaySetupViewPatch.SaberPanelIdx];
                ____selectionSegmentedControl.SelectCellWithNumber(____activePanelIdx);
                Editor.Editor.Instance?.Open();
                cell.ClearHighlight(SelectableCell.TransitionType.Instant);
                return false;
            }
            return true;
        }
    }
}