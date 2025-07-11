using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Modules.ModSuit.Components;
using Content.Shared.Modules.ModSuit.Events;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Clothing.EntitySystems;

public sealed class HideLayerClothingSystem : EntitySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HideLayerClothingComponent, ClothingGotUnequippedEvent>(OnHideGotUnequipped);
        SubscribeLocalEvent<HideLayerClothingComponent, ClothingGotEquippedEvent>(OnHideGotEquipped);
        SubscribeLocalEvent<HideLayerClothingComponent, ItemMaskToggledEvent>(OnHideToggled);

        SubscribeLocalEvent<HideLayerClothingComponent, ModSuitSealedEvent>(OnModSuitSealed);
        SubscribeLocalEvent<HideLayerClothingComponent, ModSuitUnsealedEvent>(OnModSuitUnsealed);
    }

    private void OnHideToggled(Entity<HideLayerClothingComponent> ent, ref ItemMaskToggledEvent args)
    {
        if (args.Wearer != null)
            SetLayerVisibility(ent!, args.Wearer.Value, hideLayers: true);
    }

    private void OnHideGotEquipped(Entity<HideLayerClothingComponent> ent, ref ClothingGotEquippedEvent args)
    {
        SetLayerVisibility(ent!, args.Wearer, hideLayers: true);
    }

    private void OnHideGotUnequipped(Entity<HideLayerClothingComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        SetLayerVisibility(ent!, args.Wearer, hideLayers: false);
    }

    private void OnModSuitSealed(Entity<HideLayerClothingComponent> ent, ref ModSuitSealedEvent args)
    {
        if (args.Wearer != null)
            SetLayerVisibility(ent!, args.Wearer.Value, true);
    }

    private void OnModSuitUnsealed(Entity<HideLayerClothingComponent> ent, ref ModSuitUnsealedEvent args)
    {
        if (args.Wearer != null)
            SetLayerVisibility(ent!, args.Wearer.Value, !ent.Comp.HideOnToggle);
    }

    private void SetLayerVisibility(
        Entity<HideLayerClothingComponent?, ClothingComponent?> clothing,
        Entity<HumanoidAppearanceComponent?> user,
        bool hideLayers)
    {
        if (_timing.ApplyingState)
            return;

        if (!Resolve(clothing.Owner, ref clothing.Comp1, ref clothing.Comp2))
            return;

        // logMissing: false, as this clothing might be getting equipped by a non-human.
        if (!Resolve(user.Owner, ref user.Comp, false))
            return;

        hideLayers &= IsEnabled(clothing!);

        var hideable = user.Comp.HideLayersOnEquip;
        var inSlot = clothing.Comp2.InSlotFlag ?? SlotFlags.NONE;

        // This method should only be getting called while the clothing is equipped (though possibly currently in
        // the process of getting unequipped).
        DebugTools.AssertNotNull(clothing.Comp2.InSlot);
        DebugTools.AssertNotNull(clothing.Comp2.InSlotFlag);
        DebugTools.AssertNotEqual(inSlot, SlotFlags.NONE);

        var dirty = false;

        // iterate the HideLayerClothingComponent's layers map and check that
        // the clothing is (or was)equipped in a matching slot.
        foreach (var (layer, validSlots) in clothing.Comp1.Layers)
        {
            if (!hideable.Contains(layer))
                continue;

            // Only update this layer if we are currently equipped to the relevant slot.
            if (validSlots.HasFlag(inSlot))
                _humanoid.SetLayerVisibility(user!, layer, !hideLayers, inSlot, ref dirty);
        }

        // Fallback for obsolete field: assume we want to hide **all** layers, as long as we are equipped to any
        // relevant clothing slot
#pragma warning disable CS0618 // Type or member is obsolete
        if (clothing.Comp1.Slots is { } slots && clothing.Comp2.Slots.HasFlag(inSlot))
#pragma warning restore CS0618 // Type or member is obsolete
        {
            foreach (var layer in slots)
            {
                if (hideable.Contains(layer))
                    _humanoid.SetLayerVisibility(user!, layer, !hideLayers, inSlot, ref dirty);
            }
        }

        if (dirty)
            Dirty(user!);
    }

    private bool IsEnabled(Entity<HideLayerClothingComponent, ClothingComponent> clothing)
    {
        // TODO Generalize this
        // I.e., make this and mask component use some generic toggleable.

        if (!clothing.Comp1.HideOnToggle)
            return true;

        if (TryComp<MaskComponent>(clothing, out var mask) && mask.IsToggled)
            return true;

        if (TryComp<ModSuitSealableComponent>(clothing, out var sealable) && sealable.Sealed)
            return true;

        return false;
    }
}
