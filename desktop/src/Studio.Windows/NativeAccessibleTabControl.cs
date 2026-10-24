namespace Studio.Windows;

// Delegate the legacy accessibility child list to the real native control.
// TabControlAccessibleObject adds a selected-page pseudo-child to the native
// header list, which disagrees with native MSAA child IDs during UIA bridging.
public sealed class NativeAccessibleTabControl : TabControl
{
    protected override AccessibleObject CreateAccessibilityInstance()=>new ControlAccessibleObject(this);
}
