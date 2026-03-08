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

    internal static class A11_Log
    {
        private static bool _inited;
        private static string _path;
        private static StringBuilder _sb;
        private static float _nextFlushAt;
        private static int _lines;

        public static string ShortPath = "";
        public static string LastIo = "";

        public static void Init()
        {
            if (_inited) return;
            _inited = true;

            _sb = new StringBuilder(16 * 1024);
            _lines = 0;

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string name = "CS1_LaneBalancer_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
                _path = Path.Combine(desktop, name);
                ShortPath = name;
                LastIo = "ok";
                Write("LogPath=" + _path);
                Flush(true);
            }
            catch (Exception ex)
            {
                _path = null;
                ShortPath = "no path";
                LastIo = ex.GetType().Name + ":" + ex.Message;
            }

            _nextFlushAt = Time.realtimeSinceStartup + 2.0f;
        }

        public static void Write(string line)
        {
            if (!_inited) Init();
            if (object.ReferenceEquals(_path, null)) return;

            try
            {
                _sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
                _sb.Append(" | ");
                _sb.AppendLine(line);
                _lines++;
                if (_lines > 450) Flush(false);
            }
            catch (Exception ex)
            {
                LastIo = ex.GetType().Name + ":" + ex.Message;
            }
        }

        public static void Flush(bool force)
        {
            if (!_inited) return;
            if (object.ReferenceEquals(_path, null)) return;

            try
            {
                if (!force && Time.realtimeSinceStartup < _nextFlushAt) return;
                _nextFlushAt = Time.realtimeSinceStartup + 2.0f;

                if (_sb.Length == 0) return;

                File.AppendAllText(_path, _sb.ToString(), Encoding.UTF8);
                _sb.Length = 0;
                _lines = 0;
                LastIo = "ok";
            }
            catch (Exception ex)
            {
                LastIo = ex.GetType().Name + ":" + ex.Message;
            }
        }
    }
}
