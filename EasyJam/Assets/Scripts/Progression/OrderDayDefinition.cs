using System.Collections.Generic;
using UnityEngine;

/// <summary>One playable day: ordered list of customer orders.</summary>
[CreateAssetMenu(fileName = "OrderDay", menuName = "The Last Gift/Order Day")]
public class OrderDayDefinition : ScriptableObject
{
    [Min(1)] public int DayNumber = 1;
    public List<GiftOrderDefinition> Orders = new List<GiftOrderDefinition>();
}
