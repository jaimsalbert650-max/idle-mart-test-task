using UnityEngine;

namespace IdleMart.AI
{
    /// <summary>Thin wrapper over the shared character Animator Controller (Kenney mini characters).</summary>
    public sealed class CharacterAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int PickUpId = Animator.StringToHash("PickUp");
        private static readonly int InteractId = Animator.StringToHash("Interact");
        private static readonly int YesId = Animator.StringToHash("Yes");
        private static readonly int NoId = Animator.StringToHash("No");

        [SerializeField] private Animator animator;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        public void SetSpeed(float speed) => animator.SetFloat(SpeedId, speed);
        public void PlayPickUp() => animator.SetTrigger(PickUpId);
        public void PlayInteract() => animator.SetTrigger(InteractId);
        public void PlayYes() => animator.SetTrigger(YesId);
        public void PlayNo() => animator.SetTrigger(NoId);
    }
}
