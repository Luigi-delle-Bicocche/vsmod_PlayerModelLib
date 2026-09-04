using Newtonsoft.Json.Linq;
using OverhaulLib.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlayerModelLib
{
    public sealed class TraitItemPermissionsSystem : ModSystem
    {
        private readonly Dictionary<string, TraitItemPermissions> _byTrait = new();

        public override double ExecuteOrder() => 0.31;

        public override void AssetsFinalize(ICoreAPI api)
        {
            Load(api);
        }

        public override void Dispose()
        {
            _byTrait.Clear();
        }

        private void Load(ICoreAPI api)
        {
            _byTrait.Clear();
            foreach (KeyValuePair<AssetLocation, JToken> entry in api.Assets.GetMany<JToken>(api.Logger, "config/traits"))
            {
                try
                {
                    if (entry.Value is JObject obj)
                        ProcessTraitToken(api, obj, entry.Key.ToString());
                    else if (entry.Value is JArray arr)
                    {
                        foreach (JToken t in arr)
                        {
                            if (t is JObject tj) ProcessTraitToken(api, tj, entry.Key.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(api, typeof(TraitItemPermissionsSystem), "Failed to parse traits " + entry.Key + ": " + ex.Message);
                }
            }
        }

        private static bool Has(JObject o, string key)
        {
            return o.Property(key, StringComparison.OrdinalIgnoreCase) != null;
        }

        private void ProcessTraitToken(ICoreAPI api, JObject traitObj, string source)
        {
            string? code = traitObj["code"]?.ToObject<string>();
            if (string.IsNullOrEmpty(code)) return;

            if (!Has(traitObj, "DisallowedItems") && !Has(traitObj, "DisallowedAttack") && !Has(traitObj, "DisallowedInteract") && !Has(traitObj, "AllowedFood")) return;

            TraitItemPermissionsConfig? cfg;
            try { cfg = traitObj.ToObject<TraitItemPermissionsConfig>(); }
            catch (Exception ex)
            {
                Log.Warn(api, typeof(TraitItemPermissionsSystem), "Failed to parse item permissions for trait '" + code + "' from '" + source + "': " + ex.Message);
                return;
            }
            if (cfg == null) return;
            ResolveAndMerge(api, code, cfg, source);
        }

        private void ResolveAndMerge(ICoreAPI api, string traitCode, TraitItemPermissionsConfig cfg, string source)
        {
            TraitItemPermissions? existing;
            if (!_byTrait.TryGetValue(traitCode, out existing))
            {
                existing = new TraitItemPermissions();
                _byTrait[traitCode] = existing;
            }

            if (cfg.DisallowedItems != null) AddIds(api, cfg.DisallowedItems, existing.DisallowedIds);
            if (cfg.DisallowedInteract != null) AddIds(api, cfg.DisallowedInteract, existing.DisallowedInteractIds);
            if (cfg.DisallowedAttack != null) AddIds(api, cfg.DisallowedAttack, existing.DisallowedAttackIds);
            if (cfg.AllowedFood == null) return;
            foreach (KeyValuePair<string, FoodOverrideJson> kv in cfg.AllowedFood)
            {
                string wildcard = kv.Key;
                FoodOverrideJson foodJson = kv.Value;
                foreach (CollectibleObject coll in api.World.Collectibles)
                {
                    if (!WildcardUtil.Match(wildcard, coll.Code != null ? coll.Code.ToString() : "")) continue;

                    if (existing.AllowedFoodOverrides.ContainsKey(coll.Id))
                        Log.Warn(api, typeof(TraitItemPermissionsSystem), "Food override for '" + coll.Code + "' in trait '" + traitCode + "' from '" + source + "' overwrites previous mod value.");

                    FoodNutritionProperties baseProps = coll.NutritionProps != null ? coll.NutritionProps.Clone() : new FoodNutritionProperties
                    {
                        FoodCategory = EnumFoodCategory.NoNutrition, Satiety = 0, Health = 0
                    };

                    if (foodJson != null)
                    {
                        if (foodJson.Satiety.HasValue) baseProps.Satiety = foodJson.Satiety.Value;
                        if (foodJson.Health.HasValue) baseProps.Health = foodJson.Health.Value;
                        if (!string.IsNullOrEmpty(foodJson.FoodCategory)
                            && Enum.TryParse<EnumFoodCategory>(foodJson.FoodCategory, true, out EnumFoodCategory cat))
                            baseProps.FoodCategory = cat;
                        else if (!string.IsNullOrEmpty(foodJson.FoodCategory))
                            Log.Warn(api, typeof(TraitItemPermissionsSystem), "Unknown food category '" + foodJson.FoodCategory + "' for '" + coll.Code + "' trait '" + traitCode + "'");
                    }

                    existing.AllowedFoodOverrides[coll.Id] = baseProps;
                }
            }
        }

        private static void AddIds(ICoreAPI api, string[] wildcards, HashSet<int> target)
        {
            foreach (string wildcard in wildcards)
            {
                foreach (CollectibleObject coll in api.World.Collectibles)
                {
                    if (WildcardUtil.Match(wildcard, coll.Code != null ? coll.Code.ToString() : ""))
                        target.Add(coll.Id);
                }
            }
        }

        public static HashSet<string> GatherPlayerTraitCodes(ICoreAPI api, EntityPlayer player)
        {
            HashSet<string> result = new HashSet<string>();
            Settings settings = PlayerModelModSystem.Settings;
            CharacterSystem charSys = api.ModLoader.GetModSystem<CharacterSystem>();
            if (charSys == null) return result;

            if (!settings.DisableModelClassesAndTraits)
            {
                string classCode = player.WatchedAttributes.GetString("characterClass");
                CharacterClass? cc;
                if (!string.IsNullOrEmpty(classCode) && charSys.characterClassesByCode.TryGetValue(classCode, out cc))
                {
                    foreach (string t in cc.Traits) result.Add(t);
                }
            }

            if (!settings.DisableCustomClassesAndTraits)
            {
                CustomModelsSystem modelSystem = api.ModLoader.GetModSystem<CustomModelsSystem>();
                PlayerSkinBehavior? skin = player.GetBehavior<PlayerSkinBehavior>();
                CustomModelData? model;
                if (modelSystem != null && skin != null && modelSystem.CustomModels.TryGetValue(skin.CurrentModelCode, out model))
                {
                    foreach (string t in model.ExtraTraits) result.Add(t);
                }

                string[]? extra = player.WatchedAttributes.GetStringArray("extraTraits");
                if (extra != null)
                {
                    foreach (string t in extra) result.Add(t);
                }
            }
            return result;
        }

        private IEnumerable<string> GetPlayerTraitCodes(EntityPlayer player)
        {
            return GatherPlayerTraitCodes(player.Api, player);
        }

        public bool IsInteractAllowed(EntityPlayer player, CollectibleObject coll)
        {
            if (PlayerModelModSystem.Settings.DisableClassItemRestrictions || coll == null) return true;
            foreach (string trait in GetPlayerTraitCodes(player))
            {
                TraitItemPermissions? perm;
                if (_byTrait.TryGetValue(trait, out perm) && (perm.DisallowedIds.Contains(coll.Id) || perm.DisallowedInteractIds.Contains(coll.Id)))
                    return false;
            }
            return true;
        }

        public bool TryGetFoodOverride(EntityPlayer player, CollectibleObject coll, out FoodNutritionProperties props)
        {
            props = null!;
            if (PlayerModelModSystem.Settings.DisableClassItemRestrictions || coll == null) return false;
            foreach (string trait in GetPlayerTraitCodes(player))
            {
                TraitItemPermissions? perm;
                FoodNutritionProperties? p;
                if (_byTrait.TryGetValue(trait, out perm) && perm.AllowedFoodOverrides.TryGetValue(coll.Id, out p))
                {
                    props = p.Clone();
                    return true;
                }
            }
            return false;
        }

        public bool IsAttackAllowed(EntityPlayer player, CollectibleObject coll)
        {
            if (PlayerModelModSystem.Settings.DisableClassItemRestrictions || coll == null) return true;
            foreach (string trait in GetPlayerTraitCodes(player))
            {
                TraitItemPermissions? perm;
                if (_byTrait.TryGetValue(trait, out perm) && (perm.DisallowedIds.Contains(coll.Id) || perm.DisallowedAttackIds.Contains(coll.Id)))
                    return false;
            }
            return true;
        }

    }

    public class FoodOverrideJson
    {
        public float? Satiety { get; set; }
        public string? FoodCategory { get; set; }
        public float? Health { get; set; }
    }

    public class TraitItemPermissionsConfig
    {
        public string[] DisallowedItems { get; set; } = new string[0];
        public string[] DisallowedInteract { get; set; } = new string[0];
        public string[] DisallowedAttack { get; set; } = new string[0];
        public Dictionary<string, FoodOverrideJson> AllowedFood { get; set; } = new Dictionary<string, FoodOverrideJson>();
    }

    public class TraitItemPermissions
    {
        public HashSet<int> DisallowedIds { get; set; } = new HashSet<int>();
        public HashSet<int> DisallowedInteractIds { get; set; } = new HashSet<int>();
        public HashSet<int> DisallowedAttackIds { get; set; } = new HashSet<int>();
        public Dictionary<int, FoodNutritionProperties> AllowedFoodOverrides { get; set; } = new Dictionary<int, FoodNutritionProperties>();
    }
}