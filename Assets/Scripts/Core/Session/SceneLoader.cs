using UnityEngine.SceneManagement;

namespace RuralRumble.Core
{
    public enum SceneId
    {
        Start,
        CharacterSelect,
        Match,
        Victory
    }

    /// <summary>The only place scene names appear (ARCHITECTURE 3).</summary>
    public static class SceneNames
    {
        public const string Start = "Start";
        public const string CharacterSelect = "CharacterSelect";
        public const string Match = "Match";
        public const string Victory = "Victory";

        public static string For(SceneId id)
        {
            switch (id)
            {
                case SceneId.Start: return Start;
                case SceneId.CharacterSelect: return CharacterSelect;
                case SceneId.Match: return Match;
                case SceneId.Victory: return Victory;
                default: throw new System.ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }
    }

    /// <summary>One scene per screen, loaded singly (D-021).</summary>
    public static class SceneLoader
    {
        public static void Load(SceneId id) => SceneManager.LoadScene(SceneNames.For(id), LoadSceneMode.Single);
    }
}
