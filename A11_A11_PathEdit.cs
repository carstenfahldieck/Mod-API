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

    public class A11_PathEdit
        {
            private static bool _inited;
            private static System.Reflection.MethodInfo _getPos;
            private static System.Reflection.MethodInfo _setPos;
            private static System.Reflection.FieldInfo _nextField;

            private static string TN(Type t)
            {
                if (object.ReferenceEquals(t, null)) return "<null>";
                string s = t.FullName;
                if (object.ReferenceEquals(s, null)) return t.Name;
                return s;
            }

            public static void EnsureInit()
            {
                if (_inited) return;
                _inited = true;

                try
                {
                    Type t = typeof(PathUnit);
                    var flags = System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.Public |
                                System.Reflection.BindingFlags.NonPublic;

                    // GetPosition(int, out PathUnit.Position)
                    var ms = t.GetMethods(flags);
                    for (int i = 0; i < ms.Length; i++)
                    {
                        var m = ms[i];
                        if (object.ReferenceEquals(m, null)) continue;
                        if (!string.Equals(m.Name, "GetPosition", StringComparison.Ordinal)) continue;
                        var ps = m.GetParameters();
                        if (object.ReferenceEquals(ps, null) || ps.Length != 2) continue;
                        if (!string.Equals(TN(ps[0].ParameterType), "System.Int32", StringComparison.Ordinal)) continue;
                        Type p1 = ps[1].ParameterType;
                        if (object.ReferenceEquals(p1, null) || !p1.IsByRef) continue;
                        Type e1 = p1.GetElementType();
                        if (!string.Equals(TN(e1), typeof(PathUnit.Position).FullName, StringComparison.Ordinal)) continue;
                        _getPos = m;
                        break;
                    }

                    // SetPosition(int, PathUnit.Position)
                    for (int i = 0; i < ms.Length; i++)
                    {
                        var m = ms[i];
                        if (object.ReferenceEquals(m, null)) continue;
                        if (!string.Equals(m.Name, "SetPosition", StringComparison.Ordinal)) continue;
                        var ps = m.GetParameters();
                        if (object.ReferenceEquals(ps, null) || ps.Length != 2) continue;
                        if (!string.Equals(TN(ps[0].ParameterType), "System.Int32", StringComparison.Ordinal)) continue;
                        if (!string.Equals(TN(ps[1].ParameterType), typeof(PathUnit.Position).FullName, StringComparison.Ordinal)) continue;
                        if (!string.Equals(TN(m.ReturnType), "System.Void", StringComparison.Ordinal)) continue;
                        _setPos = m;
                        break;
                    }

                    _nextField = t.GetField("m_nextPathUnit", flags);

                    A11_Log.Write("PathEdit: GetPos=" + (!object.ReferenceEquals(_getPos, null) ? "Y" : "N") +
                                  " SetPos=" + (!object.ReferenceEquals(_setPos, null) ? "Y" : "N") +
                                  " Next=" + (!object.ReferenceEquals(_nextField, null) ? "Y" : "N"));
                }
                catch (Exception ex)
                {
                    A11_Log.Write("PathEdit: init EX " + ex.GetType().Name + ":" + ex.Message);
                }
            }

            public static uint GetNext(PathUnit u)
            {
                try
                {
                    if (object.ReferenceEquals(_nextField, null)) return 0u;
                    object v = _nextField.GetValue(u);
                    if (v is uint) return (uint)v;
                }
                catch { }
                return 0u;
            }

            public static bool TryGetPosition(PathUnit u, int i, out PathUnit.Position p)
            {
                p = default(PathUnit.Position);
                if (object.ReferenceEquals(_getPos, null)) return false;

                try
                {
                    object boxed = (object)u;
                    object[] args = new object[2];
                    args[0] = i;
                    args[1] = p;
                    object r = _getPos.Invoke(boxed, args);
                    bool ok = (r is bool) ? (bool)r : false;
                    if (ok && args[1] is PathUnit.Position) p = (PathUnit.Position)args[1];
                    return ok;
                }
                catch { }
                return false;
            }

            public static bool TrySetPosition(ref object boxedUnit, int i, PathUnit.Position p)
            {
                if (object.ReferenceEquals(_setPos, null)) return false;
                try
                {
                    _setPos.Invoke(boxedUnit, new object[] { i, p });
                    return true;
                }
                catch { }
                return false;
            }
        }
}
