using HarmonyLib;
using OverhaulLib.Utils;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

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

                m = t.GetMethod(nameof(CollectibleObject.GetNutritionProperties), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(ItemPermissionsPatches), "Could not find CollectibleObject.GetNutritionProperties");
                    return;
                }
                harmony.Patch(m, postfix: new HarmonyMethod(AccessTools.Method(typeof(ItemPermissionsPatches), nameof(GetNutritionPropertiesPostfix))));
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

        private static void SendDisallowed(EntityPlayer player)
        { 
            string msg = "placeholder";
            if (player.Player is IServerPlayer sp) sp.SendIngameError("placeholder", msg);
            else if (player.Api is ICoreClientAPI capi && player.PlayerUID == capi.World.Player?.PlayerUID)
                capi.TriggerIngameError(null, "placeholder", msg);
        }

        private static TraitItemPermissionsSystem? ForPlayer(EntityPlayer player)
        {
            ICoreAPI api = player.Api;
            return api.ModLoader.GetModSystem<TraitItemPermissionsSystem>();
        }

        private static bool TryBlockInteract(EntityPlayer player, CollectibleObject coll, ref EnumHandHandling handling)
        {
            TraitItemPermissionsSystem? inst = ForPlayer(player);
            if (inst == null) return true;
            if (inst.TryGetFoodOverride(player, coll, out _)) return true;
            if (inst.IsInteractAllowed(player, coll)) return true;
            handling = EnumHandHandling.PreventDefault;
            SendDisallowed(player);
            return false;
        }

        private static bool ShouldBlockUse(EntityPlayer player, CollectibleObject coll)
        {
            TraitItemPermissionsSystem? inst = ForPlayer(player);
            if (inst == null) return false;
            if (inst.TryGetFoodOverride(player, coll, out _)) return false;
            return !inst.IsInteractAllowed(player, coll);
        }

        private static bool OnHeldInteractStartPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            return TryBlockInteract(player, coll, ref handling);
        }

        private static bool OnHeldUseStepPrefix(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandInteract __result)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (ShouldBlockUse(player, coll)) { __result = EnumHandInteract.None; return false; }
            return true;
        }

        private static bool OnHeldUseStopPrefix(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumHandInteract useType)
        {
            CollectibleObject? coll = slot != null && slot.Itemstack != null ? slot.Itemstack.Collectible : null;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (coll == null || player == null) return true;
            if (ShouldBlockUse(player, coll)) return false;
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
            else if (!inst.IsInteractAllowed(player, coll))
                __result = null!;
        }
    }
}