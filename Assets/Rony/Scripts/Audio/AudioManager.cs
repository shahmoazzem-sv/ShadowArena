using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Audio Tracks")]
    [SerializeField] private List<AudioClip> playlist;

    [Header("Chess SFX")]
    public AudioClip ClickNormal;
    public AudioClip PieceClick;
    public AudioClip PieceMove;
    public AudioClip PieceCapture;
    public AudioClip PieceCapturedByAI;
    public AudioClip KingCheck;
    public AudioClip Checkmate;
    public AudioClip GameOver;

    private AudioSource musicSource;
    private AudioSource sfxSource;

    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    public float MusicVolume { get; private set; } = 0.5f;
    public float SFXVolume { get; private set; } = 0.5f;

    private int lastPlayedIndex = -1;

    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            LoadVolumeSettings();
            SetupSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetupSources()
    {
        // Setup Music Source
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.outputAudioMixerGroup = musicGroup;
        musicSource.loop = false; // We handle looping manually to pick new random tracks
        musicSource.volume = MusicVolume;

        // Setup SFX Source
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.outputAudioMixerGroup = sfxGroup;
        sfxSource.volume = SFXVolume;
    }

    private void Update()
    {
        // Play next random song if nothing is playing
        if (!musicSource.isPlaying && playlist.Count > 0)
        {
            PlayRandomMusic();
        }
    }

    public void PlayRandomMusic()
    {
        if (playlist.Count == 0) return;

        int newIndex = Random.Range(0, playlist.Count);
        
        // Avoid playing the same song twice in a row if there's more than one
        if (playlist.Count > 1 && newIndex == lastPlayedIndex)
        {
            newIndex = (newIndex + 1) % playlist.Count;
        }

        lastPlayedIndex = newIndex;
        AudioClip clip = playlist[newIndex];
        musicSource.clip = clip;
        musicSource.loop = false; // reset in case we came back from GameOver status
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
    {
        if (clip == null) return;
        // PlayOneShot uses the current Volume of the AudioSource * volumeMultiplier
        sfxSource.PlayOneShot(clip, volumeMultiplier);
    }

    // ──────────────────────────────────────────────
    // Volume Management (PlayerPrefs)
    // ──────────────────────────────────────────────

    private void LoadVolumeSettings()
    {
        MusicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.5f);
        SFXVolume   = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 0.5f);
    }

    public void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        if (musicSource != null) musicSource.volume = MusicVolume;
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, MusicVolume);
        
        // If using AudioMixer, you'd typically do: 
        // musicGroup.audioMixer.SetFloat("MusicVol", Mathf.Log10(MusicVolume) * 20);
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = SFXVolume;
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, SFXVolume);
    }

    public float GetMusicVolume() => MusicVolume;
    public float GetSFXVolume()   => SFXVolume;

    // ──────────────────────────────────────────────
    // Game Over: stop music, play game-over SFX
    // ──────────────────────────────────────────────
    public void PlayGameOverSound()
    {
        // Stop SFX
        if (sfxSource != null) sfxSource.Stop();

        // Stop background music and play game over as looping music
        if (musicSource != null) 
        {
            musicSource.Stop();
            musicSource.clip = GameOver;
            musicSource.loop = true;
            musicSource.Play();
        }
    }

    public void ResumeMusic()
    {
        if (musicSource != null && !musicSource.isPlaying && playlist.Count > 0)
            PlayRandomMusic();
    }


}
