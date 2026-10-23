using BooTrayal.Core.Night;
using BooTrayal.Game.Night;
using UnityEngine;

namespace BooTrayal.Game.Audio
{
    /// <summary>
    /// Non-positional game sounds (GDD 12): the night ambience while you are in the House, the shrine bell when a meeting
    /// starts, and UI clicks/hovers. Fear layers live in FearDirector; voices in VoiceService.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        const float AmbienceVolume = 0.35f;
        const float UiVolume = 0.5f;

        public static AudioDirector Instance { get; private set; }

        AudioSource _ambience;
        AudioSource _oneShots;
        MeetingPhase _lastMeeting;

        void Awake()
        {
            Instance = this;
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.clip = ProceduralClips.Ambience();
            _ambience.loop = true;
            _ambience.playOnAwake = false;
            _ambience.spatialBlend = 0f;
            _ambience.volume = 0f;
            _oneShots = gameObject.AddComponent<AudioSource>();
            _oneShots.playOnAwake = false;
            _oneShots.spatialBlend = 0f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var d = NightDirector.Instance;
            bool inHouse = d != null && d.IsSpawned;
            float target = inHouse ? AmbienceVolume : 0f;
            _ambience.volume = Mathf.MoveTowards(_ambience.volume, target, 0.5f * Time.unscaledDeltaTime);
            if (_ambience.volume > 0f && !_ambience.isPlaying) _ambience.Play();
            else if (_ambience.volume <= 0f && _ambience.isPlaying) _ambience.Stop();

            var meeting = inHouse ? d.MeetPhase.Value : MeetingPhase.None;
            if (meeting == MeetingPhase.Discuss && _lastMeeting != MeetingPhase.Discuss) PlayBell();
            _lastMeeting = meeting;
        }

        public void PlayBell()
        {
            _oneShots.PlayOneShot(ProceduralClips.Bell(), 0.9f);
            if (Application.isBatchMode) Debug.Log("AUDIO_BELL");
        }

        /// <summary>UI feedback: a click on press, a softer tick on hover.</summary>
        public static void PlayUi(bool hover)
        {
            if (Instance == null) return;
            Instance._oneShots.PlayOneShot(hover ? ProceduralClips.UiHover() : ProceduralClips.UiClick(), UiVolume);
        }
    }
}
