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

    internal static class A11_Reflect
    {
        private static Type _cachedAiType;
        private static System.Reflection.MethodInfo _m2;
        private static System.Reflection.MethodInfo _m4;

        public static void ResetCache()
        {
            _cachedAiType = null;
            _m2 = null;
            _m4 = null;
        }

        private static string TN(Type t)
        {
            if (object.ReferenceEquals(t, null)) return "<null>";
            string s = t.FullName;
            if (object.ReferenceEquals(s, null)) return t.Name;
            return s;
        }

        private static void EnsureCached(VehicleAI ai)
        {
            if (object.ReferenceEquals(ai, null)) return;
            Type t = ai.GetType();

            if (object.ReferenceEquals(_cachedAiType, t)) return;

            _cachedAiType = t;
            _m2 = null;
            _m4 = null;

            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;

            var ms = t.GetMethods(flags);

            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i];
                if (object.ReferenceEquals(m, null)) continue;
                if (!string.Equals(m.Name, "StartPathFind", StringComparison.Ordinal)) continue;

                var ps = m.GetParameters();
                if (object.ReferenceEquals(ps, null)) continue;
                if (!string.Equals(TN(m.ReturnType), "System.Boolean", StringComparison.Ordinal)) continue;

                if (ps.Length == 2)
                {
                    if (string.Equals(TN(ps[0].ParameterType), "System.UInt16", StringComparison.Ordinal) &&
                        ps[1].ParameterType.IsByRef &&
                        string.Equals(TN(ps[1].ParameterType.GetElementType()), typeof(Vehicle).FullName, StringComparison.Ordinal))
                    {
                        _m2 = m;
                    }
                }
                else if (ps.Length == 4)
                {
                    if (string.Equals(TN(ps[0].ParameterType), "System.UInt16", StringComparison.Ordinal) &&
                        ps[1].ParameterType.IsByRef &&
                        string.Equals(TN(ps[1].ParameterType.GetElementType()), typeof(Vehicle).FullName, StringComparison.Ordinal) &&
                        string.Equals(TN(ps[2].ParameterType), typeof(Vector3).FullName, StringComparison.Ordinal) &&
                        string.Equals(TN(ps[3].ParameterType), typeof(Vector3).FullName, StringComparison.Ordinal))
                    {
                        _m4 = m;
                    }
                }
            }

            A11_Log.Write("StartPathFind cached for " + t.FullName + " m2=" + (!object.ReferenceEquals(_m2, null) ? "Y" : "N") + " m4=" + (!object.ReferenceEquals(_m4, null) ? "Y" : "N"));
        }

        public static bool StartPathFind_Try(VehicleAI ai, ushort vehicleId, ref Vehicle v, Vector3 startPos, Vector3 endPos, out int usedMode)
        {
            usedMode = 0;
            if (object.ReferenceEquals(ai, null)) return false;

            try
            {
                EnsureCached(ai);

                // ------------------------------------------------------------
                // IMPORTANT (060F):
                // Prefer the 4-arg StartPathFind overload when available,
                // otherwise our goal endPos steering is ignored.
                // ------------------------------------------------------------

                if (!object.ReferenceEquals(_m4, null))
                {
                    object[] args4 = new object[] { vehicleId, (object)v, startPos, endPos };
                    object ret4 = _m4.Invoke(ai, args4);

                    if (args4.Length >= 2 && args4[1] is Vehicle)
                        v = (Vehicle)args4[1];

                    if (ret4 is bool && (bool)ret4)
                    {
                        usedMode = 4;
                        return true;
                    }
                }

                // Fallback to 2-arg overload (no endPos steering)
                if (!object.ReferenceEquals(_m2, null))
                {
                    object[] args2 = new object[] { vehicleId, (object)v };
                    object ret2 = _m2.Invoke(ai, args2);

                    if (args2.Length >= 2 && args2[1] is Vehicle)
                        v = (Vehicle)args2[1];

                    if (ret2 is bool && (bool)ret2)
                    {
                        usedMode = 2;
                        return true;
                    }
                }

                // ------------------------------------------------------------
                // [A11-KEEP-COMMENTED] Old order (m2 first) - kept for reference
                // ------------------------------------------------------------
                // if (!object.ReferenceEquals(_m2, null))
                // {
                //     object[] args = new object[] { vehicleId, (object)v };
                //     object ret = _m2.Invoke(ai, args);
                //     if (args.Length >= 2 && args[1] is Vehicle) v = (Vehicle)args[1];
                //     if (ret is bool && (bool)ret) { usedMode = 2; return true; }
                // }
                // if (!object.ReferenceEquals(_m4, null))
                // {
                //     object[] args = new object[] { vehicleId, (object)v, startPos, endPos };
                //     object ret = _m4.Invoke(ai, args);
                //     if (args.Length >= 2 && args[1] is Vehicle) v = (Vehicle)args[1];
                //     if (ret is bool && (bool)ret) { usedMode = 4; return true; }
                // }
            }
            catch (Exception ex)
            {
                A11_Log.Write("StartPathFind invoke EX " + ex.GetType().Name + ":" + ex.Message);
            }

            return false;
        }
    }
}
