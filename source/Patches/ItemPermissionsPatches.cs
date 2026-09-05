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

        private static void SendDisallowed(EntityPlayer player, CollectibleObject coll)
        {
            if (coll != null && coll.NutritionProps != null) return;
            string msg = Lang.Get("playermodellib:itemdisallowed");
            if (player.Player is IServerPlayer sp) sp.SendIngameError("itemdisallowed", msg);
            else if (player.Api is ICoreClientAPI capi && player.PlayerUID == capi.World.Player?.PlayerUID)
                capi.TriggerIngameError(null, "itemdisallowed", msg);
        }

        private static TraitItemPermissionsSystem? ForPlayer(EntityPlayer player)
        {
            ICoreAPI api = player.Api;
            return api.ModLoader.GetModSystem<TraitItemPermissionsSystem>();
        }

        private static bool TryBlockInteract(EntityPlayer player, CollectibleObject coll, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            TraitItemPermissionsSystem? inst = ForPlayer(player);
            if (inst == null) return true;
            if (inst.TryGetFoodOverride(player, coll, out _)) return true;
            if (inst.IsInteractAllowed(player, coll)) return true;
            if (firstEvent && blockSel != null && entitySel == null && byEntity.Controls.ShiftKey && TryGroundStore(coll, slot, byEntity, blockSel, entitySel, firstEvent, ref handling)) return false;
            handling = EnumHandHandling.PreventDefault;
            SendDisallowed(player, coll);
            return false;
        }

        private static bool TryGroundStore(CollectibleObject coll, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (coll.CollectibleBehaviors == null) return false;
            foreach (CollectibleBehavior behavior in coll.CollectibleBehaviors)
            {
                if (behavior is CollectibleBehaviorGroundStorable groundStorable)
                {
                    EnumHandHandling bhHandHandling = EnumHandHandling.NotHandled;
                    EnumHandling bhHandling = EnumHandling.PassThrough;
                    groundStorable.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref bhHandHandling, ref bhHandling);
                    if (bhHandHandling == EnumHandHandling.NotHandled && bhHandling == EnumHandling.PassThrough) return false;
                    handling = bhHandHandling;
                    return true;
                }
            }
            return false;
        }

        private static bool ShouldBlockUse(EntityPlayer player, CollectibleObject coll, EnumHandInteract useType)
        {
            TraitItemPermissionsSystem? inst = ForPlayer(player);
            if (inst == null) return false;
            if (useType == EnumHandInteract.HeldItemAttack) return !inst.IsAttackAllowed(player, coll);
            if (inst.TryGetFoodOverride(player, coll, out _)) return false;
            return !inst.IsInteractAllowed(player, coll);
        }

        private static bool OnHeldInteractStartPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            return TryBlockInteract(player, coll, slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        private static bool OnHeldUseStepPrefix(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandInteract __result)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (ShouldBlockUse(player, coll, player.Controls.HandUse)) { __result = EnumHandInteract.None; return false; }
            return true;
        }

        private static bool OnHeldUseStopPrefix(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumHandInteract useType)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (ShouldBlockUse(player, coll, useType)) return false;
            return true;
        }

        private static void GetNutritionPropertiesPostfix(IWorldAccessor world, ItemStack itemstack, Entity forEntity, ref FoodNutritionProperties __result)
        {
            CollectibleObject? coll = itemstack != null ? itemstack.Collectible : null;
            EntityPlayer? player = forEntity as EntityPlayer;
            if (coll == null || player == null) return;
            TraitItemPermissionsSystem? inst = ForPlayer(player);
            if (inst == null) return;
            FoodNutritionProperties? props;
            if (inst.TryGetFoodOverride(player, coll, out props))
                __result = props;
        }

        private static bool TryBlockAttack(EntityPlayer player, CollectibleObject coll, ref EnumHandHandling handling)
        {
            TraitItemPermissionsSystem? inst = ForPlayer(player);
            if (inst == null) return true;
            if (inst.IsAttackAllowed(player, coll)) return true;
            handling = EnumHandHandling.PreventDefault;
            SendDisallowed(player, coll);
            return false;
        }

        private static bool OnHeldAttackStartPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
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
            TraitItemPermissionsSystem? inst = ForPlayer(player);
            if (inst == null || inst.IsAttackAllowed(player, coll)) return true;
            __result = BlockedAnimation;
            return false;
        }

        private static bool GetHeldTpUseAnimationPrefix(ItemSlot activeHotbarSlot, Entity forEntity, ref string __result)
        {
            CollectibleObject? coll = activeHotbarSlot != null && activeHotbarSlot.Itemstack != null ? activeHotbarSlot.Itemstack.Collectible : null;
            EntityPlayer? player = forEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            TraitItemPermissionsSystem? inst = ForPlayer(player);
            if (inst == null) return true;
            if (inst.TryGetFoodOverride(player, coll, out _)) return true;
            if (inst.IsInteractAllowed(player, coll)) return true;
            __result = BlockedAnimation;
            return false;
        }

        private static void GetHeldItemInfoPostfix(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (dsc == null || !(world is IClientWorldAccessor cworld)) return;
            EntityPlayer? player = cworld.Player != null ? cworld.Player.Entity as EntityPlayer : null;
            CollectibleObject? coll = inSlot != null && inSlot.Itemstack != null ? inSlot.Itemstack.Collectible : null;
            if (player == null || coll == null) return;
            TraitItemPermissionsSystem? inst = ForPlayer(player);
            if (inst == null) return;
            if (inst.TryGetFoodOverride(player, coll, out _)) return;
            if (inst.IsInteractAllowed(player, coll)) return;
            string key = coll.NutritionProps != null ? "playermodellib:tooltiptext-foodnotallowed" : "playermodellib:tooltiptext-itemnotallowed";
            dsc.AppendLine("<font color=\"#ff8484\">" + Lang.Get(key) + "</font>");
        }
    }
}