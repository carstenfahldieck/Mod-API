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

    internal static class A11_NodeSegments
    {
        private static bool _inited;
        private static System.Reflection.FieldInfo[] _segFields;

        public static void EnsureInit()
        {
            if (_inited) return;
            _inited = true;

            try
            {
                Type t = typeof(NetNode);
                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic;

                _segFields = new System.Reflection.FieldInfo[8];
                for (int i = 0; i < 8; i++)
                {
                    string fn = "m_segment" + i.ToString();
                    _segFields[i] = t.GetField(fn, flags);
                }
            }
            catch
            {
                _segFields = null;
            }
        }

        public static ushort GetSegmentAt(ref NetNode node, int index)
        {
            EnsureInit();
            if (object.ReferenceEquals(_segFields, null)) return 0;
            if (index < 0 || index >= 8) return 0;

            try
            {
                var fi = _segFields[index];
                if (object.ReferenceEquals(fi, null)) return 0;
                object v = fi.GetValue(node);
                if (v is ushort) return (ushort)v;
            }
            catch { }

            return 0;
        }
    }
}
