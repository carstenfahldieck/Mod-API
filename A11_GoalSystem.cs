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
        private void GoalClear()
        {
            _goalCount = 0;
            _goalNext = 0;
            _goalSeg = 0;
            _goalRightMost = 0f;
            _goalExitNode = 0;
            _goalFromNode = 0;
            _goalToNode = 0;
            _goalDepth = 0;
            _goalSegName = "-";
            ExitNodeHud = "-";
            ExitDistHud = "-";
            for (int i = 0; i < GOAL_MAX; i++) { _goalLanePos[i] = 0f; _goalLaneIdx[i] = 0; }
            GoalSeg = "-";
            GoalCount = "-";
            GoalRightPos = "-";
            _exitNode = 0;
            _exitNodePos = default(Vector3);
            ExitNodeHud = "-";
                    // Keep assignments across frequent goal rebuilds (e.g. repeated F11),
                    // otherwise passenger cars tend to get goal=0 repeatedly. Guard memory growth.
                    try
                    {
                        if (!object.ReferenceEquals(_goalAssign, null) && _goalAssign.Count > 12000) _goalAssign.Clear();
                    }
                    catch { }
}


        private ushort ChooseBestExitNodeForAnchor(ushort ancSeg, Vector3 seedPos)
        {
            try
            {
                if (ancSeg == 0) return 0;
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return 0;

                NetSegment anc = nm.m_segments.m_buffer[ancSeg];
                ushort n0 = anc.m_startNode;
                ushort n1 = anc.m_endNode;

                int score0 = ScoreExitNode(n0, ancSeg);
                int score1 = ScoreExitNode(n1, ancSeg);

                if (score0 > score1) return n0;
                if (score1 > score0) return n1;

                // Tie-break: choose the node FARTHER from seedPos (often the city-side).
                try
                {
                    Vector3 p0 = nm.m_nodes.m_buffer[n0].m_position;
                    Vector3 p1 = nm.m_nodes.m_buffer[n1].m_position;
                    float d0 = (p0 - seedPos).sqrMagnitude;
                    float d1 = (p1 - seedPos).sqrMagnitude;
                    return (d0 >= d1) ? n0 : n1;
                }
                catch { return n0; }
            }
            catch { }
            return 0;
        }


        private int ScoreExitNode(ushort nodeId, ushort ancSeg)
        {
            try
            {
                if (nodeId == 0) return 0;
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return 0;

                NetNode node = nm.m_nodes.m_buffer[nodeId];

                int nonHwyCount = 0;
                int bestVehLanes = 0;

                for (int i = 0; i < 8; i++)
                {
                    ushort s = A11_NodeSegments.GetSegmentAt(ref node, i);
                    if (s == 0) continue;
                    if (s == ancSeg) continue;

                    string nmName = A11_HighwayFilter.GetNetName(s);
                    bool isRampOrHwy = A11_HighwayFilter.IsHighwaySegment(s) || A11_HighwayFilter.IsRampSegmentName(nmName);
                    if (!isRampOrHwy)
                    {
                        nonHwyCount++;
                        int vc = CountVehicleLanes(s);
                        if (vc > bestVehLanes) bestVehLanes = vc;
                    }
                }

                // Score: strongly prefer nodes that have any non-highway connection.
                // Add best lane count for finer ranking.
                return nonHwyCount * 100 + bestVehLanes;
            }
            catch { }
            return 0;
        }


        private static ushort GetOtherNodeOnSegment(NetManager nm, ushort seg, ushort nodeOnSeg)
        {
            if (seg == 0 || nodeOnSeg == 0) return 0;
            try
            {
                NetSegment s = nm.m_segments.m_buffer[seg];
                if (s.m_startNode == nodeOnSeg) return s.m_endNode;
                if (s.m_endNode == nodeOnSeg) return s.m_startNode;
            }
            catch { }
            return 0;
        }


        private bool FindGoalSegmentFromExitBfs(NetManager nm, ushort ancSeg, ushort exitNode, out ushort bestSeg, out ushort fromNode, out ushort toNode, out int depth, out string segName)
        {
            bestSeg = 0;
            fromNode = 0;
            toNode = 0;
            depth = 0;
            segName = "-";

            try
            {
                if (exitNode == 0) return false;

                const int MAXQ = 2048;
                ushort[] qNode = new ushort[MAXQ];
                int[] qDepth = new int[MAXQ];
                int qh = 0, qt = 0;

                // visited nodes (NetNodes are up to 65535, keep it small & safe)
                bool[] vis = new bool[nm.m_nodes.m_buffer.Length];

                qNode[qt] = exitNode;
                qDepth[qt] = 0;
                qt++;
                vis[exitNode] = true;

                int bestScore = int.MinValue;
                int bestLanes = 0;
                ushort bestFrom = 0;
                ushort bestTo = 0;
                string bestName = null;
                int bestDepth = 0;

                while (qh < qt && qt < MAXQ)
                {
                    ushort nodeId = qNode[qh];
                    int d0 = qDepth[qh];
                    qh++;

                    if (nodeId == 0) continue;
                    if (d0 > 6) continue;

                    NetNode node = nm.m_nodes.m_buffer[nodeId];

                    for (int i = 0; i < 8; i++)
                    {
                        ushort s = A11_NodeSegments.GetSegmentAt(ref node, i);
                        if (s == 0) continue;
                        if (s == ancSeg) continue;

                        string nmName = A11_HighwayFilter.GetNetName(s);
                        bool isRampOrHwy = A11_HighwayFilter.IsHighwaySegment(s) || A11_HighwayFilter.IsRampSegmentName(nmName);

                        // Prefer city segments only
                        if (!isRampOrHwy)
                        {
                            int lanes = CountVehicleLanes(s);

                            // Score: prefer segments with >=2 lanes strongly, then more lanes, then shallow depth.
                            int score = 0;
                            if (lanes >= 2) score += 10000;
                            score += lanes * 100;
                            score -= d0 * 10;

                            if (score > bestScore)
                            {
                                ushort other = GetOtherNodeOnSegment(nm, s, nodeId);
                                if (other != 0)
                                {
                                    bestScore = score;
                                    bestLanes = lanes;
                                    bestSeg = s;
                                    bestFrom = nodeId;
                                    bestTo = other;
                                    bestName = nmName;
                                    bestDepth = d0;
                                }
                            }
                        }

                        // Continue BFS along ALL non-highway connections, and also along ramps for connectivity
                        // (but ramps/highways won't be chosen as best, they just let us reach city roads).
                        ushort nextNode = GetOtherNodeOnSegment(nm, s, nodeId);
                        if (nextNode == 0) continue;
                        if (nextNode >= (ushort)vis.Length) continue;
                        if (vis[nextNode]) continue;

                        int nd = d0 + 1;
                        if (nd > 6) continue;

                        vis[nextNode] = true;
                        if (qt < MAXQ)
                        {
                            qNode[qt] = nextNode;
                            qDepth[qt] = nd;
                            qt++;
                        }
                    }
                }

                if (bestSeg != 0)
                {
                    fromNode = bestFrom;
                    toNode = bestTo;
                    depth = bestDepth;
                    segName = object.ReferenceEquals(bestName, null) ? "-" : bestName;
                    if (segName.Length > 28) segName = segName.Substring(0, 28);

                    // If we failed to find >=2 lanes, still allow the best we found (for logging),
                    // but this will not show visible "lane change".
                    return true;
                }
            }
            catch { }

            return false;
        }


        private bool BuildGoalsFromAnchor(Vector3 seedPos)
        {
            try
            {
                GoalClear();

                ushort ancSeg = A11_Corridor.AnchorSeg;
                if (ancSeg == 0) return false;

                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return false;

                ushort exitNode = ChooseBestExitNodeForAnchor(ancSeg, seedPos);
                if (exitNode == 0) return false;

                _exitNode = exitNode;
                try { _exitNodePos = nm.m_nodes.m_buffer[exitNode].m_position; } catch { _exitNodePos = default(Vector3); }
                _goalExitNode = exitNode;
                ExitNodeHud = exitNode.ToString();

                ushort bestSeg, fromNode, toNode;
                int depth;
                string segName;

                bool ok = FindGoalSegmentFromExitBfs(nm, ancSeg, exitNode, out bestSeg, out fromNode, out toNode, out depth, out segName);
                if (!ok || bestSeg == 0) return false;

                int laneCnt = CountVehicleLanes(bestSeg);
                int n = FillGoalLanePositions(bestSeg);
                if (n <= 0) return false;

                _goalSeg = bestSeg;
                _goalCount = n;
                _goalFromNode = fromNode;
                _goalToNode = toNode;
                _goalDepth = depth;
                _goalSegName = segName;

                _goalRightMost = _goalLanePos[0];

                GoalSeg = _goalSeg.ToString();
                GoalCount = _goalCount.ToString();
                GoalRightPos = _goalRightMost.ToString("0.0");

                A11_Log.Write("GOAL lanes built: seg=" + _goalSeg + " n=" + _goalCount + " lanes=" + laneCnt + " d=" + depth + " name=" + _goalSegName + " exitNode=" + exitNode);
                return true;
            }
            catch
            {
                GoalClear();
                return false;
            }
        }

