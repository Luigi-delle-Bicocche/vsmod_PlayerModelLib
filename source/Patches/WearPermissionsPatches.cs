using HarmonyLib;
using OverhaulLib.Utils;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace PlayerModelLib
{
    public static class WearPermissionsPatches
    {
        private static Harmony? _harmony;

        public static void Patch(string harmonyId, ICoreAPI api)
        {
            if (_harmony != null) return;
            Harmony harmony = new Harmony(harmonyId);
            try
            {
                Type t = typeof(ItemSlotCharacter);

                MethodInfo? m = t.GetMethod(nameof(ItemSlotCharacter.CanHold), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(WearPermissionsPatches), "Could not find ItemSlotCharacter.CanHold");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(WearPermissionsPatches), nameof(CanHoldPrefix))));

                m = t.GetMethod(nameof(ItemSlotCharacter.CanTakeFrom), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(WearPermissionsPatches), "Could not find ItemSlotCharacter.CanTakeFrom");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(WearPermissionsPatches), nameof(CanTakeFromPrefix))));

                m = typeof(CollectibleBehaviorWearable).GetMethod(nameof(CollectibleBehaviorWearable.OnHeldInteractStart), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(WearPermissionsPatches), "Could not find CollectibleBehaviorWearable.OnHeldInteractStart");
                    return;
                }
                harmony.Patch(m, prefix: new HarmonyMethod(AccessTools.Method(typeof(WearPermissionsPatches), nameof(OnHeldInteractStartPrefix))));

                m = typeof(CollectibleObject).GetMethod(nameof(CollectibleObject.GetHeldItemInfo), AccessTools.all);
                if (m == null)
                {
                    Log.Warn(api, typeof(WearPermissionsPatches), "Could not find CollectibleObject.GetHeldItemInfo");
                    return;
                }
                harmony.Patch(m, postfix: new HarmonyMethod(AccessTools.Method(typeof(WearPermissionsPatches), nameof(GetHeldItemInfoPostfix))));
                _harmony = harmony;
            }
            catch (Exception ex)
            {
                Log.Error(api, typeof(WearPermissionsPatches), "Failed to apply patches (" + harmonyId + "): " + ex.Message);
            }
        }

        public static void Unpatch(string harmonyId)
        {
            Harmony? h = _harmony;
            if (h == null) return;
            h.UnpatchAll(harmonyId);
            _harmony = null;
        }

        private static EntityPlayer? ResolveCharacterSlotOwner(ItemSlotCharacter? slot)
        {
            InventoryBase? inv = slot?.Inventory;
            ICoreAPI api = inv.Api;
            if (api is ICoreClientAPI capi) return capi.World?.Player?.Entity;
            if (api is ICoreServerAPI sapi)
            {
                EntityPlayer? owner = TryResolveOwnerByPlayerUid(sapi, inv, slot);
                if (owner != null) return owner;
            }
            return null;
        }

        private static EntityPlayer? TryResolveOwnerByPlayerUid(ICoreServerAPI sapi, InventoryBase? inv, ItemSlot? slot)
        {
            string? inventoryId = inv?.InventoryID;
            string prefix = GlobalConstants.characterInvClassName + "-";
            if (string.IsNullOrEmpty(inventoryId) || !inventoryId.StartsWith(prefix, StringComparison.Ordinal)) return null;
            string playerUid = inventoryId.Substring(prefix.Length);
            if (string.IsNullOrEmpty(playerUid)) return null;
            if (sapi.World.PlayerByUid(playerUid) is not IServerPlayer serverPlayer) return null;
            if (serverPlayer.Entity is not EntityPlayer playerEntity) return null;
            IInventory? characterInventory = serverPlayer.InventoryManager?.GetOwnInventory(GlobalConstants.characterInvClassName);
            if (characterInventory == null) return null;
            if (ContainsSlot(characterInventory, slot)) return playerEntity;
            return null;
        }

        private static bool ContainsSlot(IInventory inventory, ItemSlot? slot)
        {
            foreach (ItemSlot candidateSlot in inventory)
            {
                if (ReferenceEquals(candidateSlot, slot)) return true;
            }
            return false;
        }

        private static bool TryBlockWear(ICoreAPI api, CollectibleObject? coll, Func<EntityPlayer?> resolveOwner)
        {
            if (coll == null) return true;
            TraitItemPermissionsSystem? inst = api.ModLoader.GetModSystem<TraitItemPermissionsSystem>();
            if (inst == null) return true;
            if (!inst.IsWearRelevant(coll.Id)) return true;
            EntityPlayer? player = resolveOwner();
            if (player == null || inst.IsWearAllowed(player, coll)) return true;
            TraitItemPermissionsSystem.SendWearDisallowed(player);
            return false;
        }

        private static bool CanHoldPrefix(ItemSlotCharacter __instance, ItemSlot itemstackFromSourceSlot, ref bool __result)
        {
            CollectibleObject? coll = itemstackFromSourceSlot?.Itemstack?.Collectible;
            ICoreAPI api = __instance.Inventory.Api;
            if (!TryBlockWear(api, coll, () => ResolveCharacterSlotOwner(__instance))) { __result = false; return false; }
            return true;
        }

        private static bool CanTakeFromPrefix(ItemSlotCharacter __instance, ItemSlot sourceSlot, ref bool __result)
        {
            CollectibleObject? coll = sourceSlot?.Itemstack?.Collectible;
            ICoreAPI api = __instance.Inventory.Api;
            if (!TryBlockWear(api, coll, () => ResolveCharacterSlotOwner(__instance))) { __result = false; return false; }
            return true;
        }

        private static bool OnHeldInteractStartPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            CollectibleObject? coll = slot?.Itemstack?.Collectible;
            EntityPlayer? player = byEntity as EntityPlayer;
            if (player == null) return true;
            if (IsNonSelfDressInteract(byEntity, blockSel, entitySel)) return true;
            if (!TryBlockWear(player.Api, coll, () => player)) { handHandling = EnumHandHandling.PreventDefault; handling = EnumHandling.PreventSubsequent; return false; }
            return true;
        }
        
        private static bool IsNonSelfDressInteract(EntityAgent byEntity, BlockSelection? blockSel, EntitySelection? entitySel)
        {
            if (byEntity.Controls?.ShiftKey == true) return true;
            if (entitySel?.Entity?.GetBehavior<EntityBehaviorAttachable>() != null) return true;
            if (blockSel == null) return false;
            try
            {
                IWorldAccessor? world = byEntity.World;
                BlockPos pos = blockSel.Position;
                Block? block = world?.BlockAccessor?.GetBlock(pos);
                if (block?.GetBehavior<BlockBehaviorMultiblock>()?.ControllerPositionRel is { } relPos)
                    pos = pos.AddCopy(relPos);
                BlockEntity? be = world?.BlockAccessor?.GetBlockEntity(pos);
                if (be?.Behaviors == null) return false;
                foreach (BlockEntityBehavior bh in be.Behaviors)
                {
                    if (bh is BEBehaviorMannequin or BEBehaviorDisplay) return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warn(byEntity.Api, typeof(WearPermissionsPatches), "Stand interact check failed: " + ex.Message);
                return false;
            }
            return false;
        }

        private static void GetHeldItemInfoPostfix(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
        {
            if (dsc == null || world is not IClientWorldAccessor cworld) return;
            EntityPlayer? player = cworld.Player?.Entity;
            CollectibleObject? coll = inSlot?.Itemstack?.Collectible;
            if (player == null || coll == null) return;
            TraitItemPermissionsSystem? inst = TraitItemPermissionsSystem.GetInstance(player);
            if (inst == null || !inst.IsWearRelevant(coll.Id) || inst.IsWearAllowed(player, coll)) return;
            dsc.AppendLine("<font color=\"#ff8484\">" + Lang.Get("playermodellib:tooltiptext-wearnotallowed") + "</font>");
        }
    }
}