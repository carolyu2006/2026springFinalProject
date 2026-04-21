using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

#if UNITY_EDITOR
using UnityEditor;
#endif

// The "Maker Orango Controller" HID descriptor trips an infinite recursion in
// InputDeviceBuilder.InsertControlBitRangeNode. The controller is read over
// serial (see SerialController.cs), so the InputSystem never needs a layout
// for it. We:
//   1. Intercept layout resolution so future device arrivals use the bare
//      "HID" layout instead of the auto-generated (crashing) one.
//   2. Tear down any cached "HID::Maker Orango Controller" layout.
//   3. Remove any matching device that Unity's native layer already
//      enumerated before this script ran.
public static class OrangoHIDSuppressor
{
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EditorInit() => Install();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Install()
    {
        InputSystem.onFindLayoutForDevice -= OnFindLayoutForDevice;
        InputSystem.onFindLayoutForDevice += OnFindLayoutForDevice;

        try { InputSystem.RemoveLayout("HID::Maker Orango Controller"); } catch { }

        foreach (var device in InputSystem.devices.ToArray())
        {
            if (IsOrango(device.description))
                InputSystem.RemoveDevice(device);
        }
    }

    private static string OnFindLayoutForDevice(ref InputDeviceDescription description,
                                                string matchedLayout,
                                                InputDeviceExecuteCommandDelegate executeCommand)
    {
        return IsOrango(description) ? "HID" : null;
    }

    private static bool IsOrango(InputDeviceDescription description)
    {
        if (description.interfaceName != "HID") return false;
        var product = description.product;
        return !string.IsNullOrEmpty(product) &&
               product.IndexOf("Orango", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
