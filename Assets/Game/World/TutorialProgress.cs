using UnityEngine;
using UnityEngine.SceneManagement;
using HeroesOfDragonland.Profiles;

namespace HeroesOfDragonland.World
{
    public static class TutorialProgress
    {
        public const string SceneName = "TrainingGrounds";
        public const string ScenePath = "Assets/Scenes/TrainingGrounds.unity";
        public const string BattlefieldPath = "Assets/Scenes/DragonlandBattlefield.unity";
        public static bool TutorialCompleted => LocalPlayerProfile.Current.Data.TutorialCompleted;
        public static bool RecommendTutorial => !TutorialCompleted && !LocalPlayerProfile.Current.Data.TutorialSkipped;
        public static void Complete()
        {
            LocalPlayerProfile.Current.Data.TutorialCompleted = true;
            LocalPlayerProfile.Current.Save();
        }
        public static void Skip()
        {
            LocalPlayerProfile.Current.Data.TutorialSkipped = true;
            LocalPlayerProfile.Current.Save();
        }
        public static void Open() => SceneManager.LoadScene(ScenePath);
        public static void ReturnToMenu() => SceneManager.LoadScene(BattlefieldPath);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OpenAcceptanceScene()
        {
            if (SceneManager.GetActiveScene().name != SceneName
                && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hodrTutorialCheck") >= 0
                && Application.CanStreamedLevelBeLoaded(ScenePath)) Open();
        }
#endif
    }
}
