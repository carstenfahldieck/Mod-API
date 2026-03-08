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

    internal static class A11_HighwayFilter
    {
        public static bool IsHighwaySegment(ushort segmentId)
        {
            if (segmentId == 0) return false;

            try
            {
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return false;

                NetInfo info = nm.m_segments.m_buffer[segmentId].Info;
                if (object.ReferenceEquals(info, null)) return false;

                string name = info.name;
                if (object.ReferenceEquals(name, null)) return false;

                if (name.IndexOf("Highway", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (name.IndexOf("Autobahn", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (name.IndexOf("Motorway", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (name.IndexOf("Freeway", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            catch { }

            return false;
        }

        public static bool IsRampSegmentName(string netInfoName)
        {
            if (object.ReferenceEquals(netInfoName, null)) return false;
            if (netInfoName.IndexOf("Ramp", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (netInfoName.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static string GetNetName(ushort segmentId)
        {
            try
            {
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return null;
                NetInfo info = nm.m_segments.m_buffer[segmentId].Info;
                if (object.ReferenceEquals(info, null)) return null;
                return info.name;
            }
            catch { }
            return null;
        }
    }
}
