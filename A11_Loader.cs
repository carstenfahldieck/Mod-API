using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICities;
using UnityEngine;

namespace CS1_LaneBalancer
{
    // ===============================================================
    // HUD läuft. Nicht anfassen.
    // Nur MOD.cs ändern.
    // Keine Experimente mit Runtime-Features.
    //
    // MOD_A11_HUD_OK_REPATH_060P_ONEKEY_SUMMARY
    // Stamp: 2026-03-01 22:10:00
    //
    // Phase 1 (Lane-Diagnose):
    // - Bei jedem (MAN/AUTO) Repath merken wir uns aus PathUnit.Position:
    //     * startLane (LaneIndex)
    //     * startOffset (Offset)
    // - Wir bauen ein einfaches Histogramm (Lane 0..7 + unknown),
    //   damit du im HUD siehst, ob wir wirklich "immer Spur 1" haben.
    //
    // Keys:
    //  F11 = MAN: seed+anchor+goals + 1 manual repath (quiet log)
    //  F10 = RUN (seed+anchor+goals + start capture 120s, quiet log)
    //  F7  = toggle corridorFilter
    //  F8  = clear seed
    //  F9  = toggle AUTO
    //  F12 = toggle hwyOnly
    //  F6  = toggle verbose per-vehicle log (for debugging)
    // ===============================================================

    public class Loader : LoadingExtensionBase
    {
        private GameObject _go;

        
public override void OnLevelLoaded(LoadMode mode)
{
    base.OnLevelLoaded(mode);

    A11_Bootstrap.Ensure("OnLevelLoaded");
    try
    {
        _go = new GameObject("CS1_LaneBalancer_ROOT");
        UnityEngine.Object.DontDestroyOnLoad(_go);

        // HUD framework (stable)
        _go.AddComponent<HudController>();

        // Logging
        A11_Log.Init();
        A11_Log.Write("=== Loaded " + BuildInfo.ModVersion + " " + BuildInfo.ModStamp + " ===");

        // Controllers
        _go.AddComponent<TestUpdater>();
        _go.AddComponent<A11_RepathController>();

        // Header: keep it minimal and DO NOT overwrite the HUD time line.
        HudData.SetLine0("CS1 LaneBalancer (HUD)");
        HudData.SetLine1("MOD: " + BuildInfo.ModVersion + " | " + BuildInfo.ModStamp);
    }
    catch (Exception ex)
    {
        try { UnityEngine.Debug.LogError("[LaneBalancer] OnLevelLoaded EX: " + ex.GetType().Name + ":" + ex.Message); } catch { }
    }
}

public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();
            try { A11_Log.Write("=== Unloading ==="); A11_Log.Flush(true); } catch { }

            if (!object.ReferenceEquals(_go, null))
            {
                UnityEngine.Object.Destroy(_go);
                _go = null;
            }
        }
    }
}
