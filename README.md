# CarryCapacity

.. is a [*Vintage Story*][VS] mod which allows picking up blocks, especially containers along with their contents, to carry them in your hands and on your back. Inspired by my Minecraft mod [*Wearable Backpacks*][WBs] as well as *Charset*, *CarryOn* and similar.

![Screenshot](docs/screenshot.jpg)

[VS]: https://www.vintagestory.at/
[WBs]: https://github.com/copygirl/WearableBackpacks

## Download / Installation

Available right here through [GitHub Releases][DL].

To install, start the game and go to the *Mod Manager* in the main menu. There you can find a button labeled *Open Mod Folder* that will open said folder in your file explorer. Simply copy the `.zip` file you downloaded into here **without extracting it**.

[DL]: https://github.com/copygirl/CarryCapacity/releases

## Usage

The only control in this mod is **sneaking** and **holding right click**, while not having an item selected in your hotbar, **nor your offhand slot**. If you're doing things right, you will see a circle that fills up over time.

- You can pick up supported blocks.
- While carrying something in-hand, place it back down against a block.
- Or put what you're carrying on your back by instead not aiming at a block.
- Grab what's on your back into your hands again.

Carrying something in-hand prevents you from using items and sprinting. Whether in your hands or on your back, you may be slowed down in general. The exact amount depends on the type of block, though. Additionally, placing takes a little less time (-25%) than picking up, and interacting with your back is quite a bit slower (+50%).

For the curious, the values in the following table show example walk speed modifiers (hands / back) and time to interact (pickup / place / switch) for blocks that you can carry around. Ones with a "~~back~~" entry can't be carried on your back.

| Block                   | Weight  |   Walk speed   | Time to interact  |
| ----------------------- |:-------:|:--------------:|:-----------------:|
| Basket                  |  Light  | 85% /   100%   | 0.4 / 0.3 / 0.6 s |
| Chest / Barrel / Vessel |  Heavy  | 75% /    85%   | 0.8 / 0.6 / 1.2 s |
| Crate / Planter         |  Heavy  | 75% / ~~back~~ | 0.8 / 0.6 /  —  s |
| Anvil                   | Massive | 50% / ~~back~~ | 1.5 / 1.0 /  —  s |

## Modding

The block behavior `Carryable` is [retroactively added][patch] to Vanilla blocks. It is also possible to add it to additional ones. Simply add it to the behavior list like in the following example, or use the patching system to modify existing blocks like I do.

[patch]: ./resources/assets/carrycapacity/patches/carryable.json

```json
behaviors: [
  { name: "Container" },
  ...
  { name: "Carryable", properties: {
    interactDelay: 0.8,
    
    translation: [ 0, 0, 0 ],
    rotation: [ 0, 0, 0 ],
    origin: [ 0.5, 0.5, 0.5 ],
    scale: [ 0.5, 0.5, 0.5 ],
    
    slots: {
      "Hands": {
        animation: "carrycapacity:holdheavy",
        walkSpeedModifier: 0.75,
        
        translation: [ 0, 0, 0 ],
        rotation: [ 0, 0, 0 ],
        origin: [ 0.5, 0.5, 0.5 ],
        scale: [ 0.5, 0.5, 0.5 ],
      },
    }
  } },
]
```

The properties and each of the entries are optional, reverting to the defaults shown here if not present. Note that if you don't include a `"Back"` slot, the block will also default to not be carryable on the back.

CarryCapacity patches the player entity and seraph shape to provide two animations `"carrycapacity:holdheavy"` and `"carrycapacity:holdlight"` made specifically for in-hand carrying. If the block is light, consider using the light variant instead of the default heavy one.
