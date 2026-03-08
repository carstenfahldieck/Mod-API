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

    internal static class A11_PathSeg
    {
        private static bool _inited;
        private static System.Reflection.MethodInfo _pathUnit_GetPosition;
        private static bool _searched;

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
            TryFindGetPosition();
        }

        private static void TryFindGetPosition()
        {
            if (_searched) return;
            _searched = true;

            try
            {
                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic;

                var ms = typeof(PathUnit).GetMethods(flags);

                for (int i = 0; i < ms.Length; i++)
                {
                    var m = ms[i];
                    if (object.ReferenceEquals(m, null)) continue;
                    if (!string.Equals(m.Name, "GetPosition", StringComparison.Ordinal)) continue;

                    var ps = m.GetParameters();
                    if (object.ReferenceEquals(ps, null)) continue;
                    if (ps.Length != 2) continue;

                    if (!string.Equals(TN(ps[0].ParameterType), "System.Int32", StringComparison.Ordinal)) continue;

                    Type p1 = ps[1].ParameterType;
                    if (object.ReferenceEquals(p1, null) || !p1.IsByRef) continue;

                    Type e1 = p1.GetElementType();
                    if (!string.Equals(TN(e1), typeof(PathUnit.Position).FullName, StringComparison.Ordinal)) continue;

                    _pathUnit_GetPosition = m;
                    break;
                }

                A11_Log.Write("PathSeg: GetPosition " + (!object.ReferenceEquals(_pathUnit_GetPosition, null) ? "FOUND(int)" : "NOTFOUND"));
            }
            catch (Exception ex)
            {
                A11_Log.Write("PathSeg: TryFindGetPosition EX " + ex.GetType().Name + ":" + ex.Message);
            }
        }

        public static ushort TryGetCurrentSegment(ref Vehicle v)
        {
            EnsureInit();

            try
            {
                uint path = v.m_path;
                if (path == 0u) return 0;

                int posIndex = (int)v.m_pathPositionIndex;

                PathManager pm = PathManager.instance;
                if (object.ReferenceEquals(pm, null)) return 0;

                PathUnit[] units = pm.m_pathUnits.m_buffer;
                if (object.ReferenceEquals(units, null)) return 0;
                if (path >= (uint)units.Length) return 0;

                if (object.ReferenceEquals(_pathUnit_GetPosition, null)) return 0;

                PathUnit unit = units[path];
                PathUnit.Position p = new PathUnit.Position();

                object boxed = unit;
                object[] args = new object[] { posIndex, p };
                object ret = _pathUnit_GetPosition.Invoke(boxed, args);

                if (args.Length >= 2 && args[1] is PathUnit.Position)
                    p = (PathUnit.Position)args[1];

                if (ret is bool && (bool)ret)
                    return p.m_segment;
            }
            catch { }

            return 0;
        }

        // Expose full PathUnit.Position for lane diagnostics
        public static bool TryGetPathPosition(ushort vehicleID, ref Vehicle data, out PathUnit.Position pos)
        {
            pos = default(PathUnit.Position);
            EnsureInit();
            if (object.ReferenceEquals(_pathUnit_GetPosition, null)) return false;
            uint path = data.m_path;
            if (path == 0u) return false;

            try
            {
                PathManager pm = PathManager.instance;
                if (object.ReferenceEquals(pm, null)) return false;
                PathUnit[] buf = pm.m_pathUnits.m_buffer;
                if (object.ReferenceEquals(buf, null)) return false;
                if ((int)path < 0 || (int)path >= buf.Length) return false;

                PathUnit unit = buf[(int)path];
                object boxed = unit;
                object[] args = new object[2];
                args[0] = (int)data.m_pathPositionIndex;
                args[1] = pos;
                bool ok = false;
                object r = _pathUnit_GetPosition.Invoke(boxed, args);
                if (r is bool) ok = (bool)r;
                if (ok && args[1] is PathUnit.Position) pos = (PathUnit.Position)args[1];
                return ok;
            }
            catch
            {
                return false;
            }
        }

        public static Vector3 TryGetSegmentMid(ushort segmentId)
        {
            try
            {
                if (segmentId == 0) return Vector3.zero;
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return Vector3.zero;
                NetSegment s = nm.m_segments.m_buffer[segmentId];
                Vector3 p0 = nm.m_nodes.m_buffer[s.m_startNode].m_position;
                Vector3 p1 = nm.m_nodes.m_buffer[s.m_endNode].m_position;
                return (p0 + p1) * 0.5f;
            }
            catch
            {
            }
            return Vector3.zero;
        }

        public static bool TryGetSegmentMid(ushort segmentId, out Vector3 mid)
        {
            mid = default(Vector3);
            if (segmentId == 0) return false;
            mid = TryGetSegmentMid(segmentId);
            return true;
        }
    }
}
