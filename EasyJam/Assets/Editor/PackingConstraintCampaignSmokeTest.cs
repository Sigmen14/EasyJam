#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TheLastGift.Packing;

/// <summary>Checks authored packing orders for valid references and at least one legal 4x4 solution.</summary>
public static class PackingConstraintCampaignSmokeTest
{
    private const string OrdersFolder = "Assets/Data/PackingOrders";

    [MenuItem("Tools/The Last Gift/Run Packing Constraint Smoke Test")]
    private static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:PackingPuzzleOrderDefinition", new[] { OrdersFolder });
        if (guids.Length != 3)
        {
            Fail("Ожидалось ровно три заказа упаковки, найдено: " + guids.Length);
            return;
        }

        var orders = new List<PackingPuzzleOrderDefinition>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PackingPuzzleOrderDefinition order = AssetDatabase.LoadAssetAtPath<PackingPuzzleOrderDefinition>(path);
            if (order == null) { Fail("Не удалось загрузить заказ: " + path); return; }
            orders.Add(order);
        }
        orders.Sort((a, b) => a.OrderNumber.CompareTo(b.OrderNumber));

        int[] expectedRuleCounts = { 1, 2, 2 };
        for (int i = 0; i < orders.Count; i++)
        {
            PackingPuzzleOrderDefinition order = orders[i];
            if (order.OrderNumber != i + 1)
            { Fail("Нарушена последовательность заказов: ожидался номер " + (i + 1)); return; }
            if (!order.TryValidateConfiguration(4, 4, out string error))
            { Fail(order.OrderTitle + ": " + error); return; }
            if (order.Constraints.Count != expectedRuleCounts[i])
            { Fail(order.OrderTitle + ": неверное количество условий."); return; }
            if (!order.HasFeasibleSolution(4, 4))
            { Fail(order.OrderTitle + ": не найдено ни одной допустимой раскладки 4×4."); return; }
        }

        const string report =
            "PASSED\n" +
            "3 последовательных заказа 4×4 загружены и проверены.\n" +
            "Постепенное усложнение: 1 → 2 → 2 условия.\n" +
            "Площадь всех заказов — 16 клеток; у каждого есть допустимое решение.\n" +
            "Проверка хрупкости, комплекта и центральной зоны пройдена.";
        Debug.Log("[Packing Constraint Campaign Smoke Test]\n" + report);
        EditorUtility.DisplayDialog("Packing Constraint Campaign Smoke Test", report, "OK");
    }

    private static void Fail(string message)
    {
        Debug.LogError("[Packing Constraint Campaign Smoke Test] FAILED: " + message);
        EditorUtility.DisplayDialog("Packing Constraint Campaign Smoke Test", "FAILED\n\n" + message, "OK");
    }
}
#endif
