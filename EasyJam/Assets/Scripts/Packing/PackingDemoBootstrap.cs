using UnityEngine;

namespace TheLastGift.Packing
{
    /// <summary>Optional empty helper: add this component to an object with PackingBoardController to run the default demo.</summary>
    [DisallowMultipleComponent]
    public sealed class PackingDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private PackingBoardController board;
        private void Reset() => board = GetComponent<PackingBoardController>();
        private void Start()
        {
            if (board == null) board = GetComponent<PackingBoardController>();
            if (board == null) Debug.LogWarning("PackingDemoBootstrap requires a PackingBoardController.", this);
        }
    }
}
