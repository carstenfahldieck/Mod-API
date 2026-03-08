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


        private void TrackResetCounts()
        {
            _trkActive = 0;
            _trkReached = 0;
            _trkDropped = 0;
            _trkProgress = 0;
        }


        private string TrackBestDistStats()
        {
            try
            {
                float min = 1e30f;
                int n = 0;
                int lt20 = 0, lt50 = 0, lt100 = 0, lt200 = 0;

                for (int i = 0; i < TRACK_MAX; i++)
                {
                    if (_track[i].vid == 0) continue;
                    n++;
                    float bd = _track[i].bestDist;
                    if (bd < min) min = bd;

                    if (bd < 20f) lt20++;
                    if (bd < 50f) lt50++;
                    if (bd < 100f) lt100++;
                    if (bd < 200f) lt200++;
                }

                if (n == 0) return "bestDist: n=0";
                return string.Format("bestDist: n={0} min={1:0.0} <20:{2} <50:{3} <100:{4} <200:{5}", n, min, lt20, lt50, lt100, lt200);
            }
            catch { }
            return "bestDist: err";
        }


        private void TrackClearAll()
        {
            try
            {
                for (int i = 0; i < TRACK_MAX; i++)
                {
                    _track[i].vid = 0;
                    _track[i].startTick = 0u;
                    _track[i].anchorSeg = 0;
                    _track[i].anchorPos = Vector3.zero;
                    _track[i].startDist = 0f;
                    _track[i].bestDist = 0f;
                    _track[i].active = 0;
                    _track[i].reached = 0;
                    _track[i].dropped = 0;
                }
                _trackWrite = 0;
                TrackResetCounts();
            }
            catch { }
        }


        internal void TrackAdd(ushort vid, ushort anchorSeg)
        {
            try
            {
                if (vid == 0) return;
                if (anchorSeg == 0) return;

                VehicleManager vm = VehicleManager.instance;
                if (object.ReferenceEquals(vm, null)) return;

                Vehicle data = vm.m_vehicles.m_buffer[vid];

                Vector3 anchorPos;
                if (!A11_PathSeg.TryGetSegmentMid(anchorSeg, out anchorPos)) return;

                ushort anchorNode = 0;
                Vector3 anchorNodePos = default(Vector3);
                try
                {
                    anchorNode = PickAnchorNode(anchorSeg, anchorPos);
                    if (anchorNode != 0) anchorNodePos = GetNodePos(anchorNode);
                }
                catch
                {
                    anchorNode = 0;
                    anchorNodePos = default(Vector3);
                }

                uint tick = 0;
                try { tick = SimulationManager.instance.m_currentTickIndex; } catch { tick = 0; }

                // lane diag
                byte ln = 255;
                byte off = 255;
                try
                {
                    PathUnit.Position p;
                    if (A11_PathSeg.TryGetPathPosition(vid, ref data, out p))
                    {
                        ln = p.m_lane;
                        off = p.m_offset;
                    }
                }
                catch { ln = 255; off = 255; }

                LaneHistAdd(ln, off);

                Vector3 vpos = data.GetLastFramePosition();
                float sd = (vpos - anchorPos).magnitude;

                TrackEntry e = new TrackEntry();
                e.vid = vid;
                e.startTick = tick;
                e.active = 1;
                e.reached = 0;
                e.dropped = 0;
                e.anchorSeg = anchorSeg;
                e.anchorNode = anchorNode;
                e.anchorPos = anchorPos;
                e.anchorNodePos = anchorNodePos;
                e.startDist = sd;
                e.bestDist = sd;
                e.startLane = ln;
                e.startOffset = off;

                _track[_trackWrite] = e;
                _trackWrite++;
                if (_trackWrite >= TRACK_MAX) _trackWrite = 0;
            }
            catch
            {
            }
        }


        private void TrackUpdateSome()
        {
            try
            {
                VehicleManager vm = VehicleManager.instance;
                if (object.ReferenceEquals(vm, null)) return;

                uint nowTick = SimulationManager.instance.m_currentTickIndex;
                Vehicle[] buf = vm.m_vehicles.m_buffer;

                int updated = 0;
                for (int i = 0; i < TRACK_MAX && updated < 16; i++)
                {
                    if (_track[i].active == 0) continue;

                    ushort vid = _track[i].vid;
                    if (vid == 0 || vid >= buf.Length)
                    {
                        _track[i].active = 0;
                        _track[i].dropped = 1;
                        updated++;
                        continue;
                    }

                    Vehicle v = buf[vid];

                    if ((v.m_flags & Vehicle.Flags.Created) == 0)
                    {
                        _track[i].active = 0;
                        _track[i].dropped = 1;
                        updated++;
                        continue;
                    }

                    Vector3 pos = v.GetLastFramePosition();
                    float d = (pos - _track[i].anchorPos).magnitude;

                    if (_track[i].bestDist <= 0f) _track[i].bestDist = d;
                    if (d < _track[i].bestDist) _track[i].bestDist = d;

                    ushort curSeg = A11_PathSeg.TryGetCurrentSegment(ref v);

                    bool hitAnchorNode = false;
                    if (_track[i].anchorNode != 0 && curSeg != 0)
                    {
                        try
                        {
                            NetManager nm = NetManager.instance;
                            NetSegment seg = nm.m_segments.m_buffer[curSeg];
                            if (seg.m_startNode == _track[i].anchorNode || seg.m_endNode == _track[i].anchorNode)
                            {
                                hitAnchorNode = true;
                            }
                            else if (_track[i].anchorNodePos != Vector3.zero)
                            {
                                float dn = (pos - _track[i].anchorNodePos).magnitude;
                                if (dn < 25f) hitAnchorNode = true;
                            }
                        }
                        catch { }
                    }

                    if (curSeg == _track[i].anchorSeg || hitAnchorNode || d < 20f)
                    {
                        _track[i].active = 0;
                        _track[i].reached = 1;
                        updated++;
                        continue;
                    }

                    if (nowTick - _track[i].startTick > 4096u)
                    {
                        _track[i].active = 0;
                        _track[i].dropped = 1;
                        updated++;
                        continue;
                    }

                    updated++;
                }
            }
            catch { }
        }


        private void TrackComputeCounts()
        {
            int a = 0, r = 0, dr = 0, pr = 0;
            for (int i = 0; i < TRACK_MAX; i++)
            {
                if (_track[i].active != 0) a++;
                if (_track[i].reached != 0) r++;
                if (_track[i].dropped != 0) dr++;

                if (_track[i].reached == 0 && _track[i].dropped == 0 && _track[i].active != 0)
                {
                    if (_track[i].startDist - _track[i].bestDist > 50f) pr++;
                }
            }

            _trkActive = a;
            _trkReached = r;
            _trkDropped = dr;
            _trkProgress = pr;
        }
    }
}
