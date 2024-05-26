using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ServerSync;
using BepInEx.Logging;

namespace ResourceCostScaling
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInProcess("valheim.exe")]
    public class ResourceCostScalingPlugin : BaseUnityPlugin
    {
        private const string PLUGIN_GUID = PluginInfo.PLUGIN_GUID;
        private const string PLUGIN_NAME = "Resource Cost Scaling";
        private const string PLUGIN_VERSION = PluginInfo.PLUGIN_VERSION;
        private const string PLUGIN_MIN_VERSION = "0.1.0";
        
        private static ConfigSync configSync;
        private static ConfigEntry<double> scaleFactor;
        
        private static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(PLUGIN_NAME);
        

        public void Awake()
        {
            configSync = new (PLUGIN_GUID) {
                DisplayName = PLUGIN_NAME,
                CurrentVersion = PluginInfo.PLUGIN_VERSION,
                MinimumRequiredVersion = PLUGIN_MIN_VERSION
            };

            configSync.AddLockingConfigEntry(config("General", "configLocked", true, "Force Server Config"));
            scaleFactor = config("General", "scaleFactor", 1.0, new ConfigDescription(
                "Multiply all resource costs by this value. Round up if resulting decimal is over 0.1. Minimum result of 1.",
                new RoundedValueRange(0.0, 2.0, 0.05))
            );
            
            new Harmony(PLUGIN_GUID).PatchAll(typeof(ResourceCostScalingPatches));
        }

        private static int ScaleResource(int amount)
        {
            return (amount > 0) ? Math.Max(1, (int)Math.Ceiling((amount * scaleFactor.Value) - 0.11))
                : (amount < 0) ? 5 * Math.Max(1, (int)Math.Ceiling(((-amount) * scaleFactor.Value) - 0.11))
                : 0;
        }

        class ResourceCostScalingPatches
        {
            private static readonly MethodInfo m_ScaleResource = AccessTools.Method(typeof(ResourceCostScalingPlugin), "ScaleResource");
            private static FieldInfo f_Amount = AccessTools.Field(typeof(Piece.Requirement), "m_amount");
            private static FieldInfo f_AmountPerLevel = AccessTools.Field(typeof(Piece.Requirement), "m_amountPerLevel");

            [HarmonyPostfix]
            [HarmonyPatch(typeof(ZNetScene), "Awake")]
            [HarmonyPriority(Priority.Last)]
            static void ZNetSceneAwakePostfix()
            {
                Recipe recipe = ObjectDB.instance.m_recipes.Find((r) => r.name.Equals("Recipe_Bronze5"));
                if (recipe == null)
                {
                    logger.LogWarning("Failed to find RecipeBronze5");
                    return;
                }
                foreach (Piece.Requirement req in recipe.m_resources)
                {
                    req.m_amount = -req.m_amount / 5;
                }
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(Piece), "DropResources")]
            [HarmonyPatch(typeof(Piece.Requirement), "GetAmount")]
            [HarmonyPatch(typeof(Player), "HaveRequirements", new[] { typeof(Piece), typeof(Player.RequirementMode) })]
            static IEnumerable<CodeInstruction> AmountsTranspiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    yield return instruction;
                    if (instruction.LoadsField(f_Amount) || instruction.LoadsField(f_AmountPerLevel))
                    {
                        yield return new CodeInstruction(OpCodes.Call, m_ScaleResource);
                    }
                }
            }
        }

        ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
            => config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);
            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;
            return configEntry;
        }

        class RoundedValueRange : AcceptableValueRange<double>
        {
            private double step;
            public RoundedValueRange(double min, double max, double step = 0.01f) : base(min, max)
            {
                this.step = step;
            }
            public override object Clamp(object value)
            {
                double v = Convert.ToDouble(value) + (0.5 * step);
                return (v < this.MinValue) ? this.MinValue : (v > this.MaxValue) ? this.MaxValue : v - (v % step);
            }
            public override bool IsValid(object value)
            {
                double v = Convert.ToDouble(value);
                return (v < this.MinValue) && (v > this.MaxValue) && (v % step == 0);
            }
        }
    }
}
