namespace KaiForest
{
    /// <summary>
    /// How well the boy can hear right now, set every frame by PlayerStamina and read by everything
    /// that makes "surrounding" sound (voices, ambience, the monster's steps and breath).
    /// WebGL has no realtime audio filters, so this is plain volume plus a push toward the
    /// pre-muffled "far" voice takes.
    /// </summary>
    public static class Hearing
    {
        /// <summary>Multiplier on surrounding sounds (1 = normal).</summary>
        public static float Volume = 1f;

        /// <summary>0..1: how far voices are pushed toward their muffled far take.</summary>
        public static float Muffle = 0f;

        public static void Reset()
        {
            Volume = 1f;
            Muffle = 0f;
        }
    }
}
