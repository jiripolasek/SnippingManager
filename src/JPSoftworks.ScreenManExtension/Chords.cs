using Windows.System;

namespace JPSoftworks.ScreenManExtension;

internal static class Chords
{
    private static KeyChord From(
        bool ctrl = false,
        bool alt = false,
        bool shift = false,
        bool win = false,
        VirtualKey vkey = 0,
        int scanCode = 0)
    {
        return KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, (int)vkey, scanCode);
    }

    public static KeyChord CopyCapture { get; } = From(ctrl: true, vkey: VirtualKey.C);

    public static KeyChord ToggleFavorite { get; } = From(ctrl: true, vkey: VirtualKey.D);

    public static KeyChord EditLabelAndTags { get; } = From(ctrl: true, vkey: VirtualKey.E);

    public static KeyChord ShowInFolder { get; } = From(ctrl: true, shift: true, vkey: VirtualKey.E);

    public static KeyChord CopyPath { get; } = From(ctrl: true, shift: true, vkey: VirtualKey.C);

    public static KeyChord DeleteCapture { get; } = From(ctrl: true, vkey: VirtualKey.Delete);
}
