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
    public class ResourceCostScalingPlugin : BaseUnityPlugin
    {
        private const string PLUGIN_GUID = PluginInfo.PLUGIN_GUID;
        private const string PLUGIN_NAME = "Resource Cost Scaling";
        private const string PLUGIN_VERSION = PluginInfo.PLUGIN_VERSION;
        private const string PLUGIN_MIN_VERSION = "0.3.0";
        
        private static ConfigSync configSync;
        private static ConfigEntry<float> scaleFactor;
        
        private static ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource(PLUGIN_NAME);
        
        public void Awake()
        {
            configSync = new (PLUGIN_GUID) {
                DisplayName = PLUGIN_NAME,
                CurrentVersion = PluginInfo.PLUGIN_VERSION,
                MinimumRequiredVersion = PLUGIN_MIN_VERSION
            };

            configSync.AddLockingConfigEntry(config("General", "0. Force Server Config", true, "Force clients to use the server's configuration settings."));
            scaleFactor = config("General", "1. Scale Factor", 1.0f, new ConfigDescription(
                "Multiply all resource costs by this value. Midpoints round to even numbers. Minimum result of 1.",
                new RoundedValueRange(0.0f, 2.0f, 0.01f))
            );
            
            new Harmony(PLUGIN_GUID).PatchAll(typeof(ResourceCostScalingPatches));
        }

        class ResourceCostScalingPatches
        {
            private static readonly MethodInfo m_ScaleResource = AccessTools.Method(typeof(ResourceCostScalingPatches), "ScaleResource");
            private static FieldInfo f_Amount = AccessTools.Field(typeof(Piece.Requirement), "m_amount");
            private static FieldInfo f_AmountPerLevel = AccessTools.Field(typeof(Piece.Requirement), "m_amountPerLevel");
            private static bool discovering = false;

            private static int ScaleResource(int amount)
            {
                return (scaleFactor.Value == 0) ? discovering ? 1 : 0
                    : (amount > 0) ? Math.Max(1, (int)Math.Round(amount * scaleFactor.Value))
                    : (amount < 0) ? 5 * Math.Max(1, (int)Math.Round((-amount) * scaleFactor.Value))
                    : 0;
            }

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

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
            static bool HaveRequirementItemsPrefix(bool discover)
            {
                if (discover && scaleFactor.Value == 0) {
                    discovering = true;
                }
                return true;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(Player), "HaveRequirements", new[] { typeof(Piece), typeof(Player.RequirementMode) })]
            static bool HaveRequirementsPrefix(Player.RequirementMode mode)
            {
                if (mode == Player.RequirementMode.IsKnown && scaleFactor.Value == 0) {
                    discovering = true;
                }
                return true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
            [HarmonyPatch(typeof(Player), "HaveRequirements", new[] { typeof(Piece), typeof(Player.RequirementMode) })]
            static void DiscoveringPostfix()
            {
                discovering = false;
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

        class RoundedValueRange : AcceptableValueRange<float>
        {
            private float step;
            public RoundedValueRange(float min, float max, float step = 0.01f) : base(min, max)
            {
                this.step = step;
            }
            public override object Clamp(object value)
            {
                float v = Convert.ToSingle(value) + (0.5f * step);
                return (v < this.MinValue) ? this.MinValue : (v > this.MaxValue) ? this.MaxValue : (float)Math.Round(v - (v % step), 2);
            }
            public override bool IsValid(object value)
            {
                float v = Convert.ToSingle(value);
                return (v < this.MinValue) && (v > this.MaxValue) && (v % step == 0);
            }
        }
    }
}
