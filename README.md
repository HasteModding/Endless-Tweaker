Find the mod on Steam Workshop at: https://steamcommunity.com/sharedfiles/filedetails/?id=3460131825

This mod aims to make the Endless Mode as customizable as possible, adding control over how frequently you get items, but even control over the fragments you find!

If Bosses are enabled, you must defeat each tier of a boss before you can encounter the next tier. Tiers are randomly determined from what you have unlocked that run.

Settings are defaulted to what is most in-line with vanilla Endless, descriptions are listed below:

[Item Settings]
- Allow Items in Endless:
  - Controls whether or not you'll get items at all from the initial fragment and from the recurring frequency after.
  - Default: Enabled
- Give Item after first Fragment:
  - Controls whether you get an item from completing the first fragment. Has no effect if "Allow Items in Endless" is disabled.
  - Default: Enabled
- Item Frequency:
  - The number of fragments you must complete to gain an item.
  - Default: 5
- Maximum Items:
  - After you have this many items, you will no longer gain items.
  - If set to 0, you will not gain an item from the first fragment.
  - If set below 0, it is treated as infinite.
  - Default: -1
- Reward Items to Choose From:
  - The number of items the reward screen will offer to choose from.
  - If set over 9, you may not be able to see all of them.
  - Behavior is untested and unsupported for values less than 1.
  - Default: 3
- Give Item on Challenge Complete:
  - Similar to running a shard normally, if enabled this will give an extra item reward after completing a challenge fragment.
  - This will stack with "Item Frequency" items to give two items after a fragment if it lines up that way.
  - This will override the "Items Enabled" setting and will allow you to get items from challenge fragments.
  - This will not override the "Maximum Items" setting.
  - Default: Enabled
- Give Item on Boss Complete
  - If enabled, this will give an extra item reward after completing a Boss room.
  - This will stack with "Item Frequency" items to give two items after a boss if it lines up that way.
  - This will override the "Items Enabled" setting and will allow you to get items from boss rooms.
  - This will not override the "Maximum Items" setting.
  - Default: Enabled

[Fragment Settings]
- Normal Chance Weight:
  - Weighted chance for each fragment to be a standard run.
  - Default: 100
- Challenge Chance Weight:
  - Weighted chance for each fragment to be a challenge run.
  - Default: 0
- Should Boss stages be chance or interval?
  - Toggle between having boss rooms on consistent intervals or having a random chance like the other room types.
  - Default: Chance
- Boss Weight/Interval:
  - Chooses the Weight or Interval of Boss rooms.
  - As implied, if Boss stages are set to Chance, it'll set a weight like any other room has, otherwise it'll set the interval that you get boss stages.
  - Default: 0
- Shop Chance Weight:
  - Weighted chance for each fragment to be a shop.
  - Does not count towards fragments completed for "Item Frequency".
  - Does not respect the "Items Enabled" setting.
  - Default: 0
- Rest Chance Weight:
  - Weighted chance for each fragment to be a rest site.
  - Does not count towards fragments completed for "Item Frequency".
  - Default: 0

[Boss Settings]
- Minimum Floors before Bosses
  - This many fragments must be completed before bosses can be found.
  - This overrides both Interval and Chance based Boss rooms.
  - Default: 5
- Jumper Boss Weight:
  - Weighted chance for each Boss room to be Jumper.
  - Default: 10
- Convoy Boss Weight:
  - Weighted chance for each Boss room to be Convoy.
  - Default: 10
- Snake Boss Weight: 
  - Weighted chance for each Boss room to be Snake.
  - Default: 10

[Healing Settings]
- Heal Each Stage:
  - Heals you this amount between each fragment.
  - Must be greater than 0 to have an effect.
  - Default: 25
- Life Regen Frequency:
  - The number of fragments you must complete to regain a life.
  - Default: 3

This mod overrides the "OnLevelCompleted" functionality within Endless Mode, the "EndlessAward" functionality, and the "WinRun" functionality within Endless.
As such, it will very likely be incompatible with any other mod that also overrides those functionalities.