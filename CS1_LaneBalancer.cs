// CS1_LaneBalancer.cs
// Single-file version (Mod + Loading + Logger + HUD + Patches)
// LOG is always written to Desktop and overwritten on load.

using System;
using System.IO;
using System.Reflection;
using ICities;
using UnityEngine;
using ColossalFramework;
using ColossalFramework.UI;

// Harmony: try HarmonyLib first (your runtime showed HarmonyLib.HarmonyException earlier)
using HarmonyLib;

public class CS1_LaneBalancer : IUserMod
{
    public string Name
    {
        get { return "CS1 LaneBalancer (Probe)"; }
    }

    public string Description
    {
        get { return "Traffic probe: hooks PathFind methods and shows counters (F4 toggle, F5 reset)."; }
    }
}

public class CS1_LaneBalancer_Loading : LoadingExtensionBase
{
    private static bool _installed;
    private static GameObject _go;
    private static CS1_LB_Runner _runner;

    public override void OnLevelLoaded(LoadMode mode)
    {
        // Only in-game (not asset editor)
        try
        {
            CS1_LB_Log.InitOverwriteDesktop();
            CS1_LB_Log.Write("OnLevelLoaded: " + mode);

            InstallOnce();

            if (_go == null)
            {
                _go = new GameObject("CS1_LaneBalancer_Runner");
                UnityEngine.Object.DontDestroyOnLoad(_go);
                _runner = _go.AddComponent<CS1_LB_Runner>();
                CS1_LB_Log.Write("Runner created");
            }
        }
        catch (Exception ex)
        {
            CS1_LB_Log.Write("ERROR OnLevelLoaded: " + ex);
        }
    }

    public override void OnLevelUnloading()
    {
        try
        {
            CS1_LB_Log.Write("OnLevelUnloading");

            if (_go != null)
            {
                UnityEngine.Object.Destroy(_go);
                _go = null;
                _runner = null;
            }

            // We intentionally do NOT unpatch on unload (keeps it simple/stable).
        }
        catch (Exception ex)
        {
            CS1_LB_Log.Write("ERROR OnLevelUnloading: " + ex);
        }
    }

    private static void InstallOnce()
    {
        if (_installed) return;
        _installed = true;

        CS1_LB_Log.Write("InstallOnce()");

        try
        {
            var harmony = new Harmony("cs1.lanebalancer.probe");

            Type tPathFind = typeof(PathManager).Assembly.GetType("PathFind");
            if (tPathFind == null)
            {
                CS1_LB_Log.Write("ERROR: Type PathFind not found");
                return;
            }

            // Patch: bool PathFind.CalculatePath(uint unit, bool skipQueue)
            MethodInfo mCalculatePath = tPathFind.GetMethod(
                "CalculatePath",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(uint), typeof(bool) },
                null
            );

            if (mCalculatePath != null)
            {
                var pre = new HarmonyMethod(typeof(CS1_LB_Patches).GetMethod("PF_CalculatePath_Prefix", BindingFlags.Static | BindingFlags.Public));
                harmony.Patch(mCalculatePath, pre, null, null);
                CS1_LB_Log.Write("Patched PathFind.CalculatePath(uint,bool)");
            }
            else
            {
                CS1_LB_Log.Write("WARN: PathFind.CalculatePath(uint,bool) not found");
            }

            // Patch: void PathFind.PathFindImplementation(uint unit, ref PathUnit data)
            // NOTE: PathUnit is a struct in Assembly-CSharp. We can reference it directly.
            MethodInfo mImpl = tPathFind.GetMethod(
                "PathFindImplementation",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            // We match by name + param count (2) to avoid signature surprises.
            if (mImpl != null)
            {
                ParameterInfo[] ps = mImpl.GetParameters();
                if (ps != null && ps.Length == 2)
                {
                    var pre2 = new HarmonyMethod(typeof(CS1_LB_Patches).GetMethod("PF_Impl_Prefix", BindingFlags.Static | BindingFlags.Public));
                    harmony.Patch(mImpl, pre2, null, null);
                    CS1_LB_Log.Write("Patched PathFind.PathFindImplementation(..)");
                }
                else
                {
                    CS1_LB_Log.Write("WARN: PathFindImplementation found but signature unexpected");
                }
            }
            else
            {
                CS1_LB_Log.Write("WARN: PathFind.PathFindImplementation not found");
            }

            // Patch: float PathFind.CalculateLaneSpeed(byte startOffset, byte endOffset, ref NetSegment segment, ref NetInfo.Lane lane)
            MethodInfo mLaneSpeed = tPathFind.GetMethod(
                "CalculateLaneSpeed",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            );

            if (mLaneSpeed != null)
            {
                // Postfix to count calls and optionally bias result later.
                var post = new HarmonyMethod(typeof(CS1_LB_Patches).GetMethod("PF_CalcLaneSpeed_Postfix", BindingFlags.Static | BindingFlags.Public));
                harmony.Patch(mLaneSpeed, null, post, null);
                CS1_LB_Log.Write("Patched PathFind.CalculateLaneSpeed(..)");
            }
            else
            {
                CS1_LB_Log.Write("WARN: PathFind.CalculateLaneSpeed not found");
            }
        }
        catch (Exception ex)
        {
            CS1_LB_Log.Write("ERROR InstallOnce: " + ex);
        }
    }
}

public static class CS1_LB_State
{
    public static bool Enabled = true;

    // Counters
    public static long A_CalculatePath;
    public static long B_PathFindImpl;
    public static long C_CalcLaneSpeed;

