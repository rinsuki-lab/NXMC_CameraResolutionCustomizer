using BepInEx;
using BepInEx.Logging;
using BepInEx.NET.Common;
using DirectShowLib;
using HarmonyLib;
using Microsoft.VisualBasic;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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
        var sb = new StringBuilder();
        // 本当はもうちょっと気にしないとダメな気がするが一旦決め打ちで
        var pin = DsFindPin.ByDirection(__result, PinDirection.Output, 0);
        var config = pin as IAMStreamConfig;

        config.GetNumberOfCapabilities(out var count, out var size);

        var data = Marshal.AllocHGlobal(size);
        
        for (int i=0; i<count; i++)
        {
            config.GetStreamCaps(i, out var mediaType, data); // TODO: 解放しなくていいんだっけ？
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
                sb.AppendLine($"{i}: {info.BmiHeader.Width} x {info.BmiHeader.Height}, subtype={subtypeName}");
            } else
            {
                sb.AppendLine($"{i}: 未知のFormatType({mediaType.formatType})");
            }
        }

        Plugin.Log.LogInfo($"ダイアログ内容: {sb.ToString()}");

        var result = Interaction.InputBox(sb.ToString(), "どのフォーマットを選択しますか？");
        var intresult = int.Parse(result);
        Plugin.Log.LogInfo($"intresult={intresult}");
        config.GetStreamCaps(intresult, out var finalMediaType, data);
        config.SetFormat(finalMediaType);

        // 後始末
        Marshal.FreeHGlobal(data);
    }
}