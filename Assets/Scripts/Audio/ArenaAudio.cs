using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Listens to player and game events and plays procedural sounds. Attached automatically by ClashDashBootstrap.
    /// </summary>
    public class ArenaAudio : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.22f;
        [SerializeField, Range(0f, 0.15f)] private float pitchVariation = 0.04f;

        private AudioSource sfxSource;
        private AudioSource ambientSource;
        private GameManager gm;
        private PlayerController player;

        private UnityAction onJump;
        private UnityAction onLand;
        private UnityAction onHit;
        private UnityAction onRespawn;
        private UnityAction onCountdown;
        private UnityAction onRunStarted;
        private UnityAction onCheckpoint;
        private UnityAction onFinished;

        private bool counting;
        private float beepTimer;
        private float ambientTarget;

        public void Bind(GameManager manager, PlayerController playerController)
        {
            gm = manager;
            player = playerController;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.spatialBlend = 0f;
            ambientSource.loop = true;
            ambientSource.clip = ProceduralAudio.GetAmbientLoop();
            ambientTarget = ambientVolume;
            ambientSource.volume = 0f;
            ambientSource.Play();

            onJump = () => Play(Sfx.Jump, 0.6f);
            onLand = () => Play(Sfx.Land, 0.7f);
            onHit = () => Play(Sfx.Hit, 1f);
            onRespawn = () => Play(Sfx.Fall, 0.7f);
            onCountdown = () => { counting = true; beepTimer = 0f; };
            onRunStarted = () => { counting = false; Play(Sfx.Go, 0.9f); };
            onCheckpoint = () => Play(Sfx.Checkpoint, 0.8f);
            onFinished = () => { Play(Sfx.Finish, 1f); ambientTarget = ambientVolume * 0.35f; };

            if (player != null)
            {
                player.onJump.AddListener(onJump);
                player.onLand.AddListener(onLand);
                player.onHit.AddListener(onHit);
                player.onRespawn.AddListener(onRespawn);
            }

            if (gm != null)
            {
                gm.onCountdownStarted.AddListener(onCountdown);
                gm.onRunStarted.AddListener(onRunStarted);
                gm.onCheckpointReached.AddListener(onCheckpoint);
                gm.onFinished.AddListener(onFinished);
            }
        }

        private void Update()
        {
            if (ambientSource != null)
            {
                ambientSource.volume = Mathf.MoveTowards(ambientSource.volume, ambientTarget, Time.unscaledDeltaTime * 0.15f);
            }

            if (counting)
            {
                if (gm == null || gm.State != GameState.Countdown)
                {
                    counting = false;
                    return;
                }

                beepTimer -= Time.deltaTime;
                if (beepTimer <= 0f)
                {
                    Play(Sfx.Beep, 0.7f);
                    beepTimer += 1f;
                }
            }
        }

        private void Play(Sfx id, float scale)
        {
            if (sfxSource == null) return;
            sfxSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            sfxSource.PlayOneShot(ProceduralAudio.Get(id), sfxVolume * scale);
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.onJump.RemoveListener(onJump);
                player.onLand.RemoveListener(onLand);
                player.onHit.RemoveListener(onHit);
                player.onRespawn.RemoveListener(onRespawn);
            }

            if (gm != null)
            {
                gm.onCountdownStarted.RemoveListener(onCountdown);
                gm.onRunStarted.RemoveListener(onRunStarted);
                gm.onCheckpointReached.RemoveListener(onCheckpoint);
                gm.onFinished.RemoveListener(onFinished);
            }
        }
    }
}
