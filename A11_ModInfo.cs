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

    public class ModInfo : IUserMod
    {
        public string Name { get { A11_Bootstrap.Ensure("IUserMod.Name"); return "CS1 LaneBalancer (DEV)"; } }
        public string Description { get { A11_Bootstrap.Ensure("IUserMod.Description"); return "HUD: " + BuildInfo.HudBuildId + " | MOD: " + BuildInfo.ModVersion + " | " + BuildInfo.ModStamp; } }
    }
}
