using BepInEx;
using BepInEx.Logging;
using BepInEx.NET.Common;
using DirectShowLib;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace NXMC_CameraResolutionCustomizer;

[BepInPlugin("net.rinsuki.mods.NXMC.CameraResolutionCustomizer", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} is loaded!");
        HarmonyInstance.PatchAll();
    }

    public static Assembly FindExeAssembly()
    {
        // Assembly.GetEntryAssembly は使えない (Plugin.Load() が AssemblyFixes.Exec より前に呼ばれるため entry が BepInEx のものになる)
        var exeAsm = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name == "NX Macro Controller").Last();
        Log.LogInfo($"exeAsm={exeAsm.GetName()}");
        return exeAsm;
    }
}

[HarmonyPatch]
class DSHDMICapturePatches
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.GetTypesFromAssembly(Plugin.FindExeAssembly())
            .Where(t => t.Name == "DSHDMICapture")
            .SelectMany(t => t.GetRuntimeMethods())
            .Where(m => m.Name == "GetCaptureDevice" && m.ReturnType == typeof(IBaseFilter))
            .First();
    }

    [HarmonyPostfix]
    public static void GetCaptureDevice_Postfix(int deviceIndex, IBaseFilter __result)
    {
        if (__result == null)
        {
            return;
        }

        using var form = new Form1();

        var sb = new StringBuilder();
        // 本当はもうちょっと気にしないとダメな気がするが一旦決め打ちで
        var pin = DsFindPin.ByDirection(__result, PinDirection.Output, 0);
        var config = pin as IAMStreamConfig;

        config.GetNumberOfCapabilities(out var count, out var size);

        var data = Marshal.AllocHGlobal(size);

        form.BeginListUpdate();
        
        for (int i=0; i<count; i++)
        {
            config.GetStreamCaps(i, out var mediaType, data); // TODO: 解放しなくていいんだっけ？
            string line = "";
            if (mediaType.formatType == FormatType.VideoInfo)
            {
                var info = Marshal.PtrToStructure<VideoInfoHeader>(mediaType.formatPtr);
                // subtype の表示名を探す旅
                var subtypeName = "???";
                foreach (var p in typeof(MediaSubType).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
                {
                    if (p.FieldType != typeof(Guid)) continue;
                    if (((Guid)p.GetValue(null)) == mediaType.subType)
                    {
                        subtypeName = p.Name;
                        break;
                    }
                }
                line = $"{i}: {info.BmiHeader.Width} x {info.BmiHeader.Height}, subtype={subtypeName}";
            } else
            {
                line = $"{i}: 未知のFormatType({mediaType.formatType})";
            }
            form.AddItem(line);
            sb.AppendLine(line);
        }

        Plugin.Log.LogInfo($"ダイアログ内容: {sb.ToString()}");

        form.EndListUpdate();
        form.ShowDialog();
        var intresult = form.CurrentSelectedItemIndex();
        Plugin.Log.LogInfo($"intresult={intresult}");
        config.GetStreamCaps(intresult, out var finalMediaType, data);
        config.SetFormat(finalMediaType);

        // 後始末
        Marshal.FreeHGlobal(data);
    }
}

[HarmonyPatch]
class ConsoleIconPatches
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.GetTypesFromAssembly(Plugin.FindExeAssembly())
            .Where(t => t.Name == "NXMC_VxV")
            .SelectMany(t => t.GetRuntimeMethods())
            .Where(m => m.Name == "InitializeComponent")
            .First();
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr intPtr, int msg, IntPtr wParam, IntPtr lParam);

    [HarmonyPostfix]
    public static void InitializeComponent_PostFix(Form __instance)
    {
        IntPtr cw = GetConsoleWindow();
        if (cw == IntPtr.Zero)
        {
            // BepInEx のコンソール設定切ってる?
            return;
        }

        const int WM_SETICON = 0x0080;


        SendMessage(cw, WM_SETICON, (IntPtr)0 /* ICON_BIG */, __instance.Icon.Handle);
        SendMessage(cw, WM_SETICON, (IntPtr)1 /* ICON_SMALL */, __instance.Icon.Handle);
    }
}