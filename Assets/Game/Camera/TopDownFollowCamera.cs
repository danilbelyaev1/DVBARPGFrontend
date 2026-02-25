using UnityEngine;

namespace DVBARPG.Game.Camera
{
    public sealed class TopDownFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -6f);
        [SerializeField] private float followSpeed = 12f;

        private void LateUpdate()
        {
            if (target == null) return;

            var desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
            transform.LookAt(target.position, Vector3.up);
        }
    }
}
