using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICities;
using UnityEngine;

namespace CS1_LaneBalancer
{
    public sealed partial class A11_RepathController : MonoBehaviour
    {

        private void StartOrRestartCapture(string keyTag)
        {
            ResetCountersForNewCapture();
            TrackClearAll();

            // Phase2: rebuild lane goals for the current anchor (if any)
            try
            {
                if (A11_Corridor.SeedSeg != 0)
                {
                    Vector3 sp = A11_PathSeg.TryGetSegmentMid(A11_Corridor.SeedSeg);
                    BuildGoalsFromAnchor(sp);
                }
                else
                {
                    GoalClear();
                }
            }
            catch { }
            LaneHistReset();

            _autoEnabled = true;
            _hwyOnly = false; // Phase3: allow non-highway candidates too

            _capEndAt = Time.realtimeSinceStartup + CAPTURE_SECONDS_DEFAULT;
            _capturing = true;
            _capIndex++;

            A11_Log.Write((object.ReferenceEquals(keyTag, null) ? "F11" : keyTag) + " - START CAPTURE #" + _capIndex +
                          " dur=" + CAPTURE_SECONDS_DEFAULT.ToString("0") + "s " +
                          "(auto=ON hwyOnly=" + (_hwyOnly ? "ON" : "OFF") + ")" +
                          " corridorFilter=" + (CorridorFilterEnabled ? "ON" : "OFF") +
                          " seed=" + A11_Corridor.SeedSeg +
                          " corrCnt=" + A11_Corridor.CorridorCount +
                          " anc=" + A11_Corridor.AnchorSeg +
                          " ancName=" + A11_Corridor.AnchorName +
                          " ancDist=" + (A11_Corridor.AnchorDist >= 0f ? A11_Corridor.AnchorDist.ToString("0.0") : "-"));
        }


        private void TryInitArrays()
        {
            VehicleManager vm = VehicleManager.instance;
            if (object.ReferenceEquals(vm, null)) return;

            Vehicle[] buf = vm.m_vehicles.m_buffer;
            if (object.ReferenceEquals(buf, null)) return;

            int len = buf.Length;

            _cooldownUntil = new int[len];
            _lastSampleTick = new int[len];
            _lastSamplePos = new Vector3[len];
            _slowWindows = new int[len];

            _scanIndex = 0;
            _tick = 0;

            _secWindowStart = Time.realtimeSinceStartup;
            _secCount = 0;

            A11_PathSeg.EnsureInit();
            A11_NodeSegments.EnsureInit();
            A11_Corridor.EnsureInit();
            A11_Reflect.ResetCache();
        }


        private void ResetCountersForNewCapture()
        {
            _cand = _segNZ = _hwyCand = _corrCand = 0;
            _sampled = 0;
            _stallHit = 0;
            _stallFlagHit = 0;
            _stallMoveHit = 0;
            _attemptAuto = 0;

            _skipPath0 = _skipWait = _skipSeg0 = _skipNotHwy = _skipNotCorr = _skipNoSeed = _skipCooldown = _skipRate = 0;

            if (!object.ReferenceEquals(_cooldownUntil, null))
            {
                for (int i = 0; i < _cooldownUntil.Length; i++)
                {
                    _cooldownUntil[i] = 0;
                    _lastSampleTick[i] = 0;
                    _lastSamplePos[i] = Vector3.zero;
                    _slowWindows[i] = 0;
                }
            }
        }


        private void WriteSummary()
        {
            TrackComputeCounts();
            A11_Log.Write("=== CAPTURE SUMMARY #" + _capIndex + " ===");
            A11_Log.Write("cand=" + _cand +
                          " segNZ=" + _segNZ +
                          " hwyCand=" + _hwyCand +
                          " corCand=" + _corrCand +
                          " sampled=" + _sampled +
                          " stallHit=" + _stallHit + " (F=" + _stallFlagHit + " M=" + _stallMoveHit + ")" +
                          " autoAtt=" + _attemptAuto +
                          " manAtt=" + _attemptMan +
                          " repOK=" + _repOK +
                          " repFail=" + _repFail);

            A11_Log.Write("OUTCOME reached=" + _trkReached + " progress=" + _trkProgress + " dropped=" + _trkDropped + " active=" + _trkActive);
            A11_Log.Write(TrackBestDistStats());
            A11_Log.Write("LANE hist: " + LaneHistHud + " | " + LastLaneHud);

            A11_Log.Write("=== END SUMMARY ===");
            A11_Log.Flush(true);
        }


