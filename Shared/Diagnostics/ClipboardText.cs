namespace ClickIt.Shared.Diagnostics;

// Copies text to the Windows clipboard from any thread: STA threads use Clipboard.SetText directly,
// other threads pipe through clip.exe so background coroutines can copy without an STA marshaller.
internal static class ClipboardText
{
    internal static bool TryCopy(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                Clipboard.SetText(text);
                return true;
            }

            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "clip.exe",
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true
            };

            if (!process.Start())
                return false;

            process.StandardInput.Write(text);
            process.StandardInput.Close();

            if (!process.WaitForExit(500))
            {
                try { process.Kill(); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

// Streams text to the Windows clipboard via clip.exe stdin, so a large report is never held in
// memory in full. Callers write chunks, then Finish() closes stdin and waits for clip.exe to exit.
internal sealed class ClipboardTextWriter : IDisposable
{
    private readonly Process _process;
    private bool _finished;

    private ClipboardTextWriter(Process process, StreamWriter writer)
    {
        _process = process;
        Writer = writer;
    }

    internal StreamWriter Writer { get; }

    internal static ClipboardTextWriter? Open()
    {
        try
        {
            Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "clip.exe",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
                return null;

            return new ClipboardTextWriter(process, process.StandardInput);
        }
        catch
        {
            return null;
        }
    }

    internal bool Write(string text)
    {
        try
        {
            Writer.Write(text);
            Writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal bool Finish()
    {
        if (_finished)
            return true;
        _finished = true;
        try
        {
            Writer.Close();
            if (!_process.WaitForExit(2000))
            {
                try { _process.Kill(); } catch { }
                return false;
            }
            return _process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    // Aborts a running stream: kills clip.exe so no partial dump lands on the clipboard.
    internal void Cancel()
    {
        if (_finished)
            return;
        _finished = true;
        try
        {
            _process.Kill();
            _process.WaitForExit(1000);
        }
        catch
        {
        }
        finally
        {
            _process.Dispose();
        }
    }

    public void Dispose()
    {
        _ = Finish();
    }
}
