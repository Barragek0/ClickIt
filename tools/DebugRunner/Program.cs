using System;
using ClickIt;
using ClickIt.Services;
using ClickIt.Rendering;
using ClickIt.Utils;

Console.WriteLine("DebugRunner: starting");
var plugin = new ClickIt();
plugin.__Test_SetSettings(new ClickItSettings());
var settings = plugin.__Test_GetSettings();
var altarService = new AltarService(plugin, settings, null);
altarService.DebugInfo.LastScanExarchLabels = 3;
altarService.DebugInfo.LastScanEaterLabels = 7;
altarService.DebugInfo.ElementsFound = 5;
altarService.DebugInfo.ComponentsProcessed = 11;
altarService.DebugInfo.ComponentsAdded = 2;
altarService.DebugInfo.ComponentsDuplicated = 1;
altarService.DebugInfo.ModsMatched = 9;
altarService.DebugInfo.ModsUnmatched = 4;
altarService.DebugInfo.LastProcessedAltarType = "TestAltar";
altarService.DebugInfo.LastError = "boom";
altarService.DebugInfo.LastScanTime = DateTime.Now;

var dt = new DeferredTextQueue();
var dr = new DebugRenderer(plugin, altarService: altarService, deferredTextQueue: dt);

try
{
    Console.WriteLine("Invoking RenderAltarServiceDebug...");
    var mi = dr.GetType().GetMethod("RenderAltarServiceDebug", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    var result = mi.Invoke(dr, new object[] { 5, 5, 12 });
    Console.WriteLine("OK -> returned: " + result?.ToString());

    var itemsField = typeof(DeferredTextQueue).GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    var items = (System.Collections.ICollection)itemsField.GetValue(dt)!;
    Console.WriteLine("Queue count: " + items.Count);

    foreach (var entry in (System.Collections.IEnumerable)items)
    {
        var txt = (string)entry.GetType().GetProperty("Item1")!.GetValue(entry)!;
        Console.WriteLine("ITEM: " + txt);
    }
}
catch (Exception ex)
{
    Console.WriteLine("EX: " + ex.ToString());
}

Console.WriteLine("Done.");
