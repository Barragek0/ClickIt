namespace ClickIt.Shared.Diagnostics;

// Copies text to the Windows clipboard from any thread: STA threads use Clipboard.SetText directly, other threads pipe through clip.exe so background coroutines can copy without an STA marshaller.
internal static class ClipboardText
{
    internal static Process StartClipProcess()
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "clip.exe",
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true
            }
        };
    }

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

            using Process process = StartClipProcess();

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