private int CountVehicleLanes(ushort seg)
        {
            try
            {
                if (seg == 0) return 0;
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return 0;

                NetInfo info = nm.m_segments.m_buffer[seg].Info;
                if (object.ReferenceEquals(info, null)) return 0;

                NetInfo.Lane[] lanes = info.m_lanes;
                if (object.ReferenceEquals(lanes, null)) return 0;

                int c = 0;
                for (int i = 0; i < lanes.Length; i++)
                {
                    NetInfo.Lane ln = lanes[i];
                    if ((ln.m_laneType & NetInfo.LaneType.Vehicle) == 0) continue;
                    if ((ln.m_vehicleType & VehicleInfo.VehicleType.Car) == 0) continue;
                    c++;
                }
                return c;
            }
            catch { }
            return 0;
        }


        private int FillGoalLanePositions(ushort seg)
        {
            try
            {
                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return 0;

                NetInfo info = nm.m_segments.m_buffer[seg].Info;
                if (object.ReferenceEquals(info, null)) return 0;

                NetInfo.Lane[] lanes = info.m_lanes;
                if (object.ReferenceEquals(lanes, null)) return 0;

                // collect (vehicle car lanes only) with both position and laneIndex
                float[] tmpPos = new float[64];
                byte[] tmpIdx = new byte[64];
                int n = 0;

                for (int i = 0; i < lanes.Length && n < tmpPos.Length; i++)
                {
                    NetInfo.Lane ln = lanes[i];
                    if ((ln.m_laneType & NetInfo.LaneType.Vehicle) == 0) continue;
                    if ((ln.m_vehicleType & VehicleInfo.VehicleType.Car) == 0) continue;

                    tmpPos[n] = ln.m_position;
                    tmpIdx[n] = (byte)i; // lane index within NetInfo.m_lanes
                    n++;
                }

                if (n == 0) return 0;

                // sort ascending by position, keep indices aligned
                for (int i = 0; i < n - 1; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        if (tmpPos[j] < tmpPos[i])
                        {
                            float tp = tmpPos[i];
                            tmpPos[i] = tmpPos[j];
                            tmpPos[j] = tp;

                            byte ti = tmpIdx[i];
                            tmpIdx[i] = tmpIdx[j];
                            tmpIdx[j] = ti;
                        }
                    }
                }

                int outN = n;
                if (outN > GOAL_MAX) outN = GOAL_MAX;

                for (int i = 0; i < outN; i++)
                {
                    _goalLanePos[i] = tmpPos[i];
                    _goalLaneIdx[i] = tmpIdx[i];
                }

                return outN;
            }
            catch { }
            return 0;
        }


        
        private bool TryGetGoalEndPos(int goalIndex, out Vector3 endPos)
        {
            endPos = default(Vector3);
            try
            {
                if (_goalSeg == 0) return false;
                if (goalIndex < 0 || goalIndex >= _goalCount) return false;

                NetManager nm = NetManager.instance;
                if (object.ReferenceEquals(nm, null)) return false;

                NetSegment s = nm.m_segments.m_buffer[_goalSeg];

                // We want a point ON the goal segment, a bit away from the exit node,
                // so the path end is "deep enough" to actually lock a lane choice.
                ushort fromNode = _goalFromNode;
                ushort toNode = _goalToNode;

                if (fromNode == 0 || toNode == 0)
                {
                    // fallback
                    fromNode = s.m_startNode;
                    toNode = s.m_endNode;
                }

                Vector3 pFrom = nm.m_nodes.m_buffer[fromNode].m_position;
                Vector3 pTo = nm.m_nodes.m_buffer[toNode].m_position;

                // 85% into the segment away from the exit-side node
                Vector3 baseP = Vector3.Lerp(pFrom, pTo, 0.85f);

                Vector3 dir = pTo - pFrom;
                dir.y = 0f;
                float mag = dir.magnitude;
                if (mag < 0.001f) return false;
                dir = dir / mag;

                // right-hand normal in XZ plane
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);

                float lanePos = _goalLanePos[goalIndex];
                endPos = baseP + right * lanePos;
                return true;
            }
            catch { }
            return false;
        }

        private bool ApplyGoalLaneToPath(uint path, ushort goalSeg, byte laneIdx, out int changed)
        {
            changed = 0;
            if (path == 0u || goalSeg == 0) return false;

            try
            {
                A11_PathEdit.EnsureInit();
                PathManager pm = PathManager.instance;
                if (object.ReferenceEquals(pm, null)) return false;

                PathUnit[] buf = pm.m_pathUnits.m_buffer;
                if (object.ReferenceEquals(buf, null)) return false;
                if ((int)path < 0 || (int)path >= buf.Length) return false;

                uint u = path;
                int safety = 0;

                while (u != 0u && safety < 262144)
                {
                    safety++;

                    if ((int)u < 0 || (int)u >= buf.Length) break;

                    PathUnit unit = buf[(int)u];

                    // Box, mutate via SetPosition, then unbox back.
                    object boxed = (object)unit;
                    bool any = false;

                    for (int i = 0; i < 12; i++)
                    {
                        PathUnit.Position p;
                        if (!A11_PathEdit.TryGetPosition(unit, i, out p)) continue;
                        if (p.m_segment != goalSeg) continue;

                        if (p.m_lane != laneIdx)
                        {
                            p.m_lane = laneIdx;
                            if (A11_PathEdit.TrySetPosition(ref boxed, i, p))
                            {
                                changed++;
                                any = true;
                            }
                        }
                    }

                    if (any)
                    {
                        try { buf[(int)u] = (PathUnit)boxed; } catch { }
                    }

                    uint next = A11_PathEdit.GetNext(unit);
                    if (next == u) break;
                    u = next;
                }

                return changed > 0;
            }
            catch { return false; }
        }

        private bool ApplyGoalLaneAfterExitNodeToPath(uint path, ushort goalSeg, byte laneIdx, ushort exitNode, out ushort appliedSeg, out int changed)
        {
            appliedSeg = 0;
            changed = 0;

            if (path == 0u || goalSeg == 0 || exitNode == 0) return false;

            try
            {
                A11_PathEdit.EnsureInit();
                PathManager pm = PathManager.instance;
                if (object.ReferenceEquals(pm, null)) return false;

                PathUnit[] buf = pm.m_pathUnits.m_buffer;
                if (object.ReferenceEquals(buf, null)) return false;
                if ((int)path < 0 || (int)path >= buf.Length) return false;

                NetManager nm = NetManager.instance;

                bool passedExit = false;

                uint u = path;
                int safety = 0;

                while (u != 0u && safety < 262144)
                {
                    safety++;
                    if ((int)u < 0 || (int)u >= buf.Length) break;

                    PathUnit unit = buf[(int)u];
                    object boxed = (object)unit;
                    bool any = false;

                    for (int i = 0; i < 12; i++)
                    {
                        PathUnit.Position p;
                        if (!A11_PathEdit.TryGetPosition(unit, i, out p)) continue;
                        if (p.m_segment == 0) continue;

                        // Detect passing the exit node (by segment's nodes).
                        if (!passedExit)
                        {
                            try
                            {
                                if (!object.ReferenceEquals(nm, null))
                                {
                                    NetSegment s = nm.m_segments.m_buffer[p.m_segment];
                                    if (s.m_startNode == exitNode || s.m_endNode == exitNode) passedExit = true;
                                }
                            }
                            catch { }
                        }

                        if (passedExit && p.m_segment == goalSeg)
                        {
                            appliedSeg = goalSeg;
                            if (p.m_lane != laneIdx)
                            {
                                p.m_lane = laneIdx;
                                if (A11_PathEdit.TrySetPosition(ref boxed, i, p))
                                {
                                    changed++;
                                    any = true;
                                }
                            }
                        }
                    }

                    if (any)
                    {
                        try { buf[(int)u] = (PathUnit)boxed; } catch { }
                        // If we changed at least one position on goalSeg after exit, we can stop early.
                        if (changed > 0) return true;
                    }

                    uint next = A11_PathEdit.GetNext(unit);
                    if (next == u) break;
                    u = next;
                }

                return changed > 0;
            }
            catch { return false; }
        }
    }
}
