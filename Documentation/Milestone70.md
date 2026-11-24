# Milestone 70 — Battlefield and online baseline

Commit `b8ed7c2` collects the M63–M70 work. Its existing milestone documentation covers [team pings](TeamPings.md), [gameplay UX](GameplayUx.md), [onboarding](FirstTimeOnboarding.md), [online matchmaking](OnlineMatchmaking.md), the [player profile](PlayerProfile.md), and the [demo candidate](DemoReleaseChecklist.md). The [Battlefield balance](BattlefieldBalance.md) notes retain the earlier simulation counts and their limits.

## Potions and quick items

- Open the inventory with **I**. Select an item, choose a quick slot with **[** or **]**, and assign the item with **F6** or the inventory button. During gameplay, **[** and **]** cycle slots and **G** uses the selected item.
- **F1** through **F5** directly request health, stamina, mana, defense and damage potions, in that order. The server validates the owner, inventory revision, resource or buff state, and cooldowns before it applies the effect and consumes one potion.
- Health, stamina, and mana potions restore resources. Defense and damage potions apply timed modifiers. The same quick-slot path later gained [deployable placement](Milestone72.md).

Sources include [inventory inputs](../Assets/Game/UI/InventoryEquipmentPresenter.cs), [server potion handling](../Assets/Game/Inventory/NetworkInventory.Consumables.cs), and [quick-slot state](../Assets/Game/Inventory/QuickItemSlots.cs).

## Verification

The [demo candidate records](DemoReleaseChecklist.md#candidate-verification) include the historical build, tutorial, reconnect, and two-round results. For this checkout, run the [inventory UI](../Tests/Standalone/RunInventoryUi.ps1), [online matchmaking](../Tests/Standalone/RunOnlineMatchmaking.ps1), and [HUD and respawn](../Tests/Standalone/RunGameplayHudRespawn.ps1) harnesses with the current Development player. In a manual pass, check full-resource rejection, potion cooldowns, stack consumption, and the HUD's selected slot before using M70 as the comparison baseline.
