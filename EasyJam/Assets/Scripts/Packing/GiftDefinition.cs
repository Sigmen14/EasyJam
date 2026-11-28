using UnityEngine;

namespace TheLastGift.Packing
{
    /// <summary>Data asset describing a gift's footprint and handling category in grid cells.</summary>
    [CreateAssetMenu(menuName = "The Last Gift/Packing/Gift Definition", fileName = "GiftDefinition")]
    public class GiftDefinition : ScriptableObject
    {
        [SerializeField] private string giftName = "Gift";
        [SerializeField] private Sprite sprite;
        [SerializeField] private Vector2Int[] cells = { Vector2Int.zero };
        [SerializeField] private bool canRotate = true;
        [SerializeField] private Color tint = new Color(0.9f, 0.45f, 0.3f, 1f);
        [SerializeField] private PackingWeightCategory weightCategory = PackingWeightCategory.Normal;

        public string GiftName => string.IsNullOrWhiteSpace(giftName) ? name : giftName;
        public Sprite Sprite => sprite;
        public Vector2Int[] Cells => cells;
        public bool CanRotate => canRotate;
        public Color Tint => tint;
        public PackingWeightCategory WeightCategory => weightCategory;

        /// <summary>Creates a temporary definition for demos or runtime-driven orders.</summary>
        public static GiftDefinition CreateRuntime(string displayName, Vector2Int[] footprint,
            Color color, bool rotatable = true, Sprite icon = null,
            PackingWeightCategory category = PackingWeightCategory.Normal)
        {
            var definition = CreateInstance<GiftDefinition>();
            definition.giftName = displayName;
            definition.cells = footprint;
            definition.tint = color;
            definition.canRotate = rotatable;
            definition.sprite = icon;
            definition.weightCategory = category;
            return definition;
        }
    }
}