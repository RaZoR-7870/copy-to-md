using CopyToMd.Clipboard;
using CopyToMd.Conversion;
using CopyToMd.Target;
using Xunit;

namespace CopyToMd.Tests;

public sealed class SmartPasteCoordinatorTests
{
    [Fact]
    public void ConvertsForForegroundMarkdownEditorAndPastesOnce()
    {
        var clipboard = new FakeClipboardService();
        var paste = new FakePasteService();
        var coordinator = CreateCoordinator(clipboard, paste, out var statuses);

        coordinator.Execute();

        Assert.NotNull(clipboard.WrittenResult);
        Assert.Equal("- first\n- second\n- third", clipboard.WrittenResult!.Text);
        Assert.Equal(1, paste.SendCount);
        Assert.True(statuses[^1].Success);
    }

    [Fact]
    public void ClipboardChangeDuringWritePreventsPaste()
    {
        var clipboard = new FakeClipboardService { WriteException = new ClipboardChangedException("changed") };
        var paste = new FakePasteService();
        var coordinator = CreateCoordinator(clipboard, paste, out var statuses);

        coordinator.Execute();

        Assert.Equal(0, paste.SendCount);
        Assert.False(statuses[^1].Success);
    }

    [Fact]
    public void ForegroundChangeAfterWritePreventsPaste()
    {
        var clipboard = new FakeClipboardService { ThrowOnEnsureCall = 2 };
        var paste = new FakePasteService();
        var coordinator = CreateCoordinator(clipboard, paste, out var statuses);

        coordinator.Execute();

        Assert.NotNull(clipboard.WrittenResult);
        Assert.Equal(0, paste.SendCount);
        Assert.False(statuses[^1].Success);
    }

    [Fact]
    public void PartialPasteFailureIsNotRetried()
    {
        var clipboard = new FakeClipboardService();
        var paste = new FakePasteService { SendResult = false };
        var coordinator = CreateCoordinator(clipboard, paste, out var statuses);

        coordinator.Execute();

        Assert.Equal(1, paste.SendCount);
        Assert.False(statuses[^1].Success);
    }

    private static SmartPasteCoordinator CreateCoordinator(
        FakeClipboardService clipboard,
        FakePasteService paste,
        out List<SmartPasteStatus> statuses)
    {
        var coordinator = new SmartPasteCoordinator(
            clipboard,
            new FakeForegroundWindowService(),
            paste,
            new ConversionService(),
            new TargetResolver());
        var captured = new List<SmartPasteStatus>();
        coordinator.StatusChanged += captured.Add;
        statuses = captured;
        return coordinator;
    }

    private sealed class FakeForegroundWindowService : IForegroundWindowService
    {
        public TargetInfo Capture() => new(10, 20, "obsidian", "note - Obsidian");
        public bool IsForeground(nint handle) => true;
    }

    private sealed class FakePasteService : IPasteService
    {
        public int SendCount { get; private set; }
        public bool SendResult { get; init; } = true;
        public bool WaitForModifierKeys() => true;
        public bool SendControlV()
        {
            SendCount++;
            return SendResult;
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        private int ensureCalls;
        public ConversionResult? WrittenResult { get; private set; }
        public Exception? WriteException { get; init; }
        public int ThrowOnEnsureCall { get; init; }

        public ClipboardSnapshot Capture(int maxCharacters) => new(100, null, null, "l first\nl second\nl third");

        public uint Write(ConversionResult result, uint expectedSequence, nint expectedWindow)
        {
            if (WriteException is not null)
            {
                throw WriteException;
            }

            WrittenResult = result;
            return expectedSequence + 1;
        }

        public void EnsureCurrent(uint expectedSequence, nint expectedWindow)
        {
            ensureCalls++;
            if (ensureCalls == ThrowOnEnsureCall)
            {
                throw new InvalidOperationException("window changed");
            }
        }
    }
}
