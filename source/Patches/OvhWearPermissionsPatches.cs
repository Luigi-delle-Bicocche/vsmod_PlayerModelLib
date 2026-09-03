using HarmonyLib;
using OverhaulLib.Utils;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace PlayerModelLib
{
    public static class OvhWearPermissionsPatches
    {
        private static Harmony? _harmony;
        private static bool _active;

        public static bool IsActive => _active;

        private static bool IsModLoaded(ICoreAPI api, string modId)
        {
            try { return api.ModLoader.GetMod(modId) != null; }
            catch { return false; }
        }

        public static void Patch(string harmonyId, ICoreAPI api)
        {
            if (!IsModLoaded(api, "combatoverhaulfork")) return;
            if (_harmony != null) return;
            Harmony harmony = new Harmony(harmonyId);
            int patched = 0;
            patched += PatchOvhMethod(harmony, api, "CombatOverhaul.Armor.ArmorSlot", "CanHold", new Type[] { typeof(ItemSlot) }, nameof(OvhSlotCanWearPrefix));
            patched += PatchOvhMethod(harmony, api, "CombatOverhaul.Armor.GearSlot", "CanHold", new Type[] { typeof(ItemSlot) }, nameof(OvhSlotCanWearPrefix));
            patched += PatchOvhMethod(harmony, api, "CombatOverhaul.Armor.GearSlot", "CanTakeFrom", new Type[] { typeof(ItemSlot), typeof(EnumMergePriority) }, nameof(OvhSlotCanWearPrefix));
            patched += PatchOvhMethod(harmony, api, "CombatOverhaul.Armor.ItemWearableArmor", "OnHeldInteractStart", new Type[] { typeof(ItemSlot), typeof(EntityAgent), typeof(BlockSelection), typeof(EntitySelection),
                        typeof(bool), typeof(EnumHandHandling).MakeByRefType() }, nameof(OvhItemOnHeldInteractStartPrefix));
            patched += PatchOvhMethod(harmony, api, "CombatOverhaul.Armor.WearableArmorBehavior", "OnHeldInteractStart", new Type[] { typeof(ItemSlot), typeof(EntityAgent), typeof(BlockSelection), typeof(EntitySelection),
                        typeof(bool), typeof(EnumHandHandling).MakeByRefType(), typeof(EnumHandling).MakeByRefType() }, nameof(OvhBehaviorOnHeldInteractStartPrefix));

            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.MeleeWeaponClient", "Attack", nameof(CoAttackPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.MeleeWeaponClient", "Throw", nameof(CoAttackPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.StanceBasedMeleeWeaponClient", "LeftClickAttack", nameof(CoAttackPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.StanceBasedMeleeWeaponClient", "RightClickAttack", nameof(CoAttackPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.BowClient", "Load", nameof(CoInteractPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.BowClient", "Aim", nameof(CoInteractPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.BowClient", "Shoot", nameof(CoInteractPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.BowClient", "Denock", nameof(CoInteractPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.SlingClient", "Load", nameof(CoInteractPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.SlingClient", "Swing", nameof(CoInteractPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.SlingClient", "Release", nameof(CoInteractPrefix), HasItemSlotAndPlayer);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Animations.FirstPersonAnimationsBehavior", "Play", nameof(CoAnimPlayPrefix), HasMainHandFlag);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Animations.ThirdPersonAnimationsBehavior", "Play", nameof(CoAnimPlayPrefix), HasMainHandFlag);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.RangedSystems.RangeWeaponServer", "Shoot", nameof(CoShootPrefix), HasServerPlayerAndSlot);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.BowServer", "Shoot", nameof(CoShootPrefix), HasServerPlayerAndSlot);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.SlingServer", "Shoot", nameof(CoShootPrefix), HasServerPlayerAndSlot);
            patched += PatchOvhMethodsByName(harmony, api, "CombatOverhaul.Implementations.MeleeWeaponServer", "Shoot", nameof(CoShootPrefix), HasServerPlayerAndSlot);
            patched += PatchOvhMethod(harmony, api, "CombatOverhaul.Inputs.ActionsManagerPlayerBehavior", "OnGameTick", new Type[] { typeof(float) }, nameof(OvhActionsTickGuardPrefix));
            if (patched == 0)
            {
                harmony.UnpatchAll(harmonyId);
                return;
            }
            _harmony = harmony;
            _active = true;
        }

        public static void Unpatch(string harmonyId)
        {
            Harmony? harmony = _harmony;
            if (harmony == null) return;
            harmony.UnpatchAll(harmonyId);
            _harmony = null;
            _active = false;
        }

        private static int PatchOvhMethod(Harmony harmony, ICoreAPI api, string typeName, string methodName, Type[] paramTypes, string prefixName)
        {
            try
            {
                Type? type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    Log.Warn(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: type not found: " + typeName);
                    return 0;
                }
                MethodInfo? target = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, paramTypes, null);
                if (target == null)
                {
                    Log.Warn(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: method not found: " + typeName + "." + methodName);
                    return 0;
                }
                MethodInfo? prefix = typeof(OvhWearPermissionsPatches).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic);
                if (prefix == null)
                {
                    Log.Warn(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: prefix not found: " + prefixName);
                    return 0;
                }
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                return 1;
            }
            catch (Exception ex)
            {
                Log.Error(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: failed to patch " + typeName + "." + methodName + ": " + ex.Message);
                return 0;
            }
        }

        private static int PatchOvhMethodsByName(Harmony harmony, ICoreAPI api, string typeName, string methodName, string prefixName, System.Func<MethodInfo, bool>? filter = null)
        {
            try
            {
                Type? type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    Log.Warn(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: type not found: " + typeName);
                    return 0;
                }
                MethodInfo? prefix = typeof(OvhWearPermissionsPatches).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic);
                if (prefix == null)
                {
                    Log.Warn(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: prefix not found: " + prefixName);
                    return 0;
                }
                int patched = 0;
                int skipped = 0;
                foreach (MethodInfo target in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(m => m.Name == methodName))
                {
                    if (filter != null && !filter(target)) { skipped++; continue; }
                    try
                    {
                        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                        patched++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: failed to patch " + typeName + "." + methodName + ": " + ex.Message);
                    }
                }
                if (patched == 0)
                {
                    Log.Warn(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: method not found: " + typeName + "." + methodName);
                }
                else if (skipped > 0)
                {
                    Log.Debug(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: patched " + patched + " overload(s), skipped " + skipped + " for " + typeName + "." + methodName);
                }
                return patched;
            }
            catch (Exception ex)
            {
                Log.Error(api, typeof(OvhWearPermissionsPatches), "OVHlib compat: failed to patch " + typeName + "." + methodName + ": " + ex.Message);
                return 0;
            }
        }

        private static bool HasItemSlotAndPlayer(MethodInfo m)
        {
            ParameterInfo[] pars = m.GetParameters();
            return pars.Any(p => p.ParameterType == typeof(ItemSlot)) && pars.Any(p => p.ParameterType == typeof(EntityPlayer));
        }

        private static bool HasServerPlayerAndSlot(MethodInfo m)
        {
            ParameterInfo[] pars = m.GetParameters();
            return pars.Any(p => p.ParameterType == typeof(IServerPlayer)) && pars.Any(p => p.ParameterType == typeof(ItemSlot));
        }

        private static bool HasMainHandFlag(MethodInfo m)
        {
            return m.GetParameters().Any(p => p.ParameterType == typeof(bool) && p.Name == "mainHand");
        }

        private static bool FailOpenOnce(ref bool flag, string message, Exception ex)
        {
            if (!flag)
            {
                flag = true;
                Log.Error(null, typeof(OvhWearPermissionsPatches), "OVHlib compat: " + message + " failed open: " + ex);
            }
            return true;
        }

        private static bool CheckAttackAllowed(EntityPlayer player, CollectibleObject coll, bool feedback = true)
        {
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null || inst.IsAttackAllowed(player, coll)) return true;
            if (feedback) TraitItemPermissionsSystem.SendItemDisallowed(player, coll);
            return false;
        }

        private static bool CheckInteractAllowed(EntityPlayer player, CollectibleObject coll, bool feedback = true)
        {
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null || inst.TryGetFoodOverride(player, coll, out _) || inst.IsInteractAllowed(player, coll)) return true;
            if (feedback) TraitItemPermissionsSystem.SendItemDisallowed(player, coll);
            return false;
        }

        public static bool IsCombatOverhaulArmor(CollectibleObject? coll)
        {
            if (coll == null) return false;
            string? fullName = coll.GetType().FullName;
            if (!string.IsNullOrEmpty(fullName) && fullName.Contains("ItemWearableArmor", StringComparison.Ordinal)) return true;
            CollectibleBehavior[]? behaviors = coll.CollectibleBehaviors;
            if (behaviors == null) return false;
            foreach (CollectibleBehavior behavior in behaviors)
            {
                string name = behavior.GetType().FullName ?? behavior.GetType().Name;
                if (name.Contains("ArmorBehavior", StringComparison.Ordinal) || name.Contains("WearableArmorBehavior", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool IsLocalPlayer(EntityPlayer player)
        {
            try
            {
                if (player.Api is not ICoreClientAPI capi) return false;
                return capi.World?.Player?.Entity?.EntityId == player.EntityId;
            }
            catch (Exception ex)
            {
                if (!_reportedLocalPlayerError)
                {
                    _reportedLocalPlayerError = true;
                    Log.Debug(player.Api, typeof(OvhWearPermissionsPatches), "OVHlib compat: IsLocalPlayer check failed: " + ex.Message);
                }
                return false;
            }
        }

        private static bool _reportedLocalPlayerError;

        private static bool _reportedCoAttackError;
        private static bool _reportedCoInteractError;
        private static bool _reportedCoAnimError;
        private static bool _reportedCoShootError;
        private static bool _reportedTickGuardError;
        private static bool _reportedTickGuardSkip;

        private static bool OvhActionsTickGuardPrefix(EntityBehavior __instance)
        {
            try
            {
                EntityPlayer? player = __instance?.entity as EntityPlayer;
                if (player == null) return true;
                if (player.ActiveHandItemSlot == null && player.RightHandItemSlot == null && player.LeftHandItemSlot == null)
                {
                    if (!_reportedTickGuardSkip)
                    {
                        _reportedTickGuardSkip = true;
                        Log.Warn(player.Api, typeof(OvhWearPermissionsPatches), "OVHlib compat: skipped ActionsManagerPlayerBehavior tick on slot-torn entity, disconnect race suspected.");
                    }
                    else
                    {
                        Log.Debug(player.Api, typeof(OvhWearPermissionsPatches), "OVHlib compat: skipped ActionsManagerPlayerBehavior tick on slot-torn entity.");
                    }
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (!_reportedTickGuardError)
                {
                    _reportedTickGuardError = true;
                    Log.Error(null, typeof(OvhWearPermissionsPatches), "OVHlib compat: tick guard failed open: " + ex);
                }
                return true;
            }
        }

        private static bool CoAttackPrefix(object[] __args, ref bool __result)
        {
            try
            {
                ItemSlot? slot = __args.OfType<ItemSlot>().FirstOrDefault();
                EntityPlayer? player = __args.OfType<EntityPlayer>().FirstOrDefault();
                CollectibleObject? coll = slot?.Itemstack?.Collectible;
                if (slot == null || coll == null || player == null || !player.Alive) return true;
                if (CheckAttackAllowed(player, coll)) return true;
                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                return FailOpenOnce(ref _reportedCoAttackError, "CoAttackPrefix", ex);
            }
        }

        private static bool CoInteractPrefix(object[] __args, ref bool __result)
        {
            try
            {
                ItemSlot? slot = __args.OfType<ItemSlot>().FirstOrDefault();
                EntityPlayer? player = __args.OfType<EntityPlayer>().FirstOrDefault();
                CollectibleObject? coll = slot?.Itemstack?.Collectible;
                if (slot == null || coll == null || player == null || !player.Alive) return true;
                if (CheckInteractAllowed(player, coll)) return true;
                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                return FailOpenOnce(ref _reportedCoInteractError, "CoInteractPrefix", ex);
            }
        }

        private static bool CoAnimPlayPrefix(object __instance, bool mainHand)
        {
            try
            {
                if (__instance is not EntityBehavior behavior || behavior.entity is not EntityPlayer player || !player.Alive || !IsLocalPlayer(player)) return true;
                ItemSlot? slot = mainHand ? player.RightHandItemSlot : player.LeftHandItemSlot;
                CollectibleObject? coll = slot?.Itemstack?.Collectible;
                if (coll == null) return true;
                if (!CheckAttackAllowed(player, coll, false)) return false;
                if (!CheckInteractAllowed(player, coll, false)) return false;
                return true;
            }
            catch (Exception ex)
            {
                return FailOpenOnce(ref _reportedCoAnimError, "CoAnimPlayPrefix", ex);
            }
        }

        private static bool CoShootPrefix(object[] __args, ref bool __result)
        {
            try
            {
                IServerPlayer? serverPlayer = null;
                ItemSlot? slot = null;
                foreach (object? arg in __args)
                {
                    serverPlayer ??= arg as IServerPlayer;
                    slot ??= arg as ItemSlot;
                }
                EntityPlayer? player = serverPlayer?.Entity as EntityPlayer;
                CollectibleObject? coll = slot?.Itemstack?.Collectible;
                if (serverPlayer == null || slot == null || coll == null || player == null || !player.Alive) return true;
                if (CheckAttackAllowed(player, coll) && CheckInteractAllowed(player, coll)) return true;
                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                return FailOpenOnce(ref _reportedCoShootError, "CoShootPrefix", ex);
            }
        }

        private static bool OvhSlotCanWearPrefix(ItemSlot __instance, ItemSlot sourceSlot, ref bool __result)
        {
            CollectibleObject? coll = sourceSlot?.Itemstack?.Collectible;
            ICoreAPI api = __instance.Inventory.Api;
            if (!WearPermissionsPatches.IsWearAllowed(api, coll, () => WearPermissionsPatches.ResolveCharacterSlotOwner(__instance), false)) { __result = false; return false; }
            return true;
        }

        private static bool OvhItemOnHeldInteractStartPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, ref EnumHandHandling handHandling)
        {
            CollectibleObject? coll = slot?.Itemstack?.Collectible;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (player == null || WearPermissionsPatches.IsNonSelfDressInteract(byEntity, blockSel, entitySel)) return true;
            if (!WearPermissionsPatches.IsWearAllowed(player.Api, coll, () => player, true)) { handHandling = EnumHandHandling.PreventDefault; return false; }
            return true;
        }

        private static bool OvhBehaviorOnHeldInteractStartPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            CollectibleObject? coll = slot?.Itemstack?.Collectible;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (player == null || WearPermissionsPatches.IsNonSelfDressInteract(byEntity, blockSel, entitySel)) return true;
            if (!WearPermissionsPatches.IsWearAllowed(player.Api, coll, () => player, true)) { handHandling = EnumHandHandling.PreventDefault; handling = EnumHandling.PreventSubsequent; return false; }
            return true;
        }
    }
}