using UnityEngine;
using FishNet.Object;
using System.Collections;
using UnityEngine.Events;

namespace zoya.game
{




    [RequireComponent(typeof(Collider))]
    public class AdvancedGoalTrigger : NetworkBehaviour
    {
        [System.Serializable]
        public class GoalSettings
        {
            public int teamId = 1;
            public string teamName = "Blue Team";
            public float goalResetTime = 3f;
            public int pointsPerGoal = 1;
            public bool requireLastTouch = true;
        }

        [System.Serializable]
        public class VisualSettings
        {
            public GameObject goalModel;
            public Material goalMaterial;
            public Material scoredMaterial;
            public ParticleSystem goalEffect;
            public ParticleSystem netEffect;
            public Light goalLight;
            public Color goalColor = Color.blue;
            public Color scoreColor = Color.white;
        }

        [System.Serializable]
        public class AudioSettings
        {
            public AudioClip goalSound;
            public AudioClip saveSound;
            public AudioClip postHitSound;
            public float goalVolume = 1f;
            public float saveVolume = 0.7f;
        }

        [System.Serializable]
        public class GoalEvent : UnityEvent<int, int> { } // scoringTeam, scoredOnTeam

        public GoalSettings settings = new GoalSettings();
        public VisualSettings visual = new VisualSettings();
        public AudioSettings audioSettings = new AudioSettings();
        public GoalEvent OnGoalScored;

        private Collider _goalCollider;
        private AudioSource _audioSource;
        private bool _isGoalActive = true;
        private int _goalsScored = 0;

        private void Awake()
        {
            _goalCollider = GetComponent<Collider>();
            _goalCollider.isTrigger = true;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;

            SetupVisuals();
        }

        private void SetupVisuals()
        {
            if (visual.goalModel != null && visual.goalMaterial != null)
            {
                visual.goalModel.GetComponent<Renderer>().material = visual.goalMaterial;
            }

            if (visual.goalLight != null)
            {
                visual.goalLight.color = visual.goalColor;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_isGoalActive) return;

            AdvancedBall ball = other.GetComponent<AdvancedBall>();
            if (ball != null)
            {
                HandleBallEnter(ball);
            }
        }

        private void HandleBallEnter(AdvancedBall ball)
        {
            if (!IsServerInitialized) return;

            // Check if this is a valid goal
            if (IsValidGoal(ball))
            {
                ScoreGoal(ball);
            }
            else
            {
                PlaySaveEffect();
            }
        }

        private bool IsValidGoal(AdvancedBall ball)
        {
            // Check if ball was last touched by opposite team
            if (settings.requireLastTouch && ball.LastTeamTouch == settings.teamId)
            {
                return false;
            }

            // Check if ball is moving at reasonable speed
            if (ball.CurrentSpeed < 1f)
            {
                return false;
            }

            return true;
        }

        [Server]
        private void ScoreGoal(AdvancedBall ball)
        {
            if (!_isGoalActive) return;

            _isGoalActive = false;
            _goalsScored++;

            // Determine scoring team (opposite of this goal's team)
            int scoringTeam = settings.teamId == 1 ? 2 : 1;

            // Notify game manager
            AdvancedGameManager.Instance.ScoreGoal(scoringTeam, settings.teamId, settings.pointsPerGoal);

            // Play effects
            PlayGoalEffects();

            // Invoke event
            OnGoalScored?.Invoke(scoringTeam, settings.teamId);

            // Reset goal after delay
            StartCoroutine(ResetGoalAfterDelay(settings.goalResetTime));
        }

        private void PlayGoalEffects()
        {
            // Visual effects
            if (visual.goalEffect != null)
            {
                visual.goalEffect.Play();
            }

            if (visual.netEffect != null)
            {
                visual.netEffect.Play();
            }

            if (visual.goalLight != null)
            {
                visual.goalLight.color = visual.scoreColor;
                StartCoroutine(ResetGoalLightAfterDelay(1f));
            }

            // Audio effect
            if (_audioSource != null && audioSettings.goalSound != null)
            {
                _audioSource.PlayOneShot(audioSettings.goalSound, audioSettings.goalVolume);
            }

            // Camera shake for all players
            CameraShakeManager.Instance.ShakeAllCameras(0.5f, 0.7f);
        }

        private void PlaySaveEffect()
        {
            if (_audioSource != null && audioSettings.saveSound != null)
            {
                _audioSource.PlayOneShot(audioSettings.saveSound, audioSettings.saveVolume);
            }

            if (visual.netEffect != null)
            {
                visual.netEffect.Play();
            }
        }

        private IEnumerator ResetGoalAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            _isGoalActive = true;
        }

        private IEnumerator ResetGoalLightAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (visual.goalLight != null)
            {
                visual.goalLight.color = visual.goalColor;
            }
        }

        public void EnableGoal() => _isGoalActive = true;
        public void DisableGoal() => _isGoalActive = false;
        public bool IsGoalActive => _isGoalActive;
        public int GoalsConceded => _goalsScored;

    }
}