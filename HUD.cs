using System;
using System.Collections.Generic;
using UnityEngine;

namespace CS1_LaneBalancer
{
    public class HudController : MonoBehaviour
    {
        private Rect _window = new Rect(20, 20, 620, 420);
        private Vector2 _scroll;

        void OnGUI()
        {
            _window = GUI.Window(991133, _window, DrawWindow, "LaneBalancer");
        }

        void DrawWindow(int id)
        {
           GUILayout.BeginVertical();

			// Build / Mod Version
			if (!string.IsNullOrEmpty(HudData.GetLine0()))
				GUILayout.Label(HudData.GetLine0());

			if (!string.IsNullOrEmpty(HudData.GetLine1()))
				GUILayout.Label(HudData.GetLine1());

			// Datum + Uhrzeit
			GUILayout.Label(DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss"));

			GUILayout.Space(10);

            _scroll = GUILayout.BeginScrollView(_scroll);

            if (HudData.Rows != null)
            {
                foreach (HudRow row in HudData.Rows)
                {
                    GUILayout.BeginHorizontal();

                    GUILayout.Label(row.Name, GUILayout.Width(90));

                    if (row.Values != null)
                    {
                        foreach (string v in row.Values)
                        {
                            GUILayout.Label(v);
                        }
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUI.DragWindow();
        }
    }

    public static class HudData
    {
        private static string _line0 = "";
        private static string _line1 = "";
        private static string _line2 = "";

        public static List<HudRow> Rows = new List<HudRow>();

        public static void SetLine0(string s)
        {
            _line0 = s ?? "";
        }

        public static void SetLine1(string s)
        {
            _line1 = s ?? "";
        }

        public static void SetLine2(string s)
        {
            _line2 = s ?? "";
        }

        public static string GetLine0()
        {
            return _line0;
        }

        public static string GetLine1()
        {
            return _line1;
        }

        public static string GetLine2()
        {
            return _line2;
        }

        public static void ClearRows()
        {
            Rows.Clear();
        }

        public static void AddRow(HudRow r)
        {
            Rows.Add(r);
        }
    }

    public class HudRow
    {
        public string Name;
        public List<string> Values = new List<string>();

        public HudRow(string name)
        {
            Name = name;
        }

        public void Add(string v)
        {
            Values.Add(v);
        }
    }
}