using System.Collections.Generic;
using UnityEngine;

/// <summary>Campaign data asset containing days in progression order.</summary>
[CreateAssetMenu(fileName = "OrderCampaign", menuName = "The Last Gift/Order Campaign")]
public class OrderCampaignDefinition : ScriptableObject
{
    public List<OrderDayDefinition> Days = new List<OrderDayDefinition>();
}