    public static void Reset()
    {
        A_CalculatePath = 0;
        B_PathFindImpl = 0;
        C_CalcLaneSpeed = 0;
        CS1_LB_Log.Write("ResetPathCounters()");
    }
}

public static class CS1_LB_Patches
{
    // Prefix for PathFind.CalculatePath
    public static void PF_CalculatePath_Prefix()
    {
        if (!CS1_LB_State.Enabled) return;
        CS1_LB_State.A_CalculatePath++;
    }

    // Prefix for PathFind.PathFindImplementation
    public static void PF_Impl_Prefix()
    {
        if (!CS1_LB_State.Enabled) return;
        CS1_LB_State.B_PathFindImpl++;
    }

    // Postfix for PathFind.CalculateLaneSpeed
    public static void PF_CalcLaneSpeed_Postfix(ref float __result)
    {
        if (!CS1_LB_State.Enabled) return;
        CS1_LB_State.C_CalcLaneSpeed++;

        // For now: ONLY probe (no gameplay change).
        // Later we can adjust __result here to influence lane choices.
    }
}

public class CS1_LB_Runner : MonoBehaviour
{
    private CS1_LB_HUD _hud;
    private float _nextHudUpdate;

    public void Start()
    {
        try
        {
            CreateHud();
        }
        catch (Exception ex)
        {
            CS1_LB_Log.Write("ERROR Runner.Start: " + ex);
        }
    }

    private void CreateHud()
    {
        if (_hud != null) return;

        UIView view = UIView.GetAView();
        if (view == null)
        {
            CS1_LB_Log.Write("WARN: UIView.GetAView() returned null (HUD not created yet)");
            return;
        }

        // Non-generic AddUIComponent (your environment complained about generic use before)
        _hud = (CS1_LB_HUD)view.AddUIComponent(typeof(CS1_LB_HUD));
        CS1_LB_Log.Write("HUD created");
    }

    public void Update()
    {
        // Make sure HUD exists (sometimes view comes later)
        if (_hud == null)
        {
            CreateHud();
        }

        // Hotkeys
        if (Input.GetKeyDown(KeyCode.F4))
        {
            CS1_LB_State.Enabled = !CS1_LB_State.Enabled;
            CS1_LB_Log.Write("Toggle Enabled=" + CS1_LB_State.Enabled);
        }
        if (Input.GetKeyDown(KeyCode.F5))
        {
            CS1_LB_State.Reset();
        }

        // HUD update ~4x/sec
        if (_hud != null)
        {
            float t = Time.realtimeSinceStartup;
            if (t >= _nextHudUpdate)
            {
                _nextHudUpdate = t + 0.25f;
                _hud.SetText(
                    CS1_LB_State.Enabled,
                    CS1_LB_State.A_CalculatePath,
                    CS1_LB_State.B_PathFindImpl,
                    CS1_LB_State.C_CalcLaneSpeed
                );
            }
        }
    }

    public void OnDestroy()
    {
        try
        {
            if (_hud != null)
            {
                UnityEngine.Object.Destroy(_hud);
                _hud = null;
            }
        }
        catch (Exception ex)
        {
            CS1_LB_Log.Write("ERROR Runner.OnDestroy: " + ex);
        }
    }
}

public class CS1_LB_HUD : UIPanel
{
    private UILabel _label;

    public override void Start()
    {
        base.Start();

        // Panel layout
        this.backgroundSprite = "GenericPanel";
        this.color = new Color32(0, 0, 0, 170);

        this.width = 420f;
        this.height = 110f; // higher, so bottom line is visible
        this.relativePosition = new Vector3(15f, 70f);

        _label = this.AddUIComponent<UILabel>();
        _label.autoSize = false;
        _label.width = this.width - 10f;
        _label.height = this.height - 10f;
        _label.relativePosition = new Vector3(5f, 5f);
        _label.textScale = 0.9f;
        _label.wordWrap = false;

        _label.text =
            "CS1 LaneBalancer (Probe)\n" +
            "F4 Toggle | F5 Reset\n" +
            "Enabled: ? | A: 0 | B: 0 | C: 0";
    }

    public void SetText(bool enabled, long a, long b, long c)
    {
        if (_label == null) return;

        _label.text =
            "CS1 LaneBalancer (Probe)\n" +
            "F4 Toggle | F5 Reset\n" +
            "Enabled: " + enabled +
            " | A(CalcPath): " + a +
            " | B(Impl): " + b +
            " | C(LaneSpeed): " + c;
    }
}

public static class CS1_LB_Log
{
    private static string _path;
    private static object _lockObj = new object();

    public static void InitOverwriteDesktop()
    {
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            _path = Path.Combine(desktop, "CS1_LaneBalancer.LOG");

            // overwrite
            using (var sw = new StreamWriter(_path, false))
            {
                sw.WriteLine("=== CS1 LaneBalancer LOG ===");
                sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sw.WriteLine();
            }
        }
        catch
        {
            // swallow - last resort
        }
    }

    public static void Write(string msg)
    {
        try
        {
            if (string.IsNullOrEmpty(_path))
            {
                // fallback init
                InitOverwriteDesktop();
            }

            string line = DateTime.Now.ToString("HH:mm:ss") + " | " + msg;

            lock (_lockObj)
            {
                using (var sw = new StreamWriter(_path, true))
                {
                    sw.WriteLine(line);
                }
            }
        }
        catch
        {
            // swallow
        }
    }
}
