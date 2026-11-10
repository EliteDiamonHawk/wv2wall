using Xunit;

public sealed class KeyboardInputPolicyTests
{
    [Fact]
    public void Ownership_starts_safe_and_without_target()
    {
        var ownership = new KeyboardOwnership<object>();

        Assert.Equal(BackgroundKeyboardState.NoBackgroundKeyboardInput, ownership.State);
        Assert.False(ownership.TryGetTarget(out _));
    }

    [Fact]
    public void Ownership_tracks_exact_target_and_releases_forwarded_keys()
    {
        var ownership = new KeyboardOwnership<object>();
        object target = new();

        Assert.True(ownership.Arm(target, out object previous));
        Assert.Null(previous);
        Assert.Equal(BackgroundKeyboardState.PendingBackgroundKeyboardInteraction, ownership.State);
        Assert.True(ownership.Begin(target, KeyboardFocusKind.Editable, out previous));
        Assert.True(ownership.TryGetTarget(out object active));
        Assert.Same(target, active);

        ownership.Track(KeyboardInputPolicy.WmKeyDown, 0x11, 29, 0);
        Assert.True(ownership.End(out object endedTarget, out var keys));

        Assert.Same(target, endedTarget);
        Assert.Single(keys);
        Assert.Equal(29, keys[0].Value.ScanCode);
        Assert.Equal(BackgroundKeyboardState.NoBackgroundKeyboardInput, ownership.State);
        Assert.False(ownership.TryGetTarget(out _));
    }

    [Fact]
    public void Repeated_begin_for_same_target_does_not_change_target()
    {
        var ownership = new KeyboardOwnership<object>();
        object target = new();

        Assert.True(ownership.Arm(target, out _));
        Assert.True(ownership.Begin(target, KeyboardFocusKind.Editable, out _));
        Assert.True(ownership.Arm(target, out object previous));
        Assert.True(ownership.Begin(target, KeyboardFocusKind.Editable, out _));
        Assert.Same(target, previous);
    }

    [Fact]
    public void System_keys_are_not_capture_candidates()
    {
        Assert.True(KeyboardInputPolicy.IsSystemOwnedKey(KeyboardInputPolicy.WmKeyDown, KeyboardInputPolicy.VkLWin, false, false, false));
        Assert.True(KeyboardInputPolicy.IsSystemOwnedKey(KeyboardInputPolicy.WmKeyDown, KeyboardInputPolicy.VkEscape, false, false, false));
        Assert.True(KeyboardInputPolicy.IsSystemOwnedKey(KeyboardInputPolicy.WmKeyDown, KeyboardInputPolicy.VkTab, true, false, false));
        Assert.True(KeyboardInputPolicy.IsSystemOwnedKey(KeyboardInputPolicy.WmSysKeyDown, 0x46, true, false, false));
        Assert.True(KeyboardInputPolicy.IsSystemOwnedKey(KeyboardInputPolicy.WmKeyDown, 0x87, false, false, false));
        Assert.True(KeyboardInputPolicy.IsSystemOwnedKey(KeyboardInputPolicy.WmKeyDown, KeyboardInputPolicy.VkCapital, false, false, false));
        Assert.False(KeyboardInputPolicy.EndsOwnership(KeyboardInputPolicy.WmKeyDown, KeyboardInputPolicy.VkCapital, false, false, false));
        Assert.True(KeyboardInputPolicy.EndsOwnership(KeyboardInputPolicy.WmKeyDown, KeyboardInputPolicy.VkEscape, false, false, true));
        Assert.False(KeyboardInputPolicy.IsSystemOwnedKey(KeyboardInputPolicy.WmKeyDown, 0x41, false, false, false));
    }
}
