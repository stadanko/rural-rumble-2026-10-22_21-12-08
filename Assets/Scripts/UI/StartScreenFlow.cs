using System;
using RuralRumble.Core;

namespace RuralRumble.UI
{
    /// <summary>Decides which scene the Start screen loads next.</summary>
    public static class StartScreenFlow
    {
        /// <summary>CharacterSelect if it is in Build Settings, otherwise Match.</summary>
        public static SceneId NextScene(Func<string, bool> isSceneInBuild)
        {
            if (isSceneInBuild == null) throw new ArgumentNullException(nameof(isSceneInBuild));
            return isSceneInBuild(SceneNames.CharacterSelect) ? SceneId.CharacterSelect : SceneId.Match;
        }
    }
}
