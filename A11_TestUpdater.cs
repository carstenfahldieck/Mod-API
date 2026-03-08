using System;
using UnityEngine;

namespace CS1_LaneBalancer
{
    public sealed class TestUpdater : MonoBehaviour
    {
        private float _lastUpdate;

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastUpdate < 0.25f) return;
            _lastUpdate = now;

            try
            {
                HudData.ClearRows();

                //--------------------------------------------------
                // SEED
                //--------------------------------------------------

                HudRow rSeed = new HudRow("SEED");
                rSeed.Add("seg=" + A11_RepathController.CorridorSeedSeg);
                rSeed.Add("cnt=" + A11_RepathController.CorridorCount);
                HudData.AddRow(rSeed);

                //--------------------------------------------------
                // ANCHOR
                //--------------------------------------------------

                HudRow rAnc = new HudRow("ANC");
                rAnc.Add(A11_RepathController.AnchorName);
                rAnc.Add("d=" + A11_RepathController.AnchorDist);
                HudData.AddRow(rAnc);

                //--------------------------------------------------
                // GOAL
                //--------------------------------------------------

                HudRow rGoal = new HudRow("GOAL");
                rGoal.Add("seg=" + A11_RepathController.GoalSeg);
                rGoal.Add("lane=" + A11_RepathController.GoalRightPos);
                HudData.AddRow(rGoal);

                //--------------------------------------------------
                // LAST VEHICLE
                //--------------------------------------------------

                HudRow rLast = new HudRow("LAST");
                rLast.Add(A11_RepathController.LastSrc);
                rLast.Add("vid=" + A11_RepathController.LastVid);
                HudData.AddRow(rLast);

                //--------------------------------------------------
                // STATUS
                //--------------------------------------------------

                HudRow rStat = new HudRow("STAT");
                rStat.Add("CAP " + A11_RepathController.CaptureRemainHud + "s");
                rStat.Add("auto=" + (A11_RepathController.AutoEnabled ? "ON" : "OFF"));
                HudData.AddRow(rStat);
            }
            catch
            {
            }
        }
    }
}