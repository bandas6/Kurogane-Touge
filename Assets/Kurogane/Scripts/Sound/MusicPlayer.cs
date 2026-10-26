using UnityEngine;

namespace Kurogane.Sound
{
    /// <summary>
    /// Reproduce la lista de música en orden, empezando por una pista al azar.
    /// El juego no trae música: las pistas (con licencia) van en Assets/Kurogane/Audio/Musica y las asigna
    /// el menú "Kurogane > Crear escena de prueba".
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        public AudioClip[] tracks;
        [Range(0, 1)] public float volume = 0.6f;

        AudioSource src;
        int index = -1;

        void Awake()
        {
            src = GetComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0;
            if (tracks != null && tracks.Length > 0) index = Random.Range(0, tracks.Length) - 1;
        }

        void Update()
        {
            src.volume = volume;
            if (tracks == null || tracks.Length == 0 || volume <= 0) return;
            if (!src.isPlaying && Application.isFocused) Next();
        }

        public void Next()
        {
            if (tracks == null || tracks.Length == 0) return;
            index = (index + 1) % tracks.Length;
            src.clip = tracks[index];
            src.Play();
        }
    }
}
