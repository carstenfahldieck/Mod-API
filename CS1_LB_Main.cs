using System;
using UnityEngine;
using ColossalFramework.UI;
using ICities;

namespace TaxiMod
{
    public sealed class CS1_LaneBalancerMod : IUserMod
    {
        public string Name { get { return "CS1 LaneBalancer (Probe)"; } }
        public string Description { get { return "HUD/Hotkeys/Logging + PathFind probe (ProcessItem laneID)"; } }
    }

    public sealed class CS1_LaneBalancerLoader : LoadingExtensionBase
    {
        private GameObject _go;

        public override void OnLevelLoaded(LoadMode mode)
        {
            try
            {
                CS1_LB_Logger.Init();
                CS1_LB_Logger.Log("OnLevelLoaded: " + mode);

                CS1_LB_Patches.InstallOnce();

                if (_go != null)
                {
                    UnityEngine.Object.Destroy(_go);
                    _go = null;
                }

                _go = new GameObject("CS1_LB_Controller");
                _go.AddComponent<CS1_LB_Controller>();

                CS1_LB_Logger.Log("Controller created.");
            }
            catch (Exception ex)
            {
                CS1_LB_Logger.Log("ERROR OnLevelLoaded: " + ex);
            }
        }

        public override void OnLevelUnloading()
        {
            try
            {
                CS1_LB_Logger.Log("OnLevelUnloading");

                if (_go != null)
                {
                    UnityEngine.Object.Destroy(_go);
                    _go = null;
                }
            }
            catch (Exception ex)
            {
                CS1_LB_Logger.Log("ERROR OnLevelUnloading: " + ex);
            }
        }
    }

    internal sealed class CS1_LB_Controller : MonoBehaviour
    {
        private CS1_LB_HUD _hud;
        private float _nextHudUpdate;

        public void Start()
        {
            try
            {
                UIView view = UIView.GetAView();
                if (view != null)
                {
                    UIComponent c = view.AddUIComponent(typeof(CS1_LB_HUD));
                    _hud = c as CS1_LB_HUD;
                }

                CS1_LB_Logger.Log("Controller.Start OK. HUD=" + (_hud != null ? "yes" : "no"));
            }
            catch (Exception ex)
            {
                CS1_LB_Logger.Log("ERROR Controller.Start: " + ex);
            }
        }

        public void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F4))
                {
                    CS1_LB_State.Enabled = !CS1_LB_State.Enabled;
                    CS1_LB_Logger.Log("Toggle Enabled => " + CS1_LB_State.Enabled);
                }

                if (Input.GetKeyDown(KeyCode.F5))
                {
                    CS1_LB_State.ResetPathCounters();
                    CS1_LB_Logger.Log("ResetPathCounters");
                }

                if (CS1_LB_State.Enabled)
                {
                    CS1_LB_State.TickA++;
                    if ((CS1_LB_State.TickA % 10) == 0) CS1_LB_State.TickB++;
                    if ((CS1_LB_State.TickA % 60) == 0) CS1_LB_State.TickC++;
                }

                float t = Time.realtimeSinceStartup;
                if (t >= _nextHudUpdate)
                {
                    _nextHudUpdate = t + 0.25f;
                    if (_hud != null) _hud.Refresh();
                }
            }
            catch (Exception ex)
            {
                CS1_LB_Logger.Log("ERROR Update: " + ex);
            }
        }
    }
}
