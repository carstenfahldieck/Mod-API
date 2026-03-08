using System;
using UnityEngine;

namespace CS1_LaneBalancer
{
    // ===============================================================
    // A11_TrafficHelper (Patch 064)
    // Sichere Hilfsdatei – noch NICHT im Controller benutzt.
    // Nur damit wir neue Dateien stabil in das Projekt integrieren.
    // ===============================================================

    public static class A11_TrafficHelper
    {
        // Platzhalter für spätere Stau-Erkennung
        public static int CountVehiclesOnSegment(ushort segmentId)
        {
            if (segmentId == 0)
                return 0;

            try
            {
                VehicleManager vm = VehicleManager.instance;
                if (vm == null)
                    return 0;

                int count = 0;

                for (ushort i = 1; i < vm.m_vehicles.m_size; i++)
                {
                    Vehicle v = vm.m_vehicles.m_buffer[i];

                    if ((v.m_flags & Vehicle.Flags.Created) == 0)
                        continue;

                    // Segmentprüfung kommt später
                    count++;
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        public static bool IsCongested(ushort segmentId)
        {
            return CountVehiclesOnSegment(segmentId) > 20;
        }
    }
}
