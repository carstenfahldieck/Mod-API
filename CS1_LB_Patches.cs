using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace TaxiMod
{
    internal static class CS1_LB_Patches
    {
        private static bool _installed;
        private static Harmony _h;

        public static void InstallOnce()
        {
            if (_installed) return;
            _installed = true;

            try
            {
                _h = new Harmony("taximod.cs1.lanebalancer.probe");

                // Wir patchen NICHT BufferItem direkt (private),
                // sondern die ProcessItem-Überladung, die laneID(uint) enthält.
                MethodInfo target = Find_PathFind_ProcessItem_WithLaneId();
                if (target == null)
                {
                    CS1_LB_Logger.Log("PATCH: Target ProcessItem(... laneID ...) NOT FOUND.");
                    return;
                }

                MethodInfo prefix = typeof(CS1_LB_Patches).GetMethod("PF_ProcessItem_Prefix",
                    BindingFlags.Static | BindingFlags.NonPublic);

                // Harmony 1.x: Patch(original, prefix, postfix, transpiler)
                _h.Patch(target, new HarmonyMethod(prefix), null, null);

                CS1_LB_Logger.Log("PATCH: Installed => " + target.DeclaringType.FullName + "::" + target.Name);
            }
            catch (Exception ex)
            {
                CS1_LB_Logger.Log("PATCH ERROR: " + ex);
            }
        }

        private static MethodInfo Find_PathFind_ProcessItem_WithLaneId()
        {
            try
            {
                // PathFind ist im global namespace (Assembly-CSharp)
                Type t = Type.GetType("PathFind, Assembly-CSharp");
                if (t == null)
                {
                    CS1_LB_Logger.Log("Find: Type PathFind not resolved via Type.GetType");
                    return null;
                }

                MethodInfo[] ms = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < ms.Length; i++)
                {
                    MethodInfo m = ms[i];
                    if (m == null) continue;
                    if (m.Name != "ProcessItem") continue;

                    ParameterInfo[] ps = m.GetParameters();
                    if (ps == null || ps.Length != 8) continue;

                    // Erwartet:
                    // (BufferItem, ushort, bool, ushort, ref NetSegment, uint laneID, byte, byte)
                    // Wir prüfen nur die "sicheren" Marker:
                    // ps[2] bool, ps[3] ushort, ps[5] uint, ps[6] byte, ps[7] byte
                    if (ps[2].ParameterType != typeof(bool)) continue;
                    if (ps[3].ParameterType != typeof(ushort)) continue;
                    if (ps[5].ParameterType != typeof(uint)) continue;
                    if (ps[6].ParameterType != typeof(byte)) continue;
                    if (ps[7].ParameterType != typeof(byte)) continue;

                    return m;
                }
            }
            catch (Exception ex)
            {
                CS1_LB_Logger.Log("Find ERROR: " + ex);
            }

            return null;
        }

        // Prefix: erster Parameter ist BufferItem (private) -> wir nehmen object, damit es kompiliert.
        private static void PF_ProcessItem_Prefix(
            object item,
            ushort itemId,
            bool targetDisabled,
            ushort segmentId,
            ref NetSegment segment,
            uint laneID,
            byte offset,
            byte connectOffset)
        {
            try
            {
                // PathFind läuft i.d.R. im Worker-Thread -> KEIN Unity API hier.
                Interlocked.Increment(ref CS1_LB_State.PF_ProcessItemCalls);

                if (laneID != 0)
                {
                    Interlocked.Increment(ref CS1_LB_State.PF_ProcessItemLaneHits);

                    CS1_LB_State.PF_LastLaneId = laneID;
                    CS1_LB_State.PF_LastSegId = segmentId;

                    // XOR-Accumulator (als “es passiert wirklich was”)
                    Interlocked.Exchange(ref CS1_LB_State.PF_LaneIdXor,
                        CS1_LB_State.PF_LaneIdXor ^ unchecked((int)laneID));
                    Interlocked.Exchange(ref CS1_LB_State.PF_SegIdXor,
                        CS1_LB_State.PF_SegIdXor ^ (int)segmentId);
                }
            }
            catch
            {
                // NIEMALS Exceptions aus dem PathFind thread nach außen werfen
            }
        }
    }
}
