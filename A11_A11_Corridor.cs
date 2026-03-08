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

    internal static class A11_Corridor
    {
        private static bool _inited;
        private static bool[] _inCorridor;
        private static ushort[] _queue;

        public static ushort SeedSeg = 0;
        public static int CorridorCount = 0;

        public static ushort AnchorSeg = 0;
        public static string AnchorName = "-";
        public static float AnchorDist = -1f;

        private const int MAX_QUEUE = 2048;

        public static void EnsureInit()
        {
            if (_inited) return;
            _inited = true;

            try
            {
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return;
                int segLen = nm.m_segments.m_buffer.Length;
                _inCorridor = new bool[segLen];
                _queue = new ushort[MAX_QUEUE];
            }
            catch
            {
                _inCorridor = null;
                _queue = null;
            }
        }

        public static void ClearSeed()
        {
            SeedSeg = 0;
            CorridorCount = 0;
            AnchorSeg = 0;
            AnchorName = "-";
            AnchorDist = -1f;
            if (!object.ReferenceEquals(_inCorridor, null))
            {
                for (int i = 0; i < _inCorridor.Length; i++) _inCorridor[i] = false;
            }
        }

        public static bool IsInCorridor(ushort seg)
        {
            if (seg == 0) return false;
            if (object.ReferenceEquals(_inCorridor, null)) return false;
            if (seg >= (ushort)_inCorridor.Length) return false;
            return _inCorridor[seg];
        }

        private static void Clear()
        {
            if (object.ReferenceEquals(_inCorridor, null)) return;
            for (int i = 0; i < _inCorridor.Length; i++) _inCorridor[i] = false;
        }

        public static void Rebuild(ushort seedSeg, Vector3 seedPos)
        {
            EnsureInit();

            SeedSeg = seedSeg;
            CorridorCount = 0;
            AnchorSeg = 0;
            AnchorName = "-";
            AnchorDist = -1f;

            if (seedSeg == 0) return;
            if (object.ReferenceEquals(_inCorridor, null)) return;
            if (object.ReferenceEquals(_queue, null)) return;

            try
            {
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return;
                if (seedSeg >= (ushort)nm.m_segments.m_buffer.Length) return;

                Clear();

                if (!A11_HighwayFilter.IsHighwaySegment(seedSeg))
                    return;

                int qh = 0;
                int qt = 0;

                _inCorridor[seedSeg] = true;
                _queue[qt++] = seedSeg;

                while (qh < qt && qt < MAX_QUEUE)
                {
                    ushort seg = _queue[qh++];
                    NetSegment s = nm.m_segments.m_buffer[seg];

                    ushort n0 = s.m_startNode;
                    ushort n1 = s.m_endNode;

                    ExpandFromNode(nm, n0, ref qt);
                    ExpandFromNode(nm, n1, ref qt);
                }

                CorridorCount = qt;

                FindAnchorNearestRamp(nm, qt, seedPos);
            }
            catch { }
        }

        private static void FindAnchorNearestRamp(NetManager nm, int qt, Vector3 seedPos)
        {
            try
            {
                ushort best = 0;
                string bestName = null;
                float bestD = float.MaxValue;

                for (int i = 0; i < qt; i++)
                {
                    ushort seg = _queue[i];
                    if (seg == 0) continue;

                    string nmName = A11_HighwayFilter.GetNetName(seg);
                    if (!A11_HighwayFilter.IsRampSegmentName(nmName)) continue;

                    Vector3 mid = A11_PathSeg.TryGetSegmentMid(seg);
                    float d = (mid - seedPos).magnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = seg;
                        bestName = nmName;
                    }
                }

                if (best == 0)
                {
                    try
                    {
                        int maxSeg = (int)nm.m_segments.m_size;
                        for (int segI = 1; segI < maxSeg; segI++)
                        {
                            ushort seg2 = (ushort)segI;
                            if (seg2 == 0) continue;

                            NetSegment s2 = nm.m_segments.m_buffer[seg2];
                            if ((s2.m_flags & NetSegment.Flags.Created) == 0) continue;

                            string nmName2 = A11_HighwayFilter.GetNetName(seg2);
                            if (!A11_HighwayFilter.IsRampSegmentName(nmName2)) continue;

                            Vector3 mid2 = A11_PathSeg.TryGetSegmentMid(seg2);
                            float d2 = (mid2 - seedPos).magnitude;
                            if (d2 < bestD)
                            {
                                bestD = d2;
                                best = seg2;
                                bestName = nmName2;
                            }
                        }
                    }
                    catch { }
                }

                if (best != 0)
                {
                    AnchorSeg = best;
                    AnchorName = bestName;
                    AnchorDist = bestD;
                    if (object.ReferenceEquals(AnchorName, null)) AnchorName = "Ramp";
                    if (AnchorName.Length > 24) AnchorName = AnchorName.Substring(0, 24);
                }
                else
                {
                    AnchorSeg = 0;
                    AnchorName = "-";
                    AnchorDist = -1f;
                }
            }
            catch
            {
                AnchorSeg = 0;
                AnchorName = "-";
                AnchorDist = -1f;
            }
        }

        private static void ExpandFromNode(NetManager nm, ushort nodeId, ref int qt)
        {
            if (nodeId == 0) return;
            try
            {
                NetNode node = nm.m_nodes.m_buffer[nodeId];

                for (int i = 0; i < 8; i++)
                {
                    ushort otherSeg = A11_NodeSegments.GetSegmentAt(ref node, i);
                    if (otherSeg == 0) continue;
                    if (otherSeg >= (ushort)nm.m_segments.m_buffer.Length) continue;
                    if (_inCorridor[otherSeg]) continue;

                    bool ok = A11_HighwayFilter.IsHighwaySegment(otherSeg);
                    if (!ok)
                    {
                        string nmName = A11_HighwayFilter.GetNetName(otherSeg);
                        ok = A11_HighwayFilter.IsRampSegmentName(nmName);
                    }
                    if (!ok) continue;

                    _inCorridor[otherSeg] = true;
                    if (qt < MAX_QUEUE)
                        _queue[qt++] = otherSeg;
                }
            }
            catch { }
        }

        public static Vector3 GetAnchorPosOr(Vector3 fallback, ushort currentSeg)
        {
            try
            {
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return fallback;

                ushort useSeg = AnchorSeg != 0 ? AnchorSeg : currentSeg;
                if (useSeg == 0) return fallback;

                NetSegment s = nm.m_segments.m_buffer[useSeg];
                Vector3 p0 = nm.m_nodes.m_buffer[s.m_startNode].m_position;
                Vector3 p1 = nm.m_nodes.m_buffer[s.m_endNode].m_position;
                return (p0 + p1) * 0.5f;
            }
            catch { }
            return fallback;
        }
    }
}
