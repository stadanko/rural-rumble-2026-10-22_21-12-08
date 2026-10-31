using RuralRumble.Core;

namespace RuralRumble.Combat
{
    /// <summary>An input source that never moves or presses anything. Stand-in opponent until the CPU brain exists.</summary>
    public sealed class IdleInput : IWrestlerInput
    {
        public static readonly IdleInput Instance = new IdleInput();

        public WrestlerIntent Read() => WrestlerIntent.None;
    }
}
