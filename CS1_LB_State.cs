using System.Threading;

namespace TaxiMod
{
    internal static class CS1_LB_State
    {
        public static volatile bool Enabled = false;

        // Debug / Probe-Zähler (Main Thread)
        public static int TickA;
        public static int TickB;
        public static int TickC;

        // PathFind thread Counters (Interlocked!)
        public static int PF_ProcessItemCalls;
        public static int PF_ProcessItemLaneHits;

        // Ein paar “Beweise”, dass echte Daten durchlaufen:
        public static int PF_LaneIdXor;     // XOR über laneIDs (ändert sich, wenn laneIDs kommen)
        public static int PF_SegIdXor;      // XOR über segmentIDs
        public static volatile uint PF_LastLaneId;
        public static volatile ushort PF_LastSegId;

        public static void ResetPathCounters()
        {
            Interlocked.Exchange(ref PF_ProcessItemCalls, 0);
            Interlocked.Exchange(ref PF_ProcessItemLaneHits, 0);
            Interlocked.Exchange(ref PF_LaneIdXor, 0);
            Interlocked.Exchange(ref PF_SegIdXor, 0);
            PF_LastLaneId = 0;
            PF_LastSegId = 0;
        }
    }
}
