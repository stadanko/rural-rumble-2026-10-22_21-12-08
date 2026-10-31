namespace RuralRumble.Core
{
    /// <summary>
    /// Input source for one wrestler, shared by the player (PlayerInputSource), the CPU
    /// (OpponentBrain) and tests (ScriptedInput). Combat calls Read() exactly once per combat tick.
    /// </summary>
    public interface IWrestlerInput
    {
        WrestlerIntent Read();
    }
}
