internal enum BackgroundKeyboardState
{
    NoBackgroundKeyboardInput,
    PendingBackgroundKeyboardInteraction,
    BackgroundKeyboardInteraction
}

internal enum KeyboardFocusKind
{
    None,
    Editable
}

internal static class KeyboardInputPolicy
{
    internal const int WmKeyDown = 0x0100;
    internal const int WmKeyUp = 0x0101;
    internal const int WmSysKeyDown = 0x0104;
    internal const int WmSysKeyUp = 0x0105;

    internal const int VkTab = 0x09;
    internal const int VkEscape = 0x1B;
    internal const int VkSnapshot = 0x2C;
    internal const int VkPause = 0x13;
    internal const int VkLWin = 0x5B;
    internal const int VkRWin = 0x5C;
    internal const int VkApps = 0x5D;
    internal const int VkMenu = 0x12;
    internal const int VkLMenu = 0xA4;
    internal const int VkRMenu = 0xA5;
    internal const int VkControl = 0x11;
    internal const int VkCapital = 0x14;
    internal const int VkNumLock = 0x90;
    internal const int VkScroll = 0x91;

    internal static bool IsKeyboardMessage(int message) =>
        message is WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp;

    internal static bool IsSystemOwnedKey(
        int message,
        int virtualKey,
        bool altDown,
        bool winDown,
        bool ctrlDown)
    {
        if (message is WmSysKeyDown or WmSysKeyUp)
            return true;

        if (virtualKey is VkLWin or VkRWin or VkMenu or VkLMenu or VkRMenu or VkSnapshot or VkApps or VkPause or VkEscape)
            return true;

        if (virtualKey is >= 0x70 and <= 0x87)
            return true;

        if (virtualKey is VkCapital or VkNumLock or VkScroll)
            return true;

        return altDown || winDown ||
            (virtualKey == VkTab && altDown) ||
            (virtualKey == VkEscape && ctrlDown);
    }

    internal static bool EndsOwnership(
        int message,
        int virtualKey,
        bool altDown,
        bool winDown,
        bool ctrlDown)
    {
        if (virtualKey is VkCapital or VkNumLock or VkScroll)
            return false;

        return IsSystemOwnedKey(message, virtualKey, altDown, winDown, ctrlDown);
    }
}

internal readonly record struct ForwardedKeyboardKey(int ScanCode, int Flags);

internal sealed class KeyboardOwnership<TTarget> where TTarget : class
{
    private readonly object _gate = new();
    private readonly Dictionary<int, ForwardedKeyboardKey> _forwardedKeys = new();

    internal BackgroundKeyboardState State { get; private set; } =
        BackgroundKeyboardState.NoBackgroundKeyboardInput;

    internal KeyboardFocusKind FocusKind { get; private set; } = KeyboardFocusKind.None;

    internal TTarget? Target { get; private set; }

    internal bool TryGetTarget(out TTarget? target)
    {
        lock (_gate)
        {
            target = State == BackgroundKeyboardState.BackgroundKeyboardInteraction ? Target : null;
            return target != null;
        }
    }

    internal bool HasTarget(TTarget target)
    {
        lock (_gate)
        {
            return State != BackgroundKeyboardState.NoBackgroundKeyboardInput &&
                ReferenceEquals(Target, target);
        }
    }

    internal bool Arm(TTarget target, out TTarget? previousTarget)
    {
        lock (_gate)
        {
            previousTarget = Target;
            bool changed = !ReferenceEquals(Target, target) ||
                State != BackgroundKeyboardState.PendingBackgroundKeyboardInteraction;
            Target = target;
            State = BackgroundKeyboardState.PendingBackgroundKeyboardInteraction;
            FocusKind = KeyboardFocusKind.None;
            return changed;
        }
    }

    internal bool Begin(TTarget target, KeyboardFocusKind focusKind, out TTarget? previousTarget)
    {
        lock (_gate)
        {
            previousTarget = Target;
            if (State != BackgroundKeyboardState.PendingBackgroundKeyboardInteraction ||
                !ReferenceEquals(Target, target) || focusKind == KeyboardFocusKind.None)
            {
                return false;
            }

            State = BackgroundKeyboardState.BackgroundKeyboardInteraction;
            FocusKind = focusKind;
            return true;
        }
    }

    internal bool End(out TTarget? target, out KeyValuePair<int, ForwardedKeyboardKey>[] forwardedKeys)
    {
        lock (_gate)
        {
            target = Target;
            forwardedKeys = _forwardedKeys.ToArray();
            bool changed = State != BackgroundKeyboardState.NoBackgroundKeyboardInput ||
                target != null || forwardedKeys.Length > 0;
            Target = null;
            State = BackgroundKeyboardState.NoBackgroundKeyboardInput;
            FocusKind = KeyboardFocusKind.None;
            _forwardedKeys.Clear();
            return changed;
        }
    }

    internal void Track(int message, int virtualKey, int scanCode, int flags)
    {
        lock (_gate)
        {
            if (State != BackgroundKeyboardState.BackgroundKeyboardInteraction)
                return;

            if (message is KeyboardInputPolicy.WmKeyUp or KeyboardInputPolicy.WmSysKeyUp)
                _forwardedKeys.Remove(virtualKey);
            else if (message is KeyboardInputPolicy.WmKeyDown or KeyboardInputPolicy.WmSysKeyDown)
                _forwardedKeys[virtualKey] = new ForwardedKeyboardKey(scanCode, flags);
        }
    }
}
