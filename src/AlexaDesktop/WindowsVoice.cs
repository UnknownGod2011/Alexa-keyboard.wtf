using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;

namespace AlexaDesktop;

internal sealed class VoiceUnavailableException(string message) : Exception(message);
internal sealed record VoiceTranscript(string Text, float Confidence);
internal interface IVoiceService
{
    Task<VoiceTranscript> ListenAsync(CancellationToken ct);
    Task SpeakAsync(string text, CancellationToken ct);
}

// Native speech objects are created only after an explicit button click, never on startup.
internal sealed class WindowsVoice : IVoiceService
{
    public Task<VoiceTranscript> ListenAsync(CancellationToken ct) => Task.Run(async () =>
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var installed = SpeechRecognitionEngine.InstalledRecognizers();
            var recognizer = installed.FirstOrDefault(r => r.Culture.Equals(CultureInfo.CurrentUICulture)) ?? installed.FirstOrDefault();
            if (recognizer is null) throw new VoiceUnavailableException("No Windows speech recognizer is installed. Install a Windows speech language pack, then try Dictate again.");
            using var engine = new SpeechRecognitionEngine(recognizer);
            engine.InitialSilenceTimeout = TimeSpan.FromSeconds(5);
            engine.BabbleTimeout = TimeSpan.FromSeconds(10);
            engine.LoadGrammar(new DictationGrammar());
            engine.SetInputToDefaultAudioDevice();
            var completion = new TaskCompletionSource<VoiceTranscript>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Completed(object? sender, RecognizeCompletedEventArgs e)
            {
                if (e.Cancelled) completion.TrySetCanceled();
                else if (e.Error is not null) completion.TrySetException(new VoiceUnavailableException("Windows speech recognition failed. Check the microphone and speech language settings."));
                else if (e.Result is null || string.IsNullOrWhiteSpace(e.Result.Text) || e.Result.Confidence < 0.3)
                    completion.TrySetException(new VoiceUnavailableException("No clear speech was recognized. Nothing was sent; try again or type your command."));
                else completion.TrySetResult(new(e.Result.Text, e.Result.Confidence));
            }
            engine.RecognizeCompleted += Completed;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            ct.ThrowIfCancellationRequested();
            engine.RecognizeAsync(RecognizeMode.Single);
            try { return await completion.Task.WaitAsync(deadline.Token); }
            finally
            {
                engine.RecognizeCompleted -= Completed;
                if (!completion.Task.IsCompleted) engine.RecognizeAsyncCancel();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or NotSupportedException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception or IOException)
        { throw new VoiceUnavailableException("Windows dictation is unavailable. Check your microphone permissions, input device and installed speech language. Typed commands remain available."); }
    }, ct);

    public Task SpeakAsync(string text, CancellationToken ct) => Task.Run(async () =>
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            using var speech = new SpeechSynthesizer();
            speech.SetOutputToDefaultAudioDevice();
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Completed(object? sender, SpeakCompletedEventArgs e)
            {
                if (e.Cancelled) completion.TrySetCanceled();
                else if (e.Error is not null) completion.TrySetException(new VoiceUnavailableException("Windows could not read this text aloud."));
                else completion.TrySetResult();
            }
            speech.SpeakCompleted += Completed;
            ct.ThrowIfCancellationRequested();
            speech.SpeakAsync(text);
            try { await completion.Task.WaitAsync(ct); }
            finally
            {
                speech.SpeakCompleted -= Completed;
                if (!completion.Task.IsCompleted) speech.SpeakAsyncCancelAll();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or NotSupportedException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception or IOException)
        { throw new VoiceUnavailableException("Windows read-aloud is unavailable. Check installed voices and your audio output device."); }
    }, ct);
}

// Hosted UI checks cannot prove microphone or speaker behavior; use this adapter explicitly.
internal sealed class RecordingVoice : IVoiceService
{
    internal string Transcript = "status";
    internal string? Spoken;
    internal bool Fail, Block;
    internal readonly TaskCompletionSource Listening = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<VoiceTranscript> ListenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Fail) throw new VoiceUnavailableException("Fixture microphone unavailable.");
        Listening.TrySetResult();
        if (Block) await Task.Delay(Timeout.Infinite, ct);
        return new(Transcript, 0.99f);
    }
    public Task SpeakAsync(string text, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Spoken = text; return Task.CompletedTask; }
}
