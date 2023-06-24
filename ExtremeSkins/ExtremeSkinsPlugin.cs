global using ExtremeRoles.Extension.Translation;

using BepInEx;
using BepInEx.Unity.IL2CPP;

using HarmonyLib;

using ExtremeSkins.SkinManager;
using ExtremeRoles.Module;


namespace ExtremeSkins;

[BepInAutoPlugin("me.yukieiji.extremeskins", "Extreme Skins")]
[BepInDependency(
    ExtremeRoles.ExtremeRolesPlugin.Id,
    BepInDependency.DependencyFlags.HardDependency)] // Never change it!
[BepInProcess("Among Us.exe")]
public partial class ExtremeSkinsPlugin : BasePlugin
{
    public Harmony Harmony { get; } = new Harmony(Id);

    public static ExtremeSkinsPlugin Instance;

    internal static BepInEx.Logging.ManualLogSource Logger;

    public const string SkinComitCategory = "SkinComit";

    public override void Load()
    {
        Logger = Log;

        Instance = this;

		CreatorModeManager.Initialize();

#if WITHHAT
        ExtremeHatManager.Initialize();
#endif
#if WITHNAMEPLATE
        ExtremeNamePlateManager.Initialize();
#endif
#if WITHVISOR
        ExtremeVisorManager.Initialize();
#endif

		ExtremeColorManager.Initialize();

        VersionManager.PlayerVersion.Clear();

        Harmony.PatchAll();

        var assembly = System.Reflection.Assembly.GetAssembly(this.GetType());

		var translator = ExtremeSkinsTranslator.Instance;
		ExtremeRoles.Module.NewTranslation.TranslatorManager.Register(translator);

		Updater.Instance.AddMod<ExRRepositoryInfo>($"{assembly.GetName().Name}.dll");
        Il2CppRegisterAttribute.Registration(assembly);
    }
}
