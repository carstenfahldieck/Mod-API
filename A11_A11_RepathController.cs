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

    public sealed partial class A11_RepathController : MonoBehaviour
    {
        private const int TRACK_MAX = 256;

        private struct TrackEntry
        {
            public ushort vid;
            public uint startTick;
            public ushort startSeg;
            public byte startLane;
            public byte startOffset;
            public ushort anchorSeg;
            public ushort anchorNode;
            public Vector3 anchorPos;
            public Vector3 anchorNodePos;
            public float startDist;
            public float bestDist;
            public byte active;
            public byte reached;
            public byte dropped;
        }

        private static ushort PickAnchorNode(ushort seg, Vector3 refPos)
        {
            if (seg == 0) return 0;
            NetManager nm = NetManager.instance;
            if (object.ReferenceEquals(nm, null)) return 0;

            NetSegment s = nm.m_segments.m_buffer[seg];
            ushort a = s.m_startNode;
            ushort b = s.m_endNode;

            if (a == 0) return b;
            if (b == 0) return a;

            Vector3 pa = nm.m_nodes.m_buffer[a].m_position;
            Vector3 pb = nm.m_nodes.m_buffer[b].m_position;

            float da = (pa - refPos).sqrMagnitude;
            float db = (pb - refPos).sqrMagnitude;
            return (da <= db) ? a : b;
        }

        private static Vector3 GetNodePos(ushort node)
        {
            if (node == 0) return default(Vector3);
            NetManager nm = NetManager.instance;
            if (object.ReferenceEquals(nm, null)) return default(Vector3);
            return nm.m_nodes.m_buffer[node].m_position;
        }

        private TrackEntry[] _track = new TrackEntry[TRACK_MAX];
        private int _trackWrite = 0;

        private int _trkActive = 0;
        private int _trkReached = 0;
        private int _trkDropped = 0;
        private int _trkProgress = 0;

        // Phase 1 lane histogram
        private int[] _laneHist = new int[9]; // 0..7 + 8=unknown
        private int _laneTotal = 0;
        private byte _lastLane = 255;
        private byte _lastOff = 255;

        public static string LaneHistHud = "hist: -";
        public static string LastLaneHud = "last: -";

        // Phase 2: lane-goal picking (up to 10 goal lane lateral positions on a target segment after the ramp)
        private const int GOAL_MAX = 10;
        private float[] _goalLanePos = new float[GOAL_MAX];
        private byte[] _goalLaneIdx = new byte[GOAL_MAX];
        private int _goalCount = 0;
        private int _goalNext = 0;
        private ushort _goalSeg = 0;
        private float _goalRightMost = 0f;
        private ushort _goalExitNode = 0;
        private ushort _goalFromNode = 0; // node on goalSeg closer to exit
        private ushort _goalToNode = 0;   // other node on goalSeg (away from exit)
        private int _goalDepth = 0;
        private string _goalSegName = "-";

        // Goal assignment per vehicle (prevents lane hopping between repaths)
        private struct GoalAssign
        {
            public int idx;
            public uint untilFrame;
        }
        private Dictionary<ushort, GoalAssign> _goalAssign = new Dictionary<ushort, GoalAssign>(4096);
        private const uint GOAL_ASSIGN_TTL_FRAMES = 4096u; // ~some seconds; safe for old runtime

        // Phase 4: remember which node is the EXIT-side node of the ramp anchor (so we can apply goals near the exit)
        private ushort _exitNode = 0;
        private Vector3 _exitNodePos = default(Vector3);

        private void LaneHistReset()
        {
            for (int i = 0; i < _laneHist.Length; i++) _laneHist[i] = 0;
            _laneTotal = 0;
            _lastLane = 255;
            _lastOff = 255;
            LaneHistHud = "hist: -";
            LastLaneHud = "last: -";
        }

        private void LaneHistAdd(byte lane, byte off)
        {
            _lastLane = lane;
            _lastOff = off;

            int idx = (lane == 255) ? 8 : (lane < 8 ? lane : 8);
            _laneHist[idx]++;
            _laneTotal++;

            // compact HUD
            LaneHistHud = "n=" + _laneTotal +
                          " L0=" + _laneHist[0] +
                          " L1=" + _laneHist[1] +
                          " L2=" + _laneHist[2] +
                          " L3+=" + (_laneHist[3] + _laneHist[4] + _laneHist[5] + _laneHist[6] + _laneHist[7]) +
                          " ?=" + _laneHist[8];

            LastLaneHud = "last lane=" + (lane == 255 ? "?" : lane.ToString()) + " off=" + (off == 255 ? "?" : off.ToString());
        }


        private void RecordLaneDiag(ushort vehicleId, ref Vehicle v)
        {
            try
            {
                byte ln = 255;
                byte off = 255;
                PathUnit.Position p;
                if (A11_PathSeg.TryGetPathPosition(vehicleId, ref v, out p))
                {
                    ln = p.m_lane;
                    off = p.m_offset;
                }
                LaneHistAdd(ln, off);
            }
            catch { }
        }


        // ------------------------------------------------------------
        // Phase 2: Build lane-goal positions on a "target segment" that
        // is connected to the ramp anchor (usually the city connector).
        // We then use those positions as endPos for StartPathFind to
        // encourage different lanes (cheap heuristic).
        // ------------------------------------------------------------




        
        // ------------------------------------------------------------
        // Phase 4: Fix "which side of the ramp is the city side?"
        //
        // Problem seen in logs: goals sometimes build on a segment that is NOT the
        // connector/city road, so endPos steering has little/no visible effect.
        //
        // New approach:
        //  - Look at BOTH nodes of the anchor segment (ramp/exit).
        //  - For each node, inspect connected segments and score "city-likeness":
        //      * prefer nodes that connect to NON-highway / NON-ramp segments
        //      * prefer the connected segment with the most vehicle lanes
        //  - Choose best node, then choose best connected "city" segment and build goals there.
        // ------------------------------------------------------------







        
        // ------------------------------------------------------------
        // Phase 2/4: Build lane-goal positions on a "city-side" segment
        // AFTER the ramp exit.
        //
        // 060O change:
        //  - Instead of picking only the immediate connected segment at the exit node,
        //    we BFS a few hops into the city network and choose the best segment with
        //    >=2 vehicle lanes (prefer more lanes, prefer shallow depth).
        //  - When applying a goal we set endPos ON that goal segment (not on the anchor),
        //    so the pathfinder has a concrete lane target to converge to.
        // ------------------------------------------------------------




















        // ------------------------------------------------------------
        // Path editing helpers (legacy-safe)
        // We change PathUnit.Position.m_lane in the vehicle's current path
        // for positions belonging to a chosen goal segment.
        //
        // IMPORTANT:
        // - PathUnit is a struct; to mutate it safely we:
        //     * copy PathUnit from buffer
        //     * box it
        //     * invoke SetPosition(...) on boxed copy
        //     * unbox back and write to buffer
        // ------------------------------------------------------------
        private static class A11_PathEdit
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

        // Try to force a specific laneIndex on all positions belonging to goalSeg in the current path.




        // If direct goalSeg edit failed, try: find the first occurrence of exitNode on the path,
        // then apply the goal lane on the first later position that belongs to goalSeg.


































        public static string HudLine0 = "ready";
        public static string HudLine1 = "";
        public static string HudLine2 = "";

        public static string LastSrc = "-";
        public static string LastVid = "-";
        public static string LastStart = "-";
        public static string LastWhy = "-";
        public static string LastSeg = "-";
        public static string LastHwy = "-";

        public static string CorridorSeedSeg = "0";
        public static string CorridorCount = "0";
        public static bool CorridorFilterEnabled = true;

        public static string AnchorSeg = "0";
        public static string AnchorName = "-";
        public static string AnchorDist = "-";

        // Phase2: lane-goal diagnostics (target segment + goal lanes)
        public static string GoalSeg = "-";
        public static string GoalCount = "-";
        public static string GoalRightPos = "-";

        // Phase4: goal target node info (for HUD/debug)
        public static string ExitNodeHud = "-";
        public static string ExitDistHud = "-";

        public static string LastKey = "-";
        public static string F11PressCount = "0";
        public static string F10PressCount = "0";

        // Exposed for HUD
        public static bool IsCapturing = false;
        public static string CaptureRemainHud = "0";
        public static bool AutoEnabled = false;
        public static bool HighwayOnly = false;

        private bool _autoEnabled = true;
        private bool _hwyOnly = true;

        private int _scanIndex;
        private int _tick;

        private bool _capturing;
        private float _capEndAt;
        private int _capIndex;

        private int _cand;
        private int _segNZ;
        private int _hwyCand;
        private int _corrCand;
        private int _sampled;
        private int _stallHit;
        private int _stallFlagHit;
        private int _stallMoveHit;
        private int _attemptAuto;
        private int _attemptMan;
        private int _repOK;
        private int _repFail;

        private int _skipPath0;
        private int _skipWait;
        private int _skipSeg0;
        private int _skipNotHwy;
        private int _skipNotCorr;
        private int _skipNoSeed;
        private int _skipCooldown;
        private int _skipRate;

        private int _f11Cnt;
        private int _f10Cnt;

        private const int BudgetPerTick = 384;

        private const int SAMPLE_TICKS = 20;
        private const float MIN_PROGRESS_METERS = 3.0f; // Phase3: treat "creeping" as potential stall
        private const int SLOW_WINDOWS_TO_STALL = 2;

        private const int CooldownTicks = 1500;
        private const int GlobalMaxPerSecond = 10; // Phase3: allow up to 10 repaths per second
        private const float CAPTURE_SECONDS_DEFAULT = 120.0f;

        private int[] _cooldownUntil;
        private int[] _lastSampleTick;
        private Vector3[] _lastSamplePos;
        private int[] _slowWindows;

        private float _secWindowStart;
        private int _secCount;

        void Start()
        {
            try { A11_Log.Init(); } catch { }
            TryInitArrays();
            LaneHistReset();
            HudLine0 = "ready (F11 seed+start capture)";
        }

        void Update()
        {
            _tick++;

            if (Input.GetKeyDown(KeyCode.F7))
            {
                CorridorFilterEnabled = !CorridorFilterEnabled;
                LastKey = "F7";
                A11_Log.Write("Toggle corridorFilter=" + (CorridorFilterEnabled ? "ON" : "OFF"));
            }

            if (Input.GetKeyDown(KeyCode.F8))
            {
                A11_Corridor.ClearSeed();
                GoalClear();
                LastKey = "F8";
                A11_Log.Write("Corridor seed CLEARED (F8)");
            }

            if (Input.GetKeyDown(KeyCode.F9))
            {
                _autoEnabled = !_autoEnabled;
                LastKey = "F9";
                A11_Log.Write("Toggle auto=" + (_autoEnabled ? "ON" : "OFF"));
            }
            if (Input.GetKeyDown(KeyCode.F12))
            {
                _hwyOnly = !_hwyOnly;
                LastKey = "F12";
                A11_Log.Write("Toggle hwyOnly=" + (_hwyOnly ? "ON" : "OFF"));
            }

            if (Input.GetKeyDown(KeyCode.F11))
            {
                _f11Cnt++;
                F11PressCount = _f11Cnt.ToString();
                LastKey = "F11";
                A11_Log.Write("F11 pressed");

                _attemptMan++;
                bool ok = ManualRepathOneAndSeed_SelectedOrPickHighwayVehicle();
                if (ok) _repOK++;
                else _repFail++;

                // One-key workflow: if manual seed/repath succeeded, start capture immediately (same as F10)
                if (ok && !_capturing)
                {
                    _f10Cnt++;
                    F10PressCount = _f10Cnt.ToString();
                    StartOrRestartCapture("F11");
                }
            }

            if (Input.GetKeyDown(KeyCode.F10))
            {
                _f10Cnt++;
                F10PressCount = _f10Cnt.ToString();
                LastKey = "F10";
                StartOrRestartCapture("F10");
            }

            float now = Time.realtimeSinceStartup;
            if (now - _secWindowStart >= 1.0f)
            {
                _secWindowStart = now;
                _secCount = 0;
                if (_capturing) TrackComputeCounts();
            }

            if (_capturing && _autoEnabled)
                ScanAuto();

            if (_capturing)
                TrackUpdateSome();

            CorridorSeedSeg = A11_Corridor.SeedSeg.ToString();
            CorridorCount = A11_Corridor.CorridorCount.ToString();
            AnchorSeg = A11_Corridor.AnchorSeg.ToString();
            AnchorName = A11_Corridor.AnchorName;
            AnchorDist = (A11_Corridor.AnchorDist >= 0f ? A11_Corridor.AnchorDist.ToString("0") : "-");

            ushort probeSeg = 0;
            int probeVeh = 0;
            bool probeStau = false;
            try
            {
                probeSeg = A11_Corridor.SeedSeg != 0 ? A11_Corridor.SeedSeg : A11_Corridor.AnchorSeg;
                if (probeSeg != 0)
                {
                    probeVeh = A11_TrafficHelper.CountVehiclesOnSegment(probeSeg);
                    probeStau = A11_TrafficHelper.IsCongested(probeSeg);
                }
            }
            catch
            {
                probeSeg = 0;
                probeVeh = 0;
                probeStau = false;
            }

            HudLine0 =
						"seed:" + CorridorSeedSeg +
						" seg:" + GoalSeg +
						" anc:" + AnchorSeg;

			HudLine1 =
						"goal:" + GoalCount +
						" mode:" + (_autoEnabled ? "AUTO" : "MAN");

			HudLine2 =
						"cap:" + CaptureRemainHud +
						" last:" + LastVid;

            if (_capturing)
            {
                float remain = _capEndAt - now;
                if (remain <= 0f)
                {
                    _capturing = false;
                    HudLine2 = "CAP DONE -> summary";
                    WriteSummary();

                    _autoEnabled = false;
                    _hwyOnly = true;
                    A11_Log.Write("Capture ended -> AUTO stopped (press F10 to start new capture).");
                }
                else
                {
                    if (CorridorFilterEnabled && A11_Corridor.SeedSeg == 0)
                        HudLine2 = "CAP " + remain.ToString("0") + "s  NEED SEED (F11)" + " | probe seg=" + probeSeg + " veh=" + probeVeh + " stau=" + (probeStau ? "Y" : "N");
                    else
                        HudLine2 = "CAP " + remain.ToString("0") + "s seed=" + A11_Corridor.SeedSeg + " anc=" + A11_Corridor.AnchorSeg + " d=" + AnchorDist + "m rate=" + _secCount + "/" + GlobalMaxPerSecond + " | probe seg=" + probeSeg + " veh=" + probeVeh + " stau=" + (probeStau ? "Y" : "N");
                }
            }
            else
            {
                if (CorridorFilterEnabled && A11_Corridor.SeedSeg == 0)
                    HudLine2 = "idle NEED SEED (F11)" + " | probe seg=" + probeSeg + " veh=" + probeVeh + " stau=" + (probeStau ? "Y" : "N");
                else
                    HudLine2 = "idle seed=" + A11_Corridor.SeedSeg + " anc=" + A11_Corridor.AnchorSeg + " d=" + AnchorDist + "m" + " | probe seg=" + probeSeg + " veh=" + probeVeh + " stau=" + (probeStau ? "Y" : "N");
            }
        

// HUD export (kept super simple)
IsCapturing = _capturing;
AutoEnabled = _autoEnabled;
HighwayOnly = _hwyOnly;
try
{
    float rem = _capEndAt - Time.realtimeSinceStartup;
    if (rem < 0f) rem = 0f;
    CaptureRemainHud = rem.ToString("0");
}
catch
{
    CaptureRemainHud = "0";
}
        }



























    }
}
