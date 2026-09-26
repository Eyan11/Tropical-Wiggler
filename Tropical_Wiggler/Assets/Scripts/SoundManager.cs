using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [SerializeField] private AudioClip[] bodySFXClips = new AudioClip[14];
    [SerializeField] [Range(0f, 1f)] private float bodySFXVolume = 1.0f; // Volume for body SFX
    [SerializeField] private AudioClip[] oneShotSFXClipsArray; // Array to hold one-shot SFX clips
    private AudioSource bodySFXSource1;
    private AudioSource bodySFXSource2;
    private AudioSource oneShotSFXSource;
    private bool useFirstSource = true;
    public static SoundManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        oneShotSFXSource = gameObject.AddComponent<AudioSource>();

        bodySFXSource1 = gameObject.AddComponent<AudioSource>();
        bodySFXSource1.volume = bodySFXVolume;
        bodySFXSource1.panStereo = -0.25f; // Pan to left ear slightly
        bodySFXSource2 = gameObject.AddComponent<AudioSource>();
        bodySFXSource2.volume = bodySFXVolume;
        bodySFXSource2.panStereo = 0.25f; // Pan to right ear slightly
    }

    // Plays a body sound effect based on the provided body index (0-13)
    public void PlayBodySFX(int index)
    {
        if (index < 0 || index >= bodySFXClips.Length)
        {
            Debug.LogWarning("[SoundManager] Invalid index for body SFX: " + index);
            return;
        }
        
        // Stop the currently playing sound, if any
        if (useFirstSource && bodySFXSource1.isPlaying) bodySFXSource1.Stop();
        if (!useFirstSource && bodySFXSource2.isPlaying) bodySFXSource2.Stop();

        // Assign and play the new sound effect
        if (useFirstSource)
        {
            bodySFXSource1.clip = bodySFXClips[index];
            bodySFXSource1.volume = bodySFXVolume; // Ensure volume is set correctly
            bodySFXSource1.Play();
        }
        else
        {
            bodySFXSource2.clip = bodySFXClips[index];
            bodySFXSource2.volume = bodySFXVolume; // Ensure volume is set correctly
            bodySFXSource2.Play();
        }

        useFirstSource = !useFirstSource; // Switch the source for the next call
    }

    public void PlayOneShotSFX(int sfxIndex, float volume = 1.0f)
    {
        if (sfxIndex < 0 || sfxIndex >= oneShotSFXClipsArray.Length)
        {
            Debug.LogWarning("[SoundManager] Invalid index for one-shot SFX: " + sfxIndex);
            return;
        }

        oneShotSFXSource.PlayOneShot(oneShotSFXClipsArray[sfxIndex], volume);
    }
}
