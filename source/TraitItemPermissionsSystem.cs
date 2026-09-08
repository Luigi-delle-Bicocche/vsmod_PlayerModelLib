using Newtonsoft.Json.Linq;
using OverhaulLib.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlayerModelLib
{
    public sealed class TraitItemPermissionsSystem : ModSystem
    {
        private readonly Dictionary<string, TraitItemPermissions> _byTrait = new();
        private readonly HashSet<int> _wearRelevantIds = new();
        private readonly Dictionary<int, HashSet<string>> _exclusiveItemOwners = new();
        private readonly Dictionary<int, HashSet<string>> _exclusiveWearOwners = new();

        public override double ExecuteOrder() => 0.31;

        public override void AssetsFinalize(ICoreAPI api)
        {
            Load(api);
        }

        public override void Dispose()
        {
            _byTrait.Clear();
            _wearRelevantIds.Clear();
            _exclusiveItemOwners.Clear();
            _exclusiveWearOwners.Clear();
        }

        public static TraitItemPermissionsSystem? GetInstance(EntityPlayer player)
        {
            ICoreAPI api = player.Api;
            return api.ModLoader.GetModSystem<TraitItemPermissionsSystem>();
        }

        public static void SendItemDisallowed(EntityPlayer player, CollectibleObject coll)
        {
            if (coll.NutritionProps != null) return; // dont send the error msg on purpose like vanilla inedible items do. if users are confused (can't read tooltip) then this could be reinstated
            SendDisallowed(player, "playermodellib:itemdisallowed", "itemdisallowed");
        }

        public static void SendWearDisallowed(EntityPlayer player)
        {
            SendDisallowed(player, "playermodellib:weardisallowed", "weardisallowed");
        }

        private static void SendDisallowed(EntityPlayer player, string langKey, string errorCode)
        {
            string message = Lang.Get(langKey);
            if (player.Player is IServerPlayer sp) sp.SendIngameError(errorCode, message);
            else if (player.Api is ICoreClientAPI capi && player.PlayerUID == capi.World.Player?.PlayerUID)
                capi.TriggerIngameError(null, errorCode, message);
        }

        private void Load(ICoreAPI api)
        {
            _byTrait.Clear();
            _wearRelevantIds.Clear();
            _exclusiveItemOwners.Clear();
            _exclusiveWearOwners.Clear();
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
            foreach (KeyValuePair<string, TraitItemPermissions> kv in _byTrait)
            {
                TraitItemPermissions perm = kv.Value;
                _wearRelevantIds.UnionWith(perm.DisallowedWearableIds);
                _wearRelevantIds.UnionWith(perm.AllowedWearableIds);
                _wearRelevantIds.UnionWith(perm.ExclusiveWearableIds);

                RegisterExclusiveOwners(_exclusiveWearOwners, perm.ExclusiveWearableIds, kv.Key);
                RegisterExclusiveOwners(_exclusiveItemOwners, perm.ExclusiveItemIds, kv.Key);
            }
        }

        private static void RegisterExclusiveOwners(Dictionary<int, HashSet<string>> ownersById, HashSet<int> exclusiveIds, string traitCode)
        {
            foreach (int id in exclusiveIds)
            {
                if (!ownersById.TryGetValue(id, out HashSet<string>? owners))
                {
                    owners = new HashSet<string>();
                    ownersById[id] = owners;
                }
                owners.Add(traitCode);
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

            if (!Has(traitObj, "DisallowedItems") && !Has(traitObj, "DisallowedAttack") && !Has(traitObj, "DisallowedInteract") && !Has(traitObj, "AllowedFood")
                && !Has(traitObj, "ExclusiveItems") && !Has(traitObj, "DisallowedWearables") && !Has(traitObj, "AllowedWearables") && !Has(traitObj, "ExclusiveWearables")) return;

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
            if (cfg.ExclusiveItems != null) AddIds(api, cfg.ExclusiveItems, existing.ExclusiveItemIds);
            if (cfg.DisallowedWearables != null) AddWearIds(api, cfg.DisallowedWearables, existing.DisallowedWearableIds, traitCode, source);
            if (cfg.AllowedWearables != null) AddWearIds(api, cfg.AllowedWearables, existing.AllowedWearableIds, traitCode, source);
            if (cfg.ExclusiveWearables != null) AddWearIds(api, cfg.ExclusiveWearables, existing.ExclusiveWearableIds, traitCode, source);
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

                    FoodNutritionProperties baseProps = GetBaseNutritionProps(api, coll) ?? new FoodNutritionProperties
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

        public static FoodNutritionProperties? GetBaseNutritionProps(ICoreAPI api, CollectibleObject coll)
        {
            if (coll.NutritionProps != null) return coll.NutritionProps.Clone();
            try
            {
                WaterTightContainableProps? wtp = coll.Attributes?["waterTightContainerProps"]?.AsObject<WaterTightContainableProps>();
                if (wtp?.NutritionPropsPerLitre != null) return wtp.NutritionPropsPerLitre.Clone();
            }
            catch (Exception ex)
            {
                Log.Warn(api, typeof(TraitItemPermissionsSystem), "Failed reading per-litre nutrition for '" + coll.Code + "': " + ex.Message);
            }
            return null;
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
        // TODO check how CO does things
        private static bool IsKnownWearType(string entry)
        {
            return entry.Equals("type:all", StringComparison.OrdinalIgnoreCase) || entry.Equals("type:armor", StringComparison.OrdinalIgnoreCase) || entry.Equals("type:clothing", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesWearType(EnumCharacterDressType dress, string entry)
        {
            if (dress == EnumCharacterDressType.Unknown) return false;
            if (entry.Equals("type:all", StringComparison.OrdinalIgnoreCase)) return true;
            bool armor = dress == EnumCharacterDressType.ArmorHead || dress == EnumCharacterDressType.ArmorBody || dress == EnumCharacterDressType.ArmorLegs;
            if (entry.Equals("type:armor", StringComparison.OrdinalIgnoreCase)) return armor;
            if (entry.Equals("type:clothing", StringComparison.OrdinalIgnoreCase)) return !armor;
            return false;
        }

        private static EnumCharacterDressType GetWearDressType(ICoreAPI api, CollectibleObject coll)
        {
            string? categoryName = coll.Attributes?["clothescategory"]?.AsString();
            if (string.IsNullOrEmpty(categoryName))
            {
                try { categoryName = coll.Attributes?["attachableToEntity"]?["categoryCode"]?.AsString(); }
                catch (Exception ex) { categoryName = null; Log.Warn(api, typeof(TraitItemPermissionsSystem), "Failed reading categoryCode for '" + coll.Code + "': " + ex.Message); }
            }
            if (!string.IsNullOrEmpty(categoryName) && Enum.TryParse<EnumCharacterDressType>(categoryName, true, out EnumCharacterDressType parsed))
                return parsed;
            try
            {
                IWearableStatsSupplier? supplier = coll.GetCollectibleInterface<IWearableStatsSupplier>();
                if (supplier != null) return supplier.GetDressType(new DummySlot(new ItemStack(coll)));
            }
            catch (Exception ex)
            {
                Log.Warn(api, typeof(TraitItemPermissionsSystem), "GetDressType failed for '" + coll.Code + "': " + ex.Message);
            }
            return EnumCharacterDressType.Unknown;
        }

        private static void AddWearIds(ICoreAPI api, string[] entries, HashSet<int> target, string traitCode, string source)
        {
            foreach (string entry in entries)
            {
                if (!string.IsNullOrEmpty(entry) && entry.StartsWith("type:", StringComparison.OrdinalIgnoreCase) && !IsKnownWearType(entry))
                    Log.Warn(api, typeof(TraitItemPermissionsSystem), "Unknown wearable entry '" + entry + "' in trait '" + traitCode + "' from '" + source + "' (use type:all, type:armor, type:clothing or a wildcard).");
            }
            foreach (CollectibleObject coll in api.World.Collectibles)
            {
                string code = coll.Code != null ? coll.Code.ToString() : "";
                EnumCharacterDressType dress = EnumCharacterDressType.Unknown;
                bool dressResolved = false;
                foreach (string entry in entries)
                {
                    if (string.IsNullOrEmpty(entry)) continue;
                    if (entry.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!dressResolved) { dress = GetWearDressType(api, coll); dressResolved = true; }
                        if (MatchesWearType(dress, entry)) { target.Add(coll.Id); break; }
                    }
                    else if (WildcardUtil.Match(entry, code)) { target.Add(coll.Id); break; }
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
            if (PlayerModelModSystem.Settings.DisableClassItemRestrictions) return true;
            return IsItemUseAllowed(player, coll, perm => perm.DisallowedIds.Contains(coll.Id) || perm.DisallowedInteractIds.Contains(coll.Id));
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
            if (PlayerModelModSystem.Settings.DisableClassItemRestrictions) return true;
            return IsItemUseAllowed(player, coll, perm => perm.DisallowedIds.Contains(coll.Id) || perm.DisallowedAttackIds.Contains(coll.Id));
        }

        private bool IsItemUseAllowed(EntityPlayer player, CollectibleObject coll, System.Func<TraitItemPermissions, bool> isDeniedByTrait)
        {
            HashSet<string>? owners;
            bool isExclusive = _exclusiveItemOwners.TryGetValue(coll.Id, out owners) && owners.Count > 0;
            bool hasOwnerTrait = false;

            foreach (string trait in GetPlayerTraitCodes(player))
            {
                TraitItemPermissions? perm;
                if (_byTrait.TryGetValue(trait, out perm) && isDeniedByTrait(perm))
                    return false;
                if (isExclusive && owners!.Contains(trait)) hasOwnerTrait = true;
            }

            if (isExclusive && !hasOwnerTrait) return false;

            return true;
        }

        public bool IsWearAllowed(EntityPlayer player, CollectibleObject coll)
        {
            if (PlayerModelModSystem.Settings.DisableClassItemRestrictions) return true;

            HashSet<string>? owners;
            bool isExclusive = _exclusiveWearOwners.TryGetValue(coll.Id, out owners) && owners.Count > 0;
            bool hasOwnerTrait = false;
            bool denied = false;

            foreach (string trait in GetPlayerTraitCodes(player))
            {
                TraitItemPermissions? perm;
                if (_byTrait.TryGetValue(trait, out perm))
                {
                    if (perm.AllowedWearableIds.Contains(coll.Id)) return true;
                    if (perm.DisallowedWearableIds.Contains(coll.Id)) denied = true;
                }
                if (isExclusive && owners!.Contains(trait)) hasOwnerTrait = true;
            }

            if (isExclusive && !hasOwnerTrait) return false;

            return !denied;
        }

        public bool IsWearRelevant(int collectibleId)
        {
            return _wearRelevantIds.Contains(collectibleId);
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
        public string[] ExclusiveItems { get; set; } = new string[0];
        public string[] DisallowedWearables { get; set; } = new string[0];
        public string[] AllowedWearables { get; set; } = new string[0];
        public string[] ExclusiveWearables { get; set; } = new string[0];
        public Dictionary<string, FoodOverrideJson> AllowedFood { get; set; } = new Dictionary<string, FoodOverrideJson>();
    }

    public class TraitItemPermissions
    {
        public HashSet<int> DisallowedIds { get; set; } = new HashSet<int>();
        public HashSet<int> DisallowedInteractIds { get; set; } = new HashSet<int>();
        public HashSet<int> DisallowedAttackIds { get; set; } = new HashSet<int>();
        public HashSet<int> ExclusiveItemIds { get; set; } = new HashSet<int>();
        public HashSet<int> DisallowedWearableIds { get; set; } = new HashSet<int>();
        public HashSet<int> AllowedWearableIds { get; set; } = new HashSet<int>();
        public HashSet<int> ExclusiveWearableIds { get; set; } = new HashSet<int>();
        public Dictionary<int, FoodNutritionProperties> AllowedFoodOverrides { get; set; } = new Dictionary<int, FoodNutritionProperties>();
    }
}