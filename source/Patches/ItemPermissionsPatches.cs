using HarmonyLib;
using OverhaulLib.Utils;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace PlayerModelLib
{
    public static class ItemPermissionsPatches
    {
        private static Harmony? _harmony;

        public static void Patch(string harmonyId, ICoreAPI api)
        {
            if (_harmony != null) return;
            Harmony harmony = new Harmony(harmonyId);
            try
            {
                Type t = typeof(CollectibleObject);

                MethodInfo? m = t.GetMethod(nameof(CollectibleObject.OnHeldInteractStart), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.OnHeldInteractStart");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(OnHeldInteractStartPrefix))));

                m = t.GetMethod(nameof(CollectibleObject.OnHeldUseStart), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.OnHeldUseStart");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(OnHeldUseStartPrefix))));

                m = t.GetMethod(nameof(CollectibleObject.OnHeldUseStep), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.OnHeldUseStep");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(OnHeldUseStepPrefix))));

                m = t.GetMethod(nameof(CollectibleObject.OnHeldUseStop), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.OnHeldUseStop");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(OnHeldUseStopPrefix))));

                m = t.GetMethod(nameof(CollectibleObject.GetHeldTpHitAnimation), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.GetHeldTpHitAnimation");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(GetHeldTpHitAnimationPrefix))));

                m = t.GetMethod(nameof(CollectibleObject.GetHeldTpUseAnimation), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.GetHeldTpUseAnimation");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(GetHeldTpUseAnimationPrefix))));

                m = t.GetMethod(nameof(CollectibleObject.OnHeldAttackStart), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.OnHeldAttackStart");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(OnHeldAttackStartPrefix))));

                m = t.GetMethod(nameof(CollectibleObject.OnHeldIdle), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.OnHeldIdle");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(OnHeldIdlePrefix))));

                m = t.GetMethod(nameof(CollectibleObject.GetNutritionProperties), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.GetNutritionProperties");
                    return;
                }
                harmony.Patch(m, postfix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(GetNutritionPropertiesPostfix))));

                m = t.GetMethod(nameof(CollectibleObject.GetHeldItemInfo), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.GetHeldItemInfo");
                    return;
                }
                harmony.Patch(m, postfix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(GetHeldItemInfoPostfix))));

                m = typeof(BlockLiquidContainerBase).GetMethod(nameof(BlockLiquidContainerBase.GetNutritionPropertiesPerLitre), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find BlockLiquidContainerBase.GetNutritionPropertiesPerLitre");
                }
                else
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(GetNutritionPropertiesPerLitrePostfix))));
                }
                MethodInfo? shieldMethod = typeof(ModSystemWearableStats).GetMethod("applyShieldProtection", BindingFlags.Instance | BindingFlags.NonPublic);
                if (shieldMethod == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find ModSystemWearableStats.applyShieldProtection");
                }
                else
                {
                    harmony.Patch(shieldMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(ShieldDisallowPrefix))));
                }
                _harmony = harmony;
            }
            catch (Exception ex)
            {
                Log.Error(api, typeof(ItemPermissionsPatches), "Failed to apply patches (" + harmonyId + "): " + ex.Message);
            }
        }

        public static void Unpatch(string harmonyId)
        {
            Harmony? h = _harmony;
            if (h == null) return;
            h.UnpatchAll(harmonyId);
            _harmony = null;
        }

        private static readonly HashSet<MethodInfo> _patchedOverrideMethods = new();
        [ThreadStatic]
        private static bool _inGetHeldItemInfo;
        private static readonly (string methodName, Type[] paramTypes, string patchName, bool isPostfix)[] _overridePatchMap = new (string, Type[], string, bool)[]
        {
            (nameof(CollectibleObject.OnHeldUseStart), new Type[] { typeof(ItemSlot), typeof(EntityAgent), typeof(BlockSelection), typeof(EntitySelection), typeof(EnumHandInteract), typeof(bool), typeof(EnumHandHandling).MakeByRefType() }, nameof(OnHeldUseStartAnyPrefix), false),
            (nameof(CollectibleObject.OnHeldInteractStart), new Type[] { typeof(ItemSlot), typeof(EntityAgent), typeof(BlockSelection), typeof(EntitySelection), typeof(bool), typeof(EnumHandHandling).MakeByRefType() }, nameof(OnHeldInteractStartAnyPrefix), false),
            (nameof(CollectibleObject.OnHeldInteractStep), new Type[] { typeof(float), typeof(ItemSlot), typeof(EntityAgent), typeof(BlockSelection), typeof(EntitySelection) }, nameof(OnHeldInteractStepAnyPrefix), false),
            (nameof(CollectibleObject.OnHeldAttackStart), new Type[] { typeof(ItemSlot), typeof(EntityAgent), typeof(BlockSelection), typeof(EntitySelection), typeof(EnumHandHandling).MakeByRefType() }, nameof(OnHeldAttackStartAnyPrefix), false),
            (nameof(CollectibleObject.OnHeldIdle), new Type[] { typeof(ItemSlot), typeof(EntityAgent) }, nameof(OnHeldIdleAnyPrefix), false),
            (nameof(CollectibleObject.GetHeldTpHitAnimation), new Type[] { typeof(ItemSlot), typeof(Entity) }, nameof(GetHeldTpHitAnimationAnyPrefix), false),
            (nameof(CollectibleObject.GetHeldTpUseAnimation), new Type[] { typeof(ItemSlot), typeof(Entity) }, nameof(GetHeldTpUseAnimationAnyPrefix), false),
            (nameof(CollectibleObject.GetHeldItemInfo), new Type[] { typeof(ItemSlot), typeof(StringBuilder), typeof(IWorldAccessor), typeof(bool) }, nameof(GetHeldItemInfoPostfix), true),
        };

        public static void PatchCollectibleOverrides(ICoreAPI api)
        {
            Harmony? harmony = _harmony;
            if (harmony == null)
            {
                Log.Warn(api, typeof(ItemPermissionsPatches), "Cannot patch collectible overrides before base patches are applied");
                return;
            }
            int patched = 0;
            int types = 0;
            try
            {
                HashSet<Type> seen = new HashSet<Type>();
                foreach (CollectibleObject coll in api.World.Collectibles)
                {
                    Type t = coll.GetType();
                    if (!seen.Add(t) || !t.IsSubclassOf(typeof(CollectibleObject))) continue;
                    bool typePatched = false;
                    foreach ((string methodName, Type[] paramTypes, string patchName, bool isPostfix) in _overridePatchMap)
                    {
                        MethodInfo? m;
                        try { m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, paramTypes, null); }
                        catch (Exception ex)
                        {
                            Log.Warn(api, typeof(ItemPermissionsPatches), "Override lookup failed for " + t.FullName + "." + methodName + ": " + ex.Message);
                            continue;
                        }
                        if (m == null || m.IsAbstract || !_patchedOverrideMethods.Add(m)) continue;
                        try
                        {
                            HarmonyMethod patch = new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), patchName));
                            if (isPostfix) harmony.Patch(m, postfix: patch);
                            else harmony.Patch(m, prefix: patch);
                            patched++;
                            typePatched = true;
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(api, typeof(ItemPermissionsPatches), "Failed to patch override " + t.FullName + "." + methodName + ": " + ex.Message);
                        }
                    }
                    if (typePatched) types++;
                }
            }
            catch (Exception ex)
            {
                Log.Error(api, typeof(ItemPermissionsPatches), "Failed to patch collectible overrides: " + ex.Message);
                return;
            }
            Log.Debug(api, typeof(ItemPermissionsPatches), "Patched " + patched + " collectible override methods on " + types + " types");
        }
        
        private static bool OnHeldUseStartAnyPrefix(object[] __args)
        {
            ItemSlot slot = (ItemSlot)__args[0];
            EntityAgent byEntity = (EntityAgent)__args[1];
            BlockSelection blockSel = (BlockSelection)__args[2];
            EntitySelection entitySel = (EntitySelection)__args[3];
            EnumHandInteract useType = (EnumHandInteract)__args[4];
            bool firstEvent = (bool)__args[5];
            EnumHandHandling handling = (EnumHandHandling)__args[6];
            bool result = OnHeldUseStartPrefix(slot, byEntity, blockSel, entitySel, useType, firstEvent, ref handling);
            __args[6] = handling;
            return result;
        }

        private static bool OnHeldInteractStartAnyPrefix(object[] __args)
        {
            ItemSlot slot = (ItemSlot)__args[0];
            EntityAgent byEntity = (EntityAgent)__args[1];
            BlockSelection blockSel = (BlockSelection)__args[2];
            EntitySelection entitySel = (EntitySelection)__args[3];
            bool firstEvent = (bool)__args[4];
            EnumHandHandling handling = (EnumHandHandling)__args[5];
            bool result = OnHeldInteractStartPrefix(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            __args[5] = handling;
            return result;
        }

        private static bool OnHeldInteractStepAnyPrefix(object[] __args, ref bool __result)
        {
            ItemSlot slot = (ItemSlot)__args[1];
            EntityAgent byEntity = (EntityAgent)__args[2];
            return OnHeldInteractStepPrefix(slot, byEntity, ref __result);
        }

        private static bool OnHeldAttackStartAnyPrefix(object[] __args)
        {
            ItemSlot slot = (ItemSlot)__args[0];
            EntityAgent byEntity = (EntityAgent)__args[1];
            EnumHandHandling handling = (EnumHandHandling)__args[4];
            bool result = OnHeldAttackStartPrefix(slot, byEntity, ref handling);
            __args[4] = handling;
            return result;
        }

        private static bool OnHeldIdleAnyPrefix(object[] __args)
        {
            ItemSlot slot = (ItemSlot)__args[0];
            EntityAgent byEntity = (EntityAgent)__args[1];
            return OnHeldIdlePrefix(slot, byEntity);
        }

        private static bool GetHeldTpHitAnimationAnyPrefix(object[] __args, ref string? __result)
        {
            ItemSlot slot = (ItemSlot)__args[0];
            Entity forEntity = (Entity)__args[1];
            return GetHeldTpHitAnimationPrefix(slot, forEntity, ref __result);
        }

        private static bool GetHeldTpUseAnimationAnyPrefix(object[] __args, ref string? __result)
        {
            ItemSlot slot = (ItemSlot)__args[0];
            Entity forEntity = (Entity)__args[1];
            return GetHeldTpUseAnimationPrefix(slot, forEntity, ref __result);
        }

        private static bool TryBlockInteract(EntityPlayer player, CollectibleObject coll, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null) return true;
            HashSet<string> traitCodes = inst.GetPlayerTraitCodes(player);
            if (!inst.IsItemRelevant(coll.Id) || inst.TryGetFoodOverride(player, coll, traitCodes, out _) || inst.IsInteractAllowed(player, coll, traitCodes)) return true;
            if (firstEvent && blockSel != null && entitySel == null && byEntity.Controls.ShiftKey && TryGroundStore(coll, slot, byEntity, blockSel, entitySel, firstEvent, ref handling)) return false;
            handling = EnumHandHandling.PreventDefault;
            TraitItemPermissionsSystem.SendItemDisallowed(player, coll);
            return false;
        }

        private static bool TryGroundStore(CollectibleObject coll, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (coll.CollectibleBehaviors == null) return false;
            foreach (CollectibleBehavior behavior in coll.CollectibleBehaviors)
            {
                if (behavior is CollectibleBehaviorGroundStorable groundStorable)
                {
                    EnumHandHandling bhHandHandling = EnumHandHandling.NotHandled;
                    EnumHandling bhHandling = EnumHandling.PassThrough;
                    groundStorable.OnHeldInteractStart(slot, byEntity, blockSel, entitySel!, firstEvent, ref bhHandHandling, ref bhHandling);
                    if (bhHandHandling == EnumHandHandling.NotHandled && bhHandling == EnumHandling.PassThrough) return false;
                    handling = bhHandHandling;
                    return true;
                }
            }
            return false;
        }

        private static bool ShouldBlockUse(EntityPlayer player, CollectibleObject coll, ItemStack? stack, IWorldAccessor world, EnumHandInteract useType, out CollectibleObject? blockedColl)
        {
            blockedColl = null;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null) return false;
            if (useType == EnumHandInteract.HeldItemAttack)
            {
                bool attackBlocked = !inst.IsAttackAllowed(player, coll);
                if (attackBlocked) blockedColl = coll;
                return attackBlocked;
            }
            if (!inst.IsItemRelevant(coll.Id) && !HasRelevantContent(inst, coll, stack, world, player)) return false;
            HashSet<string> traitCodes = inst.GetPlayerTraitCodes(player);
            if (inst.TryGetFoodOverride(player, coll, traitCodes, out _)) return false;
            if (!inst.IsInteractAllowed(player, coll, traitCodes))
            {
                blockedColl = coll;
                return true;
            }
            return IsContentBlocked(player, inst, coll, stack, world, traitCodes, out blockedColl);
        }

        private static List<ItemStack> TryGetContents(CollectibleObject coll, ItemStack? stack, IWorldAccessor world, EntityPlayer player)
        {
            List<ItemStack> result = new();
            if (stack == null) return result;
            if (coll is BlockLiquidContainerBase liquid)
            {
                try
                {
                    ItemStack? content = liquid.GetContent(stack);
                    if (content != null) result.Add(content);
                }
                catch (Exception ex)
                {
                    Log.Warn(player.Api, typeof(ItemPermissionsPatches), "GetContent failed " + coll.Code + ": " + ex.Message);
                }
                return result;
            }
            if (coll is IBlockMealContainer meal)
            {
                try
                {
                    ItemStack[]? contents = meal.GetContents(world, stack);
                    if (contents != null) result.AddRange(contents.Where(c => c != null)!);
                }
                catch (Exception ex)
                {
                    Log.Warn(player.Api, typeof(ItemPermissionsPatches), "GetContents failed " + coll.Code + ": " + ex.Message);
                }
            }
            return result;
        }

        private static bool HasRelevantContent(TraitItemPermissionsSystem inst, CollectibleObject coll, ItemStack? stack, IWorldAccessor world, EntityPlayer player)
        {
            if (stack == null) return false;
            foreach (ItemStack c in TryGetContents(coll, stack, world, player))
            {
                if (c?.Collectible != null && inst.IsItemRelevant(c.Collectible.Id)) return true;
            }
            return false;
        }

        private static bool IsContentBlocked(EntityPlayer player, TraitItemPermissionsSystem inst, CollectibleObject coll, ItemStack? stack, IWorldAccessor world, HashSet<string> traitCodes, out CollectibleObject? blockedColl)
        {
            blockedColl = null;
            if (stack == null) return false;
            bool isMeal = coll is IBlockMealContainer;
            foreach (ItemStack c in TryGetContents(coll, stack, world, player))
            {
                if (c?.Collectible == null) continue;
                bool blocked = isMeal ? IsSingleBlockedIngredient(player, inst, c.Collectible, traitCodes) : IsSingleBlockedDirect(player, inst, c.Collectible, traitCodes);
                if (blocked)
                {
                    blockedColl = c.Collectible;
                    return true;
                }
            }
            return false;
        }

        private static bool IsSingleBlockedDirect(EntityPlayer player, TraitItemPermissionsSystem inst, CollectibleObject contentColl, HashSet<string> traitCodes)
        {
            if (inst.TryGetFoodOverride(player, contentColl, traitCodes, out _)) return false;
            return !inst.IsInteractAllowed(player, contentColl, traitCodes);
        }

        private static bool IsSingleBlockedIngredient(EntityPlayer player, TraitItemPermissionsSystem inst, CollectibleObject contentColl, HashSet<string> traitCodes)
        {
            if (inst.TryGetFoodOverride(player, contentColl, traitCodes, out _)) return false;
            return !inst.IsIngredientAllowed(player, contentColl, traitCodes);
        }

        private static bool OnHeldInteractStartPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            return TryBlockInteract(player, coll, slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        private static bool OnHeldUseStepPrefix(ItemSlot slot, EntityAgent byEntity, ref EnumHandInteract __result)
        {
            if (IsStepUseBlocked(slot, byEntity)) { __result = EnumHandInteract.None; return false; }
            return true;
        }

        private static bool IsStepUseBlocked(ItemSlot slot, EntityAgent byEntity)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return false;
            return ShouldBlockUse(player, coll, slot?.Itemstack, byEntity.World, player.Controls.HandUse, out _);
        }

        private static bool OnHeldUseStopPrefix(ItemSlot slot, EntityAgent byEntity, EnumHandInteract useType)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (ShouldBlockUse(player, coll, slot?.Itemstack, byEntity.World, useType, out CollectibleObject? blockedColl))
            {
                bool silentMealContent = blockedColl != null && !ReferenceEquals(blockedColl, coll) && coll is IBlockMealContainer;
                if (!silentMealContent) TraitItemPermissionsSystem.SendItemDisallowed(player, blockedColl ?? coll);
                return false;
            }
            return true;
        }

        private static void GetNutritionPropertiesPostfix(ItemStack itemstack, Entity forEntity, ref FoodNutritionProperties __result)
        {
            CollectibleObject? coll = itemstack != null ? itemstack.Collectible : null;
            EntityPlayer? player = forEntity as EntityPlayer;
            if (coll == null || player == null) return;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null) return;
            FoodNutritionProperties? props;
            if (inst.TryGetFoodOverride(player, coll, out props))
                __result = props;
        }

        private static bool TryBlockAttack(EntityPlayer player, CollectibleObject coll, ref EnumHandHandling handling)
        {
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null || inst.IsAttackAllowed(player, coll)) return true;
            handling = EnumHandHandling.PreventDefault;
            TraitItemPermissionsSystem.SendItemDisallowed(player, coll);
            return false;
        }

        private static bool OnHeldAttackStartPrefix(ItemSlot slot, EntityAgent byEntity, ref EnumHandHandling handling)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            return TryBlockAttack(player, coll, ref handling);
        }

        private static bool GetHeldTpHitAnimationPrefix(ItemSlot slot, Entity byEntity, ref string? __result)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null || inst.IsAttackAllowed(player, coll)) return true;
            __result = GetBlockedIdleAnimation(coll, slot, byEntity);
            return false;
        }

        private static bool GetHeldTpUseAnimationPrefix(ItemSlot activeHotbarSlot, Entity forEntity, ref string? __result)
        {
            CollectibleObject? coll = activeHotbarSlot != null && activeHotbarSlot.Itemstack != null ? activeHotbarSlot.Itemstack.Collectible : null;
            EntityPlayer? player = forEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (ShouldBlockUse(player, coll, activeHotbarSlot?.Itemstack, player.Api.World, EnumHandInteract.HeldItemInteract, out _))
            {
                __result = GetBlockedIdleAnimation(coll, activeHotbarSlot, forEntity);
                return false;
            }
            return true;
        }

        private static string? GetBlockedIdleAnimation(CollectibleObject coll, ItemSlot slot, Entity forEntity)
        {
            try { return coll.GetHeldTpIdleAnimation(slot, forEntity, EnumHand.Right); }
            catch (Exception ex)
            {
                Log.Warn(forEntity.Api, typeof(ItemPermissionsPatches), "GetHeldTpIdleAnimation failed for blocked " + coll.Code + ": " + ex.Message);
                return null;
            }
        }

        private static bool OnHeldUseStartPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, EnumHandInteract useType, bool firstEvent, ref EnumHandHandling handling)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (useType == EnumHandInteract.HeldItemAttack) return TryBlockAttack(player, coll, ref handling);
            return TryBlockInteract(player, coll, slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        private static bool OnHeldInteractStepPrefix(ItemSlot slot, EntityAgent byEntity, ref bool __result)
        {
            if (IsStepUseBlocked(slot, byEntity)) { __result = false; return false; }
            return true;
        }

        private static bool OnHeldIdlePrefix(ItemSlot slot, EntityAgent byEntity)
        {
            ItemStack? stack = slot?.Itemstack;
            CollectibleObject? coll = stack?.Collectible;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (byEntity.Controls == null || !byEntity.Controls.Sneak || byEntity.Controls.RightMouseDown) return true;
            if (!IsShield(coll, stack)) return true;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null || inst.IsInteractAllowed(player, coll)) return true;
            StopShieldRaiseAnim(byEntity, slot);
            return false;
        }

        private static readonly Dictionary<int, bool> _shieldCache = new();
        private static bool _reportedShieldAttrError;

        private static bool IsShield(CollectibleObject coll, ItemStack? stack)
        {
            if (coll is ItemShield) return true;
            try { if (stack?.Attributes?.HasAttribute("shield") == true) return true; }
            catch (Exception ex)
            {
                if (!_reportedShieldAttrError)
                {
                    _reportedShieldAttrError = true;
                    Log.Warn(null, typeof(ItemPermissionsPatches), "IsShield stack.Attributes read failed: " + ex.Message);
                }
            }

            int id = coll.Id;
            lock (_shieldCache)
            {
                if (_shieldCache.TryGetValue(id, out bool cached)) return cached;
            }
            bool result;
            try { result = stack?.ItemAttributes?["shield"]?.Exists == true; }
            catch (Exception ex)
            {
                result = false;
                if (!_reportedShieldAttrError)
                {
                    _reportedShieldAttrError = true;
                    Log.Warn(null, typeof(ItemPermissionsPatches), "IsShield type attribute read failed: " + ex.Message);
                }
            }

            if (!result)
            {
                try { result = coll.Attributes?["shield"]?.Exists == true; }
                catch (Exception ex)
                {
                    result = false;
                    if (!_reportedShieldAttrError)
                    {
                        _reportedShieldAttrError = true;
                        Log.Warn(null, typeof(ItemPermissionsPatches), "IsShield type attribute read failed: " + ex.Message);
                    }
                }
            }
            lock (_shieldCache)
            {
                if (_shieldCache.Count < 8192) _shieldCache[id] = result;
            }
            return result;
        }

        private static void StopShieldRaiseAnim(EntityAgent byEntity, ItemSlot? slot)
        {
            string anim = byEntity.LeftHandItemSlot == slot ? ItemShield.RaiseShieldLeftAnim : ItemShield.RaiseShieldRightAnim;
            if (byEntity.AnimManager?.IsAnimationActive(anim) == true)
                byEntity.AnimManager.StopAnimation(anim);
        }
        
        private static bool ShieldDisallowPrefix(IPlayer player, float damage, ref float __result)
        {
            try
            {
                EntityPlayer? entityPlayer = player.Entity;
                if (entityPlayer == null) return true;
                if (entityPlayer.Controls?.Sneak != true) return true;
                ItemStack? leftStack = entityPlayer.LeftHandItemSlot?.Itemstack;
                ItemStack? rightStack = entityPlayer.RightHandItemSlot?.Itemstack;
                CollectibleObject? leftColl = leftStack?.Collectible;
                CollectibleObject? rightColl = rightStack?.Collectible;
                if (!((leftColl != null && IsShield(leftColl, leftStack)) || (rightColl != null && IsShield(rightColl, rightStack)))) return true;
                TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(entityPlayer);
                if (inst == null) return true;
                bool leftDisallowed = leftColl != null && IsShield(leftColl, leftStack) && !inst.IsInteractAllowed(entityPlayer, leftColl);
                bool rightDisallowed = rightColl != null && IsShield(rightColl, rightStack) && !inst.IsInteractAllowed(entityPlayer, rightColl);
                if (!leftDisallowed && !rightDisallowed) return true;
                __result = damage;
                return false;
            }
            catch (Exception ex)
            {
                Log.Warn(null, typeof(ItemPermissionsPatches), "ShieldDisallow check failed open: " + ex.Message);
                return true;
            }
        }

        private static void GetNutritionPropertiesPerLitrePostfix(BlockLiquidContainerBase __instance, ItemStack itemstack, Entity forEntity, ref FoodNutritionProperties __result)
        {
            EntityPlayer? player = forEntity as EntityPlayer;
            if (player == null || itemstack == null) return;
            ItemStack? content;
            try { content = __instance.GetContent(itemstack); }
            catch (Exception ex)
            { 
                Log.Warn(player.Api, typeof(ItemPermissionsPatches), "GetContent failed " + __instance.Code + ex.Message);
                return;
            }
            CollectibleObject? contentColl = content?.Collectible;
            if (contentColl == null) return;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null) return;
            if (!inst.TryGetFoodOverride(player, contentColl, out FoodNutritionProperties? edited) || edited == null)
            {
                if (!inst.IsInteractAllowed(player, contentColl)) __result = null!;
                return;
            }
            if (__result == null) return;

            FoodNutritionProperties repl = edited.Clone();
            FoodNutritionProperties? baseProps = TraitItemPermissionsSystem.GetBaseNutritionProps(player.Api, contentColl);
            float satMul = (baseProps != null && baseProps.Satiety != 0f) ? __result.Satiety / baseProps.Satiety : 1f;
            float hpMul = (baseProps != null && baseProps.Health != 0f) ? __result.Health / baseProps.Health : satMul;
            repl.Satiety *= satMul;
            repl.Health *= hpMul;
            __result = repl;
        }

        private static void GetHeldItemInfoPostfix(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
        {
            if (!(world is IClientWorldAccessor cworld)) return;
            if (inSlot == null) return;
            CollectibleObject? coll = inSlot.Itemstack?.Collectible;
            EntityPlayer? player = cworld.Player?.Entity;
            if (player == null || coll == null) return;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null) return;
            if (_inGetHeldItemInfo) return;
            _inGetHeldItemInfo = true;
            try
            {
                GetHeldItemInfoBody(inSlot, dsc, world, player, coll, inst);
            }
            finally
            {
                _inGetHeldItemInfo = false;
            }
        }

        private static void GetHeldItemInfoBody(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, EntityPlayer player, CollectibleObject coll, TraitItemPermissionsSystem inst)
        {
            string text = dsc.ToString();
            string foodText = Lang.Get("playermodellib:tooltiptext-foodnotallowed");
            string itemText = Lang.Get("playermodellib:tooltiptext-itemnotallowed");
            if (text.IndexOf(foodText, StringComparison.Ordinal) >= 0 || text.IndexOf(itemText, StringComparison.Ordinal) >= 0) return;
            if (inst.TryGetFoodOverride(player, coll, out FoodNutritionProperties? edited))
            {
                ReplaceNutritionTooltipLine(inSlot, dsc, world, player, coll, edited, coll.NutritionProps);
                return;
            }
            ItemStack? stack = inSlot.Itemstack;
            if (coll is BlockLiquidContainerBase container && stack != null)
            {
                bool handled = false;
                ItemStack? content = null;
                try { content = container.GetContent(stack); }
                catch (Exception ex)
                {
                    Log.Warn(player.Api, typeof(ItemPermissionsPatches), "GetContent failed " + coll.Code + ex.Message);
                }
                if (content?.Collectible != null && inst.TryGetFoodOverride(player, content.Collectible, out FoodNutritionProperties? contentEdited) && contentEdited != null)
                {
                    float litres = 0f;
                    try { litres = container.GetCurrentLitres(stack); }
                    catch (Exception ex)
                    {
                        Log.Warn(player.Api, typeof(ItemPermissionsPatches), "GetCurrentLitres failed " + coll.Code + ex.Message);
                    }
                    if (litres > 0f)
                    {
                        FoodNutritionProperties scaled = contentEdited.Clone();
                        scaled.Satiety *= litres;
                        scaled.Health *= litres;
                        FoodNutritionProperties? contentBase = TraitItemPermissionsSystem.GetBaseNutritionProps(player.Api, content.Collectible);
                        FoodNutritionProperties? scaledOrig = null;
                        if (contentBase != null)
                        {
                            scaledOrig = contentBase.Clone();
                            scaledOrig.Satiety *= litres;
                            scaledOrig.Health *= litres;
                        }
                        ItemSlot contentDummy = new DummySlot(content);
                        float contentSpoil = content.Collectible.AppendPerishableInfoText(contentDummy, new StringBuilder(), world);
                        ReplaceNutritionTooltipLine(inSlot, dsc, world, player, coll, scaled, scaledOrig,
                            contentSpoil, content);
                        handled = true;
                    }
                }
                if (handled) return;
            }
            string? notAllowedKey = null;
            if (IsContentBlocked(player, inst, coll, stack, world, inst.GetPlayerTraitCodes(player), out CollectibleObject? blockedColl))
                notAllowedKey = blockedColl != null && (TraitItemPermissionsSystem.HasNutrition(player.Api, blockedColl) || coll is IBlockMealContainer) ? "playermodellib:tooltiptext-foodnotallowed" : "playermodellib:tooltiptext-itemnotallowed";
            else if (!inst.IsAttackAllowed(player, coll) || !inst.IsInteractAllowed(player, coll))
                notAllowedKey = TraitItemPermissionsSystem.HasNutrition(player.Api, coll) || coll is IBlockMealContainer ? "playermodellib:tooltiptext-foodnotallowed" : "playermodellib:tooltiptext-itemnotallowed";
            if (notAllowedKey == null) return;
            dsc.AppendLine("<font color=\"#ff8484\">" + Lang.Get(notAllowedKey) + "</font>");
        }

        private static void ReplaceNutritionTooltipLine(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, EntityPlayer player, CollectibleObject coll, FoodNutritionProperties edited, FoodNutritionProperties? orig, float? spoilStateOverride = null, ItemStack? spoilStack = null)
        {
            ItemStack stack = spoilStack ?? inSlot.Itemstack!;
            bool satEdited = orig == null || orig.Satiety != edited.Satiety;
            bool hpEdited = orig == null ? edited.Health != 0f : orig.Health != edited.Health;
            if (!satEdited && !hpEdited) return;

            float spoilState = spoilStateOverride ?? coll.AppendPerishableInfoText(inSlot, new StringBuilder(), world);
            float satMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, stack, player);
            float hpMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, stack, player);

            bool liquid = coll.MatterState == EnumMatterState.Liquid;
            bool showHp = Math.Abs(edited.Health * hpMul) > 0.001f;

            double editedSat = Math.Round(edited.Satiety * satMul);
            double editedHp = Math.Round(edited.Health * hpMul, 2);

            double? origSat = orig == null ? null : Math.Round(orig.Satiety * satMul);
            double? origHp = orig == null ? null : Math.Round(orig.Health * hpMul, 2);

            object satArg = satEdited ? FormatEditedValue(editedSat, origSat) : editedSat;
            if (showHp)
            {
                object hpArg = hpEdited ? FormatEditedValue(editedHp, origHp) : editedHp;
                string hpKey = liquid ? "liquid-when-drunk-saturation-hp" : "When eaten: {0} sat, {1} hp";
                dsc.Replace(Lang.Get(hpKey, editedSat, editedHp), Lang.Get(hpKey, satArg, hpArg));
            }
            else
            {
                string satKey = liquid ? "liquid-when-drunk-saturation" : "When eaten: {0} sat";
                dsc.Replace(Lang.Get(satKey, editedSat), Lang.Get(satKey, satArg));
            }
        }

        private static string FormatEditedValue(double edited, double? orig)
        {
            string yellow = "<font color=\"#ffe14d\">" + edited + "</font>";
            if (orig == null) return yellow;
            return yellow + " (" + orig + ")";
        }
    }
}