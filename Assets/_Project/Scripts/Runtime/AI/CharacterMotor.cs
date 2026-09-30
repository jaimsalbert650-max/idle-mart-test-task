using UnityEngine;
using UnityEngine.AI;

namespace IdleMart.AI
{
    /// <summary>Moves a character with a NavMeshAgent and drives the walk animation.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private CharacterAnimator characterAnimator;
        [SerializeField] private float turnSpeed = 540f;

        private NavMeshAgent _agent;
        private Quaternion? _faceTarget;

        public CharacterAnimator Animator => characterAnimator;

        public bool HasArrived =>
            !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.05f;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<CharacterAnimator>();
        }

        public void MoveTo(Vector3 position)
        {
            _faceTarget = null;
            _agent.isStopped = false;
            _agent.SetDestination(position);
        }

        /// <summary>Turns smoothly towards <paramref name="rotation"/> once standing still.</summary>
        public void Face(Quaternion rotation) => _faceTarget = rotation;

        public void FacePoint(Vector3 point)
        {
            var dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) Face(Quaternion.LookRotation(dir));
        }

        private void Update()
        {
            characterAnimator.SetSpeed(_agent.velocity.magnitude);

            if (_faceTarget.HasValue && HasArrived)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _faceTarget.Value, turnSpeed * Time.deltaTime);
        }
    }
}
