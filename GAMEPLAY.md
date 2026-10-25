# Growth prototype

The goal is to grow as large as possible. No new art, animation, or audio was added.

## Playing

- Move with WASD, arrow keys, or the left gamepad stick.
- Sprint with either Shift key or the gamepad right shoulder.
- R restarts; gamepad south button restarts after game over.
- Eating grows the pig and resets the hunger grace period. Hunger then shrinks the pig until it eats again or reaches the minimum size.
- Exhausted sprint recovers once stamina reaches 35%, after the regeneration delay.

## Growth and camera

Select `pigMainCharacter` in SampleScene. Its Player Animation Controller has editable **Stage Thresholds**, initially 2, 4, 8, 16, and 32. Each milestone increases the camera's target view by **Stage Zoom Multiplier**, initially 1.65. Zoom and camera following ease over **Camera Smooth Time**. Reached stages stay unlocked when hungry. Movement speed increases with the square root of the stage's world scale.

The existing stamina slider and game-over object remain connected. The prototype HUD overlay and pause controls have been removed.

## Spawning and future food

Select `HumanSpanwer` in SampleScene. Spawns run once per second, independently of frame rate, with one additional human per reached stage and a cap of 30 living spawned objects. Camera bounds determine the spawn region. Padding and player/pickup clearances reduce edge spawns and overlaps. Placement makes at most 16 attempts, then waits until the next interval.

Each stage mixes its food tier and the preceding tier. The existing human prefab is scaled up for later tiers and grants proportionally more growth. It expires after 10 seconds. Old food stays in the world until eaten or expired; zooming out reveals a larger playable area without requiring environment assets.

Under **Food Prefabs**, drag a prefab into each separate **Stage 1 Prefab** through **Stage 6 Prefab** input. Stage 1 is the starting stage; stages 2–6 unlock at sizes 2, 4, 8, 16, and 32 with the default thresholds. Empty inputs use the **Human** fallback, or Stage 1 when no fallback is assigned. Previous assignments from the old prefab array are preserved. Each stage still mixes its own food with the preceding stage's food.

Give replacement prefabs a root SignalTrigger and a 2D collider, with at least one Rigidbody2D in the interacting pair. Prefabs should use a consistent base scale because the spawner applies the tier multiplier. The spawner sets nutrition from the tier and allows all spawned food to be eaten, including after hunger shrinks the pig. Hand-placed SignalTrigger food can instead set **Required Player Size** and **Nutrition** directly; lifetime zero makes it permanent.

Script files were renamed to match their MonoBehaviour classes, keeping their original .meta GUIDs so scene and prefab references survive.

## Verification

The four gameplay scripts compile against this project's installed Unity assemblies. Scene/prefab GUID and milestone configuration checks were also performed. Movement feel, gamepad input, zoom framing, and balance still need a Unity Play Mode pass.
