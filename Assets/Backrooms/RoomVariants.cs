using UnityEngine;

// Room interiors, rebuilt from the 2022 prefabs. Each is a set of free-standing boxes inside a 4x4 m cell,
// as (centre x, centre z, half width x, half depth z) relative to the cell centre.
// Rules that keep every maze walkable: nothing within 0.5 m of the centre (spawn + exit pillar),
// and at least a 0.8 m gap wherever a box doesn't touch the cell edge.
public static class RoomVariants
{
    public static readonly string[] Names = { "Empty", "Multisections", "Hallway Left", "Hallway Right",
                                              "Pillar Grid", "Lone Slab", "Doorway", "Zig-Zag", "Corridor" };
    public const int Corridor = 8; // only used on straight runs, see BackroomsGame.BuildMesh

    public static readonly Vector4[][] Boxes =
    {
        new Vector4[0],                                                       // 0 Room (Empty)
        new[] {                                                               // 1 MultisectionsRoom: partition stubs off two walls
            new Vector4(-1.45f, 0, 0.55f, 0.07f), new Vector4(1.45f, 0, 0.55f, 0.07f) },
        new[] {                                                               // 2 HallwayLeftIntersection: L-shaped inner wall
            new Vector4(-1.1f, 0.2f, 0.07f, 0.9f), new Vector4(-0.55f, 1.1f, 0.55f, 0.07f) },
        new[] {                                                               // 3 HallwayRightRoom: mirrored L
            new Vector4(1.1f, 0.2f, 0.07f, 0.9f), new Vector4(0.55f, 1.1f, 0.55f, 0.07f) },
        // New ones, after the original footage:
        new[] {                                                               // 4 Pillar Grid: four thin pillars
            new Vector4(-1, -1, 0.2f, 0.2f), new Vector4(1, -1, 0.2f, 0.2f), new Vector4(-1, 1, 0.2f, 0.2f), new Vector4(1, 1, 0.2f, 0.2f) },
        new[] { new Vector4(0, 0.9f, 0.9f, 0.1f) },                           // 5 Lone Slab: free-standing wall, short enough to walk around in a dead end
        new[] { new Vector4(-0.6f, 1, 1.4f, 0.1f) },                          // 6 Doorway: wall across with a 1.2 m gap at one end
        new[] {                                                               // 7 Zig-Zag: offset partitions you weave through
            new Vector4(-0.8f, -0.9f, 1.2f, 0.1f), new Vector4(0.8f, 0.9f, 1.2f, 0.1f) },
        new[] {                                                               // 8 Corridor: narrows a straight run to a long hallway
            new Vector4(-1.0f, 0, 0.1f, 2), new Vector4(1.0f, 0, 0.1f, 2) },
    };

    // Rotate a box by k quarter turns around the cell centre.
    public static Vector4 Rotate(Vector4 b, int k)
    {
        for (int i = 0; i < k; i++) b = new Vector4(-b.y, b.x, b.w, b.z);
        return b;
    }
}
