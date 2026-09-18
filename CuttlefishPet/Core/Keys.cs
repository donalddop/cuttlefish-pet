namespace CuttlefishPet.Core;

/// <summary>
/// What the keyboard is doing right now, asked of Windows rather than of WPF: the
/// overlay never holds focus, so <c>Keyboard.Modifiers</c> is always empty here.
/// </summary>
public static class Keys
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int Control = 0x11;

    /// <summary>
    /// Ctrl held. It means "I am looking at you, hold still": the inspector appears
    /// and nothing bolts from the cursor, because an animal that flees the moment
    /// you point at it is an animal you can never get a proper look at.
    /// </summary>
    public static bool Inspecting => (GetAsyncKeyState(Control) & 0x8000) != 0;
}
