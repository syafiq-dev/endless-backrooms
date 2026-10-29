using UnityEngine;

// Rare out-of-place furniture, built from boxes so there are no model files.
// Prop space: origin on the floor at the item's centre, y up, front is +z. Metres.
public static class Props
{
    public const float MinOdds = 200, MaxOdds = 300; // each run rolls a chance between 1 in 200 and 1 in 300 rooms
    public const int Wood = 0, Metal = 1, Red = 2, White = 3, Dark = 4; // colours, see the BackroomsSurface shaders in Resources/Presets
    public const int Sign = 256; // the shader writes a glowing red EXIT on this face

    public struct Part
    {
        public Vector3 c, h; // centre, half size
        public float rz;     // spin around its own z (degrees), only the stop sign uses it
        public int mat;
        public Part(float x, float y, float z, float hx, float hy, float hz, int mat, float rz = 0)
        {
            c = new Vector3(x, y, z); h = new Vector3(hx, hy, hz); this.mat = mat; this.rz = rz;
        }
    }

    public static readonly string[] Names = { "Wooden Chair", "Stool", "Small Table", "Filing Cabinet", "Stop Sign" };

    public static readonly Part[][] Items =
    {
        new[] { // wooden chair
            new Part(0, 0.45f, 0, 0.21f, 0.025f, 0.2f, Wood),
            new Part(-0.18f, 0.225f, 0.17f, 0.02f, 0.225f, 0.02f, Wood), new Part(0.18f, 0.225f, 0.17f, 0.02f, 0.225f, 0.02f, Wood),
            new Part(-0.18f, 0.225f, -0.17f, 0.02f, 0.225f, 0.02f, Wood), new Part(0.18f, 0.225f, -0.17f, 0.02f, 0.225f, 0.02f, Wood),
            new Part(-0.18f, 0.72f, -0.18f, 0.02f, 0.25f, 0.02f, Wood), new Part(0.18f, 0.72f, -0.18f, 0.02f, 0.25f, 0.02f, Wood),
            new Part(0, 0.82f, -0.18f, 0.2f, 0.08f, 0.015f, Wood) },
        new[] { // stool
            new Part(0, 0.6f, 0, 0.17f, 0.025f, 0.17f, Wood),
            new Part(-0.13f, 0.3f, 0.13f, 0.02f, 0.3f, 0.02f, Wood), new Part(0.13f, 0.3f, 0.13f, 0.02f, 0.3f, 0.02f, Wood),
            new Part(-0.13f, 0.3f, -0.13f, 0.02f, 0.3f, 0.02f, Wood), new Part(0.13f, 0.3f, -0.13f, 0.02f, 0.3f, 0.02f, Wood),
            new Part(0, 0.22f, 0.13f, 0.13f, 0.012f, 0.012f, Wood), new Part(0, 0.22f, -0.13f, 0.13f, 0.012f, 0.012f, Wood) },
        new[] { // small table
            new Part(0, 0.74f, 0, 0.6f, 0.025f, 0.4f, Wood),
            new Part(-0.55f, 0.37f, 0.35f, 0.03f, 0.37f, 0.03f, Wood), new Part(0.55f, 0.37f, 0.35f, 0.03f, 0.37f, 0.03f, Wood),
            new Part(-0.55f, 0.37f, -0.35f, 0.03f, 0.37f, 0.03f, Wood), new Part(0.55f, 0.37f, -0.35f, 0.03f, 0.37f, 0.03f, Wood) },
        new[] { // filing cabinet: body, drawer gaps, handles
            new Part(0, 0.66f, 0, 0.23f, 0.66f, 0.3f, Metal),
            new Part(0, 0.44f, 0.301f, 0.21f, 0.006f, 0.002f, Dark), new Part(0, 0.88f, 0.301f, 0.21f, 0.006f, 0.002f, Dark),
            new Part(0, 0.3f, 0.31f, 0.06f, 0.012f, 0.012f, Dark), new Part(0, 0.74f, 0.31f, 0.06f, 0.012f, 0.012f, Dark),
            new Part(0, 1.18f, 0.31f, 0.06f, 0.012f, 0.012f, Dark) },
        new[] { // stop sign: pole, white rim, red octagon (four overlapping strips = a regular octagon)
            new Part(0, 1.1f, 0, 0.03f, 1.1f, 0.03f, Metal),
            new Part(0, 2.0f, 0.04f, 0.4f, 0.166f, 0.008f, White, 0), new Part(0, 2.0f, 0.04f, 0.4f, 0.166f, 0.008f, White, 45),
            new Part(0, 2.0f, 0.04f, 0.4f, 0.166f, 0.008f, White, 90), new Part(0, 2.0f, 0.04f, 0.4f, 0.166f, 0.008f, White, 135),
            new Part(0, 2.0f, 0.05f, 0.36f, 0.149f, 0.008f, Red, 0), new Part(0, 2.0f, 0.05f, 0.36f, 0.149f, 0.008f, Red, 45),
            new Part(0, 2.0f, 0.05f, 0.36f, 0.149f, 0.008f, Red, 90), new Part(0, 2.0f, 0.05f, 0.36f, 0.149f, 0.008f, Red, 135) },
    };

    // The exit: a single steel door set into a wall, like a mall service door or a cinema exit, with a lit EXIT
    // sign above. Origin on the floor at the wall face, +z points out into the room. The shader expects the
    // sign plate 2.45 m up and centred on the wall.
    public static readonly Part[] ExitDoor =
    {
        new Part(-0.52f, 1.1f, 0.03f, 0.05f, 1.1f, 0.05f, Dark), new Part(0.52f, 1.1f, 0.03f, 0.05f, 1.1f, 0.05f, Dark), // frame
        new Part(0, 2.15f, 0.03f, 0.57f, 0.05f, 0.05f, Dark),
        new Part(0, 1.05f, 0.015f, 0.47f, 1.05f, 0.015f, Metal), // door
        new Part(0, 1.0f, 0.06f, 0.38f, 0.035f, 0.03f, Dark),    // push bar
        new Part(0, 2.45f, 0.08f, 0.32f, 0.13f, 0.08f, Dark),    // sign box
        new Part(0, 2.45f, 0.165f, 0.29f, 0.1f, 0.005f, Sign),
    };

    // How far each item reaches from its centre on the floor, for spacing (browser version uses it for collisions).
    public static readonly float[] Footprint = { 0.25f, 0.2f, 0.65f, 0.35f, 0.1f };
}
