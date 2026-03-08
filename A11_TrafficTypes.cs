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

private bool IsTruckAI(VehicleAI ai)
        {
            try
            {
                if (object.ReferenceEquals(ai, null)) return false;
                string n = ai.GetType().Name;
                if (object.ReferenceEquals(n, null)) return false;
                return n.IndexOf("CargoTruck", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { }
            return false;
        }

        private bool IsBusAI(VehicleAI ai)
        {
            try
            {
                if (object.ReferenceEquals(ai, null)) return false;
                string n = ai.GetType().Name;
                if (object.ReferenceEquals(n, null)) return false;
                return n.IndexOf("Bus", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { }
            return false;
        }

        private bool IsPassengerAI(VehicleAI ai)
        {
            try
            {
                if (object.ReferenceEquals(ai, null)) return false;
                string n = ai.GetType().Name;
                if (object.ReferenceEquals(n, null)) return false;
                return n.IndexOf("PassengerCar", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { }
            return false;
        }

        private bool IsEmergencyAI(VehicleAI ai)
        {
            try
            {
                if (object.ReferenceEquals(ai, null)) return false;
                string n = ai.GetType().Name;
                if (object.ReferenceEquals(n, null)) return false;

                // Conservative name-based detection (works without relying on VehicleType enums)
                if (n.IndexOf("Ambulance", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("FireTruck", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("Police", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("Hearse", StringComparison.OrdinalIgnoreCase) >= 0) return true; // treat as priority-ish
                return false;
            }
            catch { }
            return false;
        }
    }
}
