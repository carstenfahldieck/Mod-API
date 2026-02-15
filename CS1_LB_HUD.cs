// CS1_LB_HUD.cs
// Nur HUD – größer gemacht, damit alles sichtbar ist.

using UnityEngine;
using ColossalFramework.UI;

namespace TaxiMod
{
    internal sealed class CS1_LB_HUD : UIPanel
    {
        private UILabel _label;

        public override void Start()
        {
            base.Start();

            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(0, 0, 0, 180);

            // ⬇️ HÖHE VERGRÖSSERT
            this.width = 600f;
            this.height = 190f;

            this.relativePosition = new Vector3(10f, 80f);

            _label = this.AddUIComponent<UILabel>();
            _label.autoSize = false;
            _label.width = this.width - 20f;
            _label.height = this.height - 20f;
            _label.relativePosition = new Vector3(10f, 10f);
            _label.textScale = 1.0f;
            _label.wordWrap = true;

            Refresh();
        }

        public void Refresh()
        {
            if (_label == null) return;

            _label.text =
                "CS1 LaneBalancer (Probe)\n" +
                "F4 Toggle | F5 Reset Path Counters\n" +
                "Enabled: " + (CS1_LB_State.Enabled ? "YES" : "NO") +
                "\nTick: A=" + CS1_LB_State.TickA +
                "  B=" + CS1_LB_State.TickB +
                "  C=" + CS1_LB_State.TickC +
                "\nPathFind(ProcessItem): Calls=" + CS1_LB_State.PF_ProcessItemCalls +
                "  LaneHits=" + CS1_LB_State.PF_ProcessItemLaneHits +
                "\nLast: seg=" + CS1_LB_State.PF_LastSegId +
                "  laneID=" + CS1_LB_State.PF_LastLaneId +
                "\nXOR: segX=" + CS1_LB_State.PF_SegIdXor +
                "  laneX=" + CS1_LB_State.PF_LaneIdXor;
        }
    }
}
