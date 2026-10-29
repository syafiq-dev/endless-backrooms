# Endless Backrooms (Unity 6, URP, Android)

A procedural backrooms maze you can walk around on your phone. Every run picks a new maze and a random start. The exit is a single steel door with a lit EXIT sign, set into a wall of the room farthest from you. Walk up to it and a new maze is generated.

## What's in `Assets/Backrooms`

| File | What it does |
|---|---|
| `Maze.cs` | Generates the grid (24×24 rooms of 6 m, about a 20 minute walk to the exit; `growPerRun` can make each run bigger), with open halls knocked through for room-size variety, a couple of extra-long straight halls (the first ends in a funnel that narrows and drops its ceiling), and archway halls. Each cell is a "pipe fitting": a 4-bit mask of open sides (dead end, straight, corner, T, cross). Recursive backtracker so everything is reachable, plus a few extra openings so it loops. |
| `BackroomsGame.cs` | Turns the maze into one mesh (floor, ceiling, 40 cm walls on closed sides with caps where they end, the odd freestanding column; + pillars joined by low beams in archway halls; beams every 2 m along long halls), spawns the player, handles the exit and new runs. About 1 room in 100 is a wrong-way room: an exact copy of the room next door, down to its dead lights and stains. A few dead-end doors (plain, no sign, always LOCKED) sit in walls far from the exit. Press N or the big NIGHT button (mid-right) for night vision (infrared lamp + grainy grayscale picture with static, streaks and dropouts like an old tape, no battery; lit rooms blow out to white, so switch it off there). Each run is a new TAPE number. Tweak size, cell size, fog in the Inspector. **Debug Mode** (on right now; hold the bottom middle of the start screen for 3 s to flip it, remembered on the device) shows a green (DEBUG MODE) with the share of rooms explored (slam at 30%, entity at 35%) on the VHS stamp. |
| `PlayerController.cs` | Phone: drag left half to walk, drag right half to look, tap RUN to toggle running. Editor: WASD, hold right mouse to look. Handheld camcorder feel: the camera follows your aim on a soft spring (`lookSpring`/`lookDamp`) and leans into fast turns, plus a slow wobble and step rhythm (`sway`, 0 = off). Footsteps on the carpet, with a squelch on wet patches, generated in code. |
| `Entity.cs` | The entity (switch: `entityOn` on Backrooms Game). At 30% of rooms explored you hear a slam somewhere out of sight; at 35% it appears a few rooms behind you and follows the maze toward you, lit or dark, at 3.4 m/s (walk 3, run 5). Get 6 rooms ahead and it loses you for a while. If it reaches you, TAPE ENDS and a new run starts. A crooked black wire stick man with a smoky black aura (the _EntPos uniform in the surface shaders); its head jerks and hands sway. A low drone swells as it closes in. The closer it gets, the more the tape breaks up (torn lines, a fast tracking band, colour bleed: `_Danger` in the VHS shaders) and crackles. Hiding spots (Props.Hides: a metal locker to stand in, an office desk to crawl under, in ~8% of empty rooms with a wall): step up and press E / HIDE, E / LEAVE to get out. If it didn't see you go in (more than 3.5 m away), it comes to your room, waits 4 s and gives up; if it did, it reaches in. The start screen offers NORMAL (with the entity) or EASY (no entity, just exploring). |
| `AmbientHum.cs` | Fluorescent hum + room tone, generated in code. Swap for real recordings later. |
| `Resources/Presets/ShaderV1..V5/` | Five looks, each a BackroomsSurface (the room: wallpaper, carpet, drop ceiling with fake light panels, soft corner shadows, flicker in rare bad-power zones, the odd 10%-bright dark room, pitch-dark blackouts (2% light, a few faint panels) where you need night vision, rare wet carpet patches, a red glow around the EXIT sign, light that blends softly between open rooms, fog) plus a BackroomsVHS camcorder pass. **V1** greener mono-yellow, **V2** warmer, brighter and hazier like the original footage, **V3** dim, cold and realistic, **V4** amber night shift, **V5** worn, faded tape. Every run picks one at random; press L or the LOOK button to cycle, and the name pops up mid-screen. No real lights, so it stays cheap on mobile. |
| `Resources/backroom_wallpaper.jpg`, `Resources/carpet.jpg` | Wallpaper and carpet textures from your 2022 project (carpet shrunk to 512px for phones). The shader keeps their pattern but uses its own tuned colours. |
| `VhsCamera.cs` | Camcorder look: renders the game at 360 lines, then runs the current look's VHS shader (colour bleed, tracking wobble, rolling noise band, head-switching noise, bloom) and draws the PLAY/date stamp and LOOK button. Works in plain URP, no renderer-feature setup. Turn `lines` up or down on the camera's VHS Camera component. |
| `RoomVariants.cs` | 8 room interiors plus a Corridor (8) used on straight runs to make long narrow hallways: from your 2022 prefabs 0 Empty, 1 Multisections (partition stubs), 2 Hallway Left, 3 Hallway Right; plus 4 Pillar Grid, 5 Lone Slab, 6 Doorway, 7 Zig-Zag. (The cross-pillar Segmented room was dropped.) Half the rooms are empty, open halls always are; the rest pick one at random with a random rotation. |
| `Props.cs` | Rare furniture (each run rolls between 1 in 200 and 1 in 300 rooms): wooden chair, stool, small table, filing cabinet, stop sign, sofa, fridge, granny chair, built from boxes. A quarter come out slightly too big. About half stand normally; the rest are tilted and sunk into the floor, hanging upside down from the ceiling, or half inside a wall. Change `MinOdds`/`MaxOdds` to make them more or less common. |
| Locked runs | Half the runs the exit door is LOCKED. The key is hidden in a key item (office desk, drawer unit or cardboard box, see `Props.KeyItems`) against a wall in the room farthest from both the start and the exit. Walk up to it and press SEARCH (or E) to get the KEY. |
| `Editor/MazeSelfCheck.cs` | Menu **Backrooms > Run Maze Self-Check**: checks 200 mazes are fully connected and tiles line up. |

## Setup (about 5 minutes)

1. In **Unity Hub**, install **Unity 6 LTS** with the **Android Build Support** module (tick OpenJDK and Android SDK & NDK too).
2. **New project** with the **Universal 3D** template.
3. Copy the `Backrooms` folder into the new project's `Assets` folder.
4. **File > New Scene > Empty**. Then **GameObject > Create Empty**, select it, **Add Component > Backrooms Game**. Save the scene.
5. Press **Play**. WASD to walk, hold right mouse to look.
6. For the phone: **File > Build Profiles**, pick **Android**, **Switch Platform**, add the open scene. Turn on USB debugging on the phone, plug it in, **Build And Run**.

If you get errors about `UnityEngine.InputSystem`, go to **Edit > Project Settings > Player > Active Input Handling** and set it to **Input System Package (New)** or **Both**.

## Not built yet

Ads, the remove-ads purchase, menus, and custom room prefabs per tile type. The tile masks in `Maze.cs` are the hook for swapping in hand-made room models later.

## Browser version

`web/backrooms-web.html` is the same maze, mesh and shaders running in WebGL, for testing without Unity. Debug mode there is the `DEBUG` constant at the top of the script. `web/build.py` regenerates it from the Unity look presets, so edit the `.shader` files and rebuild rather than editing the HTML. Each run picks a random look; the LOOK button (or L) cycles ShaderV1-V5. Pushing to GitHub redeploys it on Vercel (`public/index.html`, see `vercel.json`).
