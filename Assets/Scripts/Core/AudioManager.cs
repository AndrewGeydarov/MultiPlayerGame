using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PartyMiniGames.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("\u041c\u0443\u0437\u044b\u043a\u0430")]
        public AudioClip menuMusic;
        public AudioClip crocodileMusic;
        public AudioClip towerMusic;
        public AudioClip memoryMusic;

        [Header("\u0417\u0432\u0443\u043a\u0438")]
        public AudioClip toothClickSFX;
        public AudioClip biteSFX;
        public AudioClip blockDropSFX;
        public AudioClip blockCutSFX;
        public AudioClip cardFlipSFX;
        public AudioClip cardMatchSFX;
        public AudioClip winSFX;
        public AudioClip loseSFX;

        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Range(0f, 1f)] public float sfxVolume = 0.7f;
        public float crossfadeDuration = 1f;

        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private AudioSource _sfxSource;
        private bool _usingSourceA = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _musicSourceA = gameObject.AddComponent<AudioSource>();
            _musicSourceA.loop = true;
            _musicSourceA.playOnAwake = false;
            _musicSourceA.volume = musicVolume;

            _musicSourceB = gameObject.AddComponent<AudioSource>();
            _musicSourceB.loop = true;
            _musicSourceB.playOnAwake = false;
            _musicSourceB.volume = 0f;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = sfxVolume;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            AudioClip clip = null;
            switch (scene.name)
            {
                case "MainMenu": clip = menuMusic; break;
                case "Crocodile": clip = crocodileMusic; break;
                case "TowerBuilder": clip = towerMusic; break;
                case "Memory": clip = memoryMusic; break;
            }
            if (clip != null)
                PlayMusic(clip);
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;

            var incoming = _usingSourceA ? _musicSourceB : _musicSourceA;
            var outgoing = _usingSourceA ? _musicSourceA : _musicSourceB;

            incoming.clip = clip;
            incoming.Play();
            _usingSourceA = !_usingSourceA;

            StartCoroutine(Crossfade(outgoing, incoming));
        }

        private IEnumerator Crossfade(AudioSource from, AudioSource to)
        {
            float elapsed = 0f;
            float fromStartVolume = from.volume;
            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / crossfadeDuration;
                from.volume = Mathf.Lerp(fromStartVolume, 0f, t);
                to.volume = Mathf.Lerp(0f, musicVolume, t);
                yield return null;
            }
            from.Stop();
            from.volume = 0f;
            to.volume = musicVolume;
        }

        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayToothClick() => PlaySFX(toothClickSFX);
        public void PlayBite() => PlaySFX(biteSFX);
        public void PlayBlockDrop() => PlaySFX(blockDropSFX);
        public void PlayBlockCut() => PlaySFX(blockCutSFX);
        public void PlayCardFlip() => PlaySFX(cardFlipSFX);
        public void PlayCardMatch() => PlaySFX(cardMatchSFX);
        public void PlayWin() => PlaySFX(winSFX);
        public void PlayLose() => PlaySFX(loseSFX);
    }
}