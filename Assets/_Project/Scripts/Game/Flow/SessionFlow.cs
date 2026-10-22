using BooTrayal.Game.Networking;
using BooTrayal.Game.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BooTrayal.Game.Flow
{
    /// <summary>
    /// Scene flow: Bootstrap → Menu on start; host loads the play scene when the room opens;
    /// everyone returns to Menu when the connection ends. Launch-arg auto host/join runs once Menu is up.
    /// </summary>
    public class SessionFlow : MonoBehaviour
    {
        public const string MenuScene = "Menu";
        public const string PlayScene = "Sandbox";

        ConnectionService _svc;
        NetStatus _last = NetStatus.Offline;
        bool _autoStarted;

        void Start()
        {
            _svc = GetComponent<ConnectionService>();
            _svc.Changed += OnStatusChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name != MenuScene) SceneManager.LoadScene(MenuScene);
            else OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_svc != null) _svc.Changed -= OnStatusChanged;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != MenuScene || _autoStarted) return;
            _autoStarted = true;
            _svc.RunLaunchArgs();
        }

        void OnStatusChanged()
        {
            var status = _svc.Status;
            if (status == _last) return;
            _last = status;

            var net = NetworkManager.Singleton;
            if (status == NetStatus.Hosting && net != null && net.IsServer)
            {
                net.SceneManager.LoadScene(PlayScene, LoadSceneMode.Single);
            }
            else if (status == NetStatus.Offline && SceneManager.GetActiveScene().name != MenuScene)
            {
                PlayerInputReader.SetCursorFree(true);
                SceneManager.LoadScene(MenuScene);
            }
        }
    }
}
