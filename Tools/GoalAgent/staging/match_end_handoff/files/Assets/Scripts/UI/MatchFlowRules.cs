using System;
using RuralRumble.Core;

namespace RuralRumble.UI
{
    /// <summary>Decides which scene the Match scene loads after a knockout.</summary>
    public static class MatchFlowRules
    {
        /// <summary>Victory if it is in Build Settings, otherwise Start (designer decision, Assignment 5).</summary>
        public static SceneId NextScene(Func<string, bool> isSceneInBuild)
        {
            if (isSceneInBuild == null) throw new ArgumentNullException(nameof(isSceneInBuild));
            return isSceneInBuild(SceneNames.Victory) ? SceneId.Victory : SceneId.Start;
        }
    }
}