        private static bool TryGetSelectedVehicleId(VehicleManager vm, out ushort sel)
        {
            sel = 0;
            if (object.ReferenceEquals(vm, null)) return false;

            try
            {
                Type t = vm.GetType();
                if (object.ReferenceEquals(t, null)) return false;

                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic;

                var fi = t.GetField("m_selectedVehicle", flags);
                if (!object.ReferenceEquals(fi, null))
                {
                    object v = fi.GetValue(vm);
                    if (v is ushort)
                    {
                        sel = (ushort)v;
                        return true;
                    }
                }

                var pi = t.GetProperty("selectedVehicle", flags);
                if (!object.ReferenceEquals(pi, null))
                {
                    object v = pi.GetValue(vm, null);
                    if (v is ushort)
                    {
                        sel = (ushort)v;
                        return true;
                    }
                }

                string[] names = new string[] { "m_selectedVehicleID", "m_selectedVehicleId", "m_selectedVehicleIndex" };
                for (int i = 0; i < names.Length; i++)
                {
                    fi = t.GetField(names[i], flags);
                    if (object.ReferenceEquals(fi, null)) continue;
                    object v = fi.GetValue(vm);
                    if (v is ushort)
                    {
                        sel = (ushort)v;
                        return true;
                    }
                    if (v is int)
                    {
                        int iv = (int)v;
                        if (iv > 0 && iv < 65535) { sel = (ushort)iv; return true; }
                    }
                }
            }
            catch { }

            return false;
        }


        private void ScanAuto()
        {
            VehicleManager vm = VehicleManager.instance;
            if (object.ReferenceEquals(vm, null)) return;

            Vehicle[] buf = vm.m_vehicles.m_buffer;
            if (object.ReferenceEquals(buf, null)) return;

            if (object.ReferenceEquals(_cooldownUntil, null) ||
                object.ReferenceEquals(_lastSampleTick, null) ||
                object.ReferenceEquals(_lastSamplePos, null) ||
                object.ReferenceEquals(_slowWindows, null))
                return;

            bool needSeed = CorridorFilterEnabled && (A11_Corridor.SeedSeg == 0);

            int max = buf.Length;
            int processed = 0;

            while (processed < BudgetPerTick)
            {
                ushort id = (ushort)_scanIndex;
                _scanIndex++;
                if (_scanIndex >= max) _scanIndex = 0;

                processed++;

                Vehicle v = buf[id];

                if ((v.m_flags & Vehicle.Flags.Created) == 0) continue;

                if ((v.m_flags & Vehicle.Flags.WaitingPath) != 0) { _skipWait++; continue; }
                if (_cooldownUntil[id] > _tick) { _skipCooldown++; continue; }
                if (v.m_path == 0u) { _skipPath0++; continue; }

                _cand++;

                ushort seg = A11_PathSeg.TryGetCurrentSegment(ref v);
                if (seg == 0) { _skipSeg0++; continue; }

                _segNZ++;

                bool isHwy = A11_HighwayFilter.IsHighwaySegment(seg) || A11_HighwayFilter.IsRampSegmentName(A11_HighwayFilter.GetNetName(seg));
                if (isHwy) _hwyCand++;
                if (_hwyOnly && !isHwy) { _skipNotHwy++; continue; }

                if (CorridorFilterEnabled && A11_Corridor.SeedSeg != 0)
                {
                    if (!A11_Corridor.IsInCorridor(seg)) { _skipNotCorr++; continue; }
                }
                _corrCand++;

                if (needSeed) { _skipNoSeed++; continue; }

                VehicleAI scanAi = null;
                try
                {
                    if (!object.ReferenceEquals(v.Info, null)) scanAi = v.Info.m_vehicleAI;
                }
                catch { scanAi = null; }

                if (IsBusAI(scanAi))
                {
                    continue;
                }

                bool scanPassenger = IsPassengerAI(scanAi);
                bool scanTruck = IsTruckAI(scanAi);
                if (!scanPassenger && !scanTruck)
                {
                    continue;
                }

                int lastT = _lastSampleTick[id];
                if (lastT == 0)
                {
                    _lastSampleTick[id] = _tick;
                    _lastSamplePos[id] = v.GetLastFramePosition();
                    _slowWindows[id] = 0;
                    continue;
                }

                int dt = _tick - lastT;
                if (dt < SAMPLE_TICKS) continue;

                Vector3 nowPos = v.GetLastFramePosition();
                float dist = (nowPos - _lastSamplePos[id]).magnitude;

                _sampled++;

                if (dist < MIN_PROGRESS_METERS || ((v.m_flags & (Vehicle.Flags.Stopped | Vehicle.Flags.WaitingSpace)) != 0))
                {
                    // Phase3 stall detector:
                    // - low movement over SAMPLE_TICKS OR vehicle flags indicate blocking/stopping
                    bool flagStall = ((v.m_flags & (Vehicle.Flags.Stopped | Vehicle.Flags.WaitingSpace)) != 0);
                    if (flagStall) _stallFlagHit++; else _stallMoveHit++;

                    int sw = _slowWindows[id] + 1;
                    _slowWindows[id] = sw;

                    if (sw >= SLOW_WINDOWS_TO_STALL)
                    {
                        _slowWindows[id] = 0;
                        _stallHit++;

                        // Trucks only when really stopped/waiting; passenger cars may react earlier.
                        if (scanTruck && !flagStall && dist >= 1.0f)
                        {
                            // keep trucks conservative and mostly right unless they are truly blocked
                        }
                        else if (_secCount >= GlobalMaxPerSecond)
                        {
                            _skipRate++;
                        }
                        else
                        {
                            _attemptAuto++;
                            bool ok = TryRepath("AUTO", id, ref v, seg, isHwy, dist);
                            if (ok) { _repOK++; TrackAdd(id, A11_Corridor.AnchorSeg); }
                            else { _repFail++; }
                            _secCount++;
                            _cooldownUntil[id] = _tick + CooldownTicks;
                        }
                    }
                }
                else
                {
                    _slowWindows[id] = 0;
                }

                _lastSampleTick[id] = _tick;
                _lastSamplePos[id] = nowPos;

                buf[id] = v;
            }
        }


