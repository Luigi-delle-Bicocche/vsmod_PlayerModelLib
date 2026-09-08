using HarmonyLib;
using OverhaulLib.Utils;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace PlayerModelLib
{
    public static class ItemPermissionsPatches
    {
        private static Harmony? _harmony;

        private const string BlockedAnimation = "playermodellib-blocked";

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

        private static bool TryBlockInteract(EntityPlayer player, CollectibleObject coll, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null) return true;
            if (inst.TryGetFoodOverride(player, coll, out _)) return true;
            if (inst.IsInteractAllowed(player, coll)) return true;
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

        private static bool ShouldBlockUse(EntityPlayer player, CollectibleObject coll, ItemStack? stack, IWorldAccessor world, EnumHandInteract useType)
        {
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null) return false;
            if (useType == EnumHandInteract.HeldItemAttack) return !inst.IsAttackAllowed(player, coll);
            if (inst.TryGetFoodOverride(player, coll, out _)) return false;
            if (!inst.IsInteractAllowed(player, coll)) return true;
            return IsContentBlocked(player, inst, coll, stack, world, out _);
        }

        private static bool IsContentBlocked(EntityPlayer player, TraitItemPermissionsSystem inst, CollectibleObject coll, ItemStack? stack, IWorldAccessor world, out CollectibleObject? blockedColl)
        {
            blockedColl = null;
            if (stack == null) return false;
            if (coll is BlockLiquidContainerBase liquid)
            {
                ItemStack? content = null;
                try { content = liquid.GetContent(stack); }
                catch (Exception ex)
                {
                    Log.Warn(player.Api, typeof(ItemPermissionsPatches), "GetContent failed " + coll.Code + ": " + ex.Message);
                    return false;
                }
                if (content?.Collectible != null && IsSingleBlocked(player, inst, content.Collectible))
                {
                    blockedColl = content.Collectible;
                    return true;
                }
                return false;
            }
            if (coll is IBlockMealContainer meal)
            {
                ItemStack[] contents;
                try { contents = meal.GetContents(world, stack); }
                catch (Exception ex)
                {
                    Log.Warn(player.Api, typeof(ItemPermissionsPatches), "GetContents failed " + coll.Code + ": " + ex.Message);
                    return false;
                }
                if (contents == null) return false;
                foreach (ItemStack? c in contents)
                {
                    if (c?.Collectible != null && IsSingleBlocked(player, inst, c.Collectible))
                    {
                        blockedColl = c.Collectible;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsSingleBlocked(EntityPlayer player, TraitItemPermissionsSystem inst, CollectibleObject contentColl)
        {
            if (inst.TryGetFoodOverride(player, contentColl, out _)) return false;
            return !inst.IsInteractAllowed(player, contentColl);
        }

        private static bool OnHeldInteractStartPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (slot == null || coll == null || player == null) return true;
            return TryBlockInteract(player, coll, slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        private static bool OnHeldUseStepPrefix(ItemSlot slot, EntityAgent byEntity, ref EnumHandInteract __result)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (ShouldBlockUse(player, coll, slot?.Itemstack, byEntity.World, player.Controls.HandUse)) { __result = EnumHandInteract.None; return false; }
            return true;
        }

        private static bool OnHeldUseStopPrefix(ItemSlot slot, EntityAgent byEntity, EnumHandInteract useType)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (ShouldBlockUse(player, coll, slot?.Itemstack, byEntity.World, useType)) { TraitItemPermissionsSystem.SendItemDisallowed(player, coll); return false; }
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
            if (inst == null) return true;
            if (inst.IsAttackAllowed(player, coll)) return true;
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

        private static bool GetHeldTpHitAnimationPrefix(ItemSlot slot, Entity byEntity, ref string __result)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null || inst.IsAttackAllowed(player, coll)) return true;
            __result = BlockedAnimation;
            return false;
        }

        private static bool GetHeldTpUseAnimationPrefix(ItemSlot activeHotbarSlot, Entity forEntity, ref string __result)
        {
            CollectibleObject? coll = activeHotbarSlot != null && activeHotbarSlot.Itemstack != null ? activeHotbarSlot.Itemstack.Collectible : null;
            EntityPlayer? player = forEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null) return true;
            if (inst.TryGetFoodOverride(player, coll, out _)) return true;
            if (inst.IsInteractAllowed(player, coll)) return true;
            __result = BlockedAnimation;
            return false;
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
            EntityPlayer? player = cworld.Player?.Entity;
            CollectibleObject? coll = inSlot?.Itemstack?.Collectible;
            if (inSlot == null || player == null || coll == null) return;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null) return;
            if (inst.TryGetFoodOverride(player, coll, out FoodNutritionProperties? edited))
            {
                ReplaceNutritionTooltipLine(inSlot, dsc, world, player, coll, edited, coll.NutritionProps);
                return;
            }
            if (coll is BlockLiquidContainerBase container && inSlot.Itemstack != null)
            {
                bool handled = false;
                ItemStack? content = null;
                try { content = container.GetContent(inSlot.Itemstack); }
                catch (Exception ex)
                {
                    Log.Warn(player.Api, typeof(ItemPermissionsPatches), "GetContent failed " + coll.Code + ex.Message);
                }
                if (content?.Collectible != null && inst.TryGetFoodOverride(player, content.Collectible, out FoodNutritionProperties? contentEdited) && contentEdited != null)
                {
                    float litres = 0f;
                    try { litres = container.GetCurrentLitres(inSlot.Itemstack); }
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
            if (IsContentBlocked(player, inst, coll, inSlot.Itemstack, world, out CollectibleObject? blockedColl))
            {
                string contentKey = blockedColl?.NutritionProps != null ? "playermodellib:tooltiptext-foodnotallowed" : "playermodellib:tooltiptext-itemnotallowed";
                dsc.AppendLine("<font color=\"#ff8484\">" + Lang.Get(contentKey) + "</font>");
                return;
            }
            if (inst.IsInteractAllowed(player, coll)) return;
            string key = coll.NutritionProps != null ? "playermodellib:tooltiptext-foodnotallowed" : "playermodellib:tooltiptext-itemnotallowed";
            dsc.AppendLine("<font color=\"#ff8484\">" + Lang.Get(key) + "</font>");
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