        private bool ManualRepathOneAndSeed_SelectedOrPickHighwayVehicle()
        {
            if (_secCount >= GlobalMaxPerSecond)
            {
                SetLast("MAN", "-", "false", "rateLimit", "-", "-");
                _skipRate++;
                return false;
            }

            VehicleManager vm = VehicleManager.instance;
            if (object.ReferenceEquals(vm, null)) return false;

            Vehicle[] buf = vm.m_vehicles.m_buffer;
            if (object.ReferenceEquals(buf, null)) return false;

            ushort pickedId = 0;
            ushort pickedSeg = 0;
            Vector3 seedPos = Vector3.zero;

            try
            {
                ushort sel;
                if (TryGetSelectedVehicleId(vm, out sel))
                {
                    if (sel != 0 && sel < buf.Length)
                    {
                        Vehicle sv = buf[sel];
                        if ((sv.m_flags & Vehicle.Flags.Created) != 0 && (sv.m_flags & Vehicle.Flags.WaitingPath) == 0 && sv.m_path != 0u)
                        {
                            ushort seg = A11_PathSeg.TryGetCurrentSegment(ref sv);
                            if (seg != 0 && A11_HighwayFilter.IsHighwaySegment(seg))
                            {
                                pickedId = sel;
                                pickedSeg = seg;
                                seedPos = sv.GetLastFramePosition();
                                A11_Log.Write("F11: using SELECTED vehicle " + pickedId + " seedSeg=" + pickedSeg + " (reflection)");
                            }
                        }
                    }
                }
                else
                {
                    A11_Log.Write("F11: selected vehicle not available on this runtime (reflection miss)");
                }
            }
            catch { }

            if (pickedId == 0)
            {
                Vector3 camPos = Vector3.zero;
                bool haveCam = false;

                try
                {
                    Camera cam = Camera.main;
                    if (!object.ReferenceEquals(cam, null))
                    {
                        camPos = cam.transform.position;
                        haveCam = true;
                    }
                }
                catch { }

                if (!haveCam)
                {
                    camPos = Vector3.zero;
                }

                // Prefer PASSENGER cars for goal/lane testing. Trucks are forced to goal=0 by design.
                // Two-pass scan:
                //  pass 1: nearest PassengerCarAI on a highway segment
                //  pass 2: nearest any vehicle on a highway segment
                ushort bestId = 0;
                ushort bestSeg = 0;
                Vector3 bestPos = Vector3.zero;
                float bestD = float.MaxValue;

                ushort bestAnyId = 0;
                ushort bestAnySeg = 0;
                Vector3 bestAnyPos = Vector3.zero;
                float bestAnyD = float.MaxValue;

                int maxScan = 12000;
                int scanned = 0;

                for (ushort i = 1; i < buf.Length && scanned < maxScan; i++, scanned++)
                {
                    Vehicle v = buf[i];
                    if ((v.m_flags & Vehicle.Flags.Created) == 0) continue;
                    if ((v.m_flags & Vehicle.Flags.WaitingPath) != 0) continue;
                    if (v.m_path == 0u) continue;

                    ushort seg = A11_PathSeg.TryGetCurrentSegment(ref v);
                    if (seg == 0) continue;
                    if (!A11_HighwayFilter.IsHighwaySegment(seg)) continue;

                    Vector3 vp = v.GetLastFramePosition();
                    float d = (vp - camPos).sqrMagnitude;

                    // Any candidate
                    if (d < bestAnyD)
                    {
                        bestAnyD = d;
                        bestAnyId = i;
                        bestAnySeg = seg;
                        bestAnyPos = vp;
                    }

                    // Passenger preference
                    try
                    {
                        VehicleInfo info = v.Info;
                        if (!object.ReferenceEquals(info, null))
                        {
                            VehicleAI ai = info.m_vehicleAI;
                            if (!object.ReferenceEquals(ai, null))
                            {
                                string ain = ai.GetType().Name;
                                if (!object.ReferenceEquals(ain, null) &&
                                    ain.IndexOf("PassengerCarAI", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    if (d < bestD)
                                    {
                                        bestD = d;
                                        bestId = i;
                                        bestSeg = seg;
                                        bestPos = vp;
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (bestId != 0)
                {
                    pickedId = bestId;
                    pickedSeg = bestSeg;
                    seedPos = bestPos;
                    A11_Log.Write("F11: picked NEAREST-TO-CAMERA PASSENGER vehicle " + pickedId + " seedSeg=" + pickedSeg + " camD=" + Math.Sqrt(bestD).ToString("0.0"));
                }
                else if (bestAnyId != 0)
                {
                    pickedId = bestAnyId;
                    pickedSeg = bestAnySeg;
                    seedPos = bestAnyPos;
                    A11_Log.Write("F11: picked NEAREST-TO-CAMERA vehicle " + pickedId + " seedSeg=" + pickedSeg + " camD=" + Math.Sqrt(bestAnyD).ToString("0.0"));
                }
            }

            if (pickedId == 0)
            {
                A11_Corridor.ClearSeed();
                A11_Log.Write("F11: no highway vehicle found -> seed CLEARED");
                SetLast("MAN", "-", "false", "noHwyVeh", "-", "-");
                return false;
            }

            Vehicle pv = buf[pickedId];

            A11_Corridor.Rebuild(pickedSeg, seedPos);
            A11_Log.Write("Corridor REBUILD seed=" + pickedSeg +
                          " count=" + A11_Corridor.CorridorCount +
                          " anc=" + A11_Corridor.AnchorSeg +
                          " ancName=" + A11_Corridor.AnchorName +
                          " ancDist=" + (A11_Corridor.AnchorDist >= 0f ? A11_Corridor.AnchorDist.ToString("0.0") : "-") +
                          " (vehicle " + pickedId + ")");

            // Phase2: build lane goals on the road segment after the ramp anchor
            BuildGoalsFromAnchor(seedPos);

            bool ok = TryRepath("MAN", pickedId, ref pv, pickedSeg, true, -1f);
            buf[pickedId] = pv;

            _secCount++;
            return ok;
        }


        private bool TryRepath(string src, ushort id, ref Vehicle v, ushort seg, bool isHwy, float measuredDist)
        {
            try
            {
                VehicleInfo info = v.Info;
                if (object.ReferenceEquals(info, null))
                {
                    SetLast(src, "VID=" + id, "false", "noInfo", "seg=" + seg, isHwy ? "hwy=Y" : "hwy=N");
                    return false;
                }

                VehicleAI ai = info.m_vehicleAI;
                if (object.ReferenceEquals(ai, null))
                {
                    SetLast(src, "VID=" + id, "false", "aiNull", "seg=" + seg, isHwy ? "hwy=Y" : "hwy=N");
                    return false;
                }
                if (IsBusAI(ai))
                {
                    SetLast(src, "VID=" + id, "false", "busSkip", "seg=" + seg, isHwy ? "hwy=Y" : "hwy=N");
                    return false;
                }

                uint oldPath = v.m_path;
                byte oldPos = v.m_pathPositionIndex;
                Vehicle.Flags oldFlags = v.m_flags;

                
                Vector3 startPos = v.GetLastFramePosition();
                Vector3 endPos = A11_Corridor.GetAnchorPosOr(startPos, seg);
                bool endToAnc = true;

                // Phase2/4: if goal lanes are available, aim endPos ON the chosen goal segment/lane.
                // This is applied for MAN + AUTO, but only within the current corridor, and only for non-emergency AIs.
                int goalIdx = -1;
                try
                {
                    if (_goalCount > 0 && _goalSeg != 0 && A11_Corridor.SeedSeg != 0 && A11_Corridor.IsInCorridor(seg))
                    {
                        // Emergency vehicles: do not force lane goals.
                        if (!IsEmergencyAI(ai))
                        {
                            float dToExit = 1e9f;
                            try { if (_exitNode != 0) dToExit = (startPos - _exitNodePos).magnitude; } catch { dToExit = 1e9f; }

                            // Only start forcing goals when we're reasonably close to the exit area.
                            // (too early can cause irrelevant detours)
                            bool nearExit = (dToExit < 650f);

                            if (nearExit)
                            {
                                uint frame = 0u;
                                try { frame = SimulationManager.instance.m_currentFrameIndex; } catch { frame = 0u; }

                                GoalAssign ga;
                                if (_goalAssign.TryGetValue(id, out ga) && frame != 0u && ga.untilFrame > frame && ga.idx >= 0 && ga.idx < _goalCount)
                                {
                                    goalIdx = ga.idx;
                                }
                                else
                                {
                                    // Deterministic distribution:
                                    // - trucks prefer right-most
                                    // - others: (vehicleId + frame bucket) mod goalCount
                                    if (IsTruckAI(ai))
                                    {
                                        goalIdx = 0;
                                    }
                                    else
                                    {
                                        uint bucket = 0u;
                                        try { bucket = frame / 512u; } catch { bucket = 0u; }
                                        goalIdx = (int)((id + (ushort)(bucket & 0xFFFFu)) % (ushort)_goalCount);
                                    }

                                    ga.idx = goalIdx;
                                    ga.untilFrame = frame + GOAL_ASSIGN_TTL_FRAMES;
                                    try { _goalAssign[id] = ga; } catch { }
                                }

                                Vector3 gp;
                                if (TryGetGoalEndPos(goalIdx, out gp))
                                {
                                    endPos = gp;
                                    endToAnc = false;
                                    ExitDistHud = dToExit.ToString("0");
                                }
                                else
                                {
                                    goalIdx = -1;
                                }
                            }
                        }
                    }
                }
                catch { goalIdx = -1; }
int usedMode = 0;
                bool started = A11_Reflect.StartPathFind_Try(ai, id, ref v, startPos, endPos, out usedMode);

                if (started)
                {
                    // Lane diagnostics: record the start lane/offset for EVERY successful repath (MAN + AUTO)
                    RecordLaneDiag(id, ref v);
                    try
                    {
                        uint newPath = v.m_path;
                        if (oldPath != 0u && newPath != oldPath)
                        {
                            // Keep oldPath alive on this legacy runtime; releasing immediately has correlated
                            // with hovering/teleport-like artifacts in tests.
                        }
                    }
                    catch { }

                    
// Phase5: If we have a goal, enforce lane choice directly in the computed path.
// First try the old "goalSeg" enforcement (kept), then fallback to an exit-node based
// enforcement which maps the desired lane position to the actual post-exit segment.
try
{
    if (goalIdx >= 0 && goalIdx < _goalCount && _goalSeg != 0)
    {
        int ch1 = 0;
        int ch2 = 0;
        ushort appliedSeg = 0;
        byte appliedLane = 0;

        byte laneIdxGoalSeg = _goalLaneIdx[goalIdx];

        bool okLane1 = ApplyGoalLaneToPath(v.m_path, _goalSeg, laneIdxGoalSeg, out ch1);

        // NOTE:
        // ApplyGoalLaneAfterExitNodeToPath expects:
        //   (path, goalSeg, laneIdx, exitNode, out appliedSeg, out changed)
        // Earlier experiment code passed desiredPos / appliedLane with a different signature.
        // That caused the compile error:
        //   float -> byte, out int -> ushort, out byte -> out int
        bool okLane2 = false;
        if (!okLane1 || ch1 == 0)
        {
            okLane2 = ApplyGoalLaneAfterExitNodeToPath(
                v.m_path,
                _goalSeg,
                laneIdxGoalSeg,
                _exitNode,
                out appliedSeg,
                out ch2);
            appliedLane = laneIdxGoalSeg;
        }

        if (okLane1 && ch1 > 0)
        {
            A11_Log.Write(src + " vid=" + id + " GOAL-LANE ENFORCED(goalSeg) seg=" + _goalSeg + " laneIdx=" + laneIdxGoalSeg + " changed=" + ch1);
        }
        else if (okLane2 && ch2 > 0)
        {
            A11_Log.Write(src + " vid=" + id + " GOAL-LANE ENFORCED(afterExit) exitNode=" + _exitNode + " seg=" + appliedSeg + " laneIdx=" + appliedLane + " changed=" + ch2);
        }
        else
        {
            A11_Log.Write(src + " vid=" + id + " GOAL-LANE enforce FAIL goalSeg=" + _goalSeg + " laneIdx=" + laneIdxGoalSeg + " ch1=" + ch1 +
                          " exitNode=" + _exitNode + " appliedSeg=" + appliedSeg + " ch2=" + ch2);
        }
    }
}
catch { }

}
                else
                {
                    v.m_path = oldPath;
                    v.m_pathPositionIndex = oldPos;
                    v.m_flags = oldFlags;
                }

                string why = started ? ("OK m" + usedMode) : "Start=false";
                if (measuredDist >= 0f) why = why + " d=" + measuredDist.ToString("0.0");

                SetLast(src, "VID=" + id, started ? "true" : "false", why, "seg=" + seg, isHwy ? "hwy=Y" : "hwy=N");

                A11_Log.Write(src +
                              " vid=" + id +
                              " start=" + (started ? "Y" : "N") +
                              " seg=" + seg +
                              " hwy=" + (isHwy ? "Y" : "N") +
                              " cor=" + (A11_Corridor.SeedSeg != 0 && A11_Corridor.IsInCorridor(seg) ? "Y" : "N") +
                              " oldPath=" + oldPath +
                              (measuredDist >= 0f ? (" dist=" + measuredDist.ToString("0.00")) : "") +
                              " mode=" + usedMode +
                              " goal=" + (goalIdx >= 0 ? (goalIdx.ToString() + "/L" + _goalLaneIdx[goalIdx].ToString()) : "-") +
                              " gSeg=" + (goalIdx >= 0 ? _goalSeg.ToString() : "-") +
                              " gD=" + (goalIdx >= 0 ? _goalDepth.ToString() : "-") +
                              " gName=" + (goalIdx >= 0 ? _goalSegName : "-") +
                              " endToAnc=" + (endToAnc ? "Y" : "N") +
                              " anc=" + A11_Corridor.AnchorSeg +
                              " ancD=" + (A11_Corridor.AnchorDist >= 0f ? A11_Corridor.AnchorDist.ToString("0.0") : "-") +
                              " AI=" + ai.GetType().Name);

                return started;
            }
            catch (Exception ex)
            {
                SetLast(src, "VID=" + id, "false", "EX:" + ex.GetType().Name, "seg=" + seg, isHwy ? "hwy=Y" : "hwy=N");
                A11_Log.Write(src + " vid=" + id + " repath EX " + ex.ToString());
                return false;
            }
        }


        private static void SetLast(string src, string vid, string start, string why, string seg, string hwy)
        {
            LastSrc = object.ReferenceEquals(src, null) ? "-" : src;
            LastVid = object.ReferenceEquals(vid, null) ? "-" : vid;
            LastStart = object.ReferenceEquals(start, null) ? "-" : start;
            LastWhy = object.ReferenceEquals(why, null) ? "-" : why;
            LastSeg = object.ReferenceEquals(seg, null) ? "-" : seg;
            LastHwy = object.ReferenceEquals(hwy, null) ? "-" : hwy;
        }
    }
}
