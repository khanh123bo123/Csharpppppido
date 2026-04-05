using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Media;

namespace TouristGuideApp.Services
{
    public interface IAudioService
    {
        Task EnqueueSpeechAsync(string text, Action onStarted, Action onEnded);
        bool IsPlaying { get; }
    }

    public class AudioService : IAudioService
    {
        private readonly Queue<(string Text, Action OnStarted, Action OnEnded)> _speechQueue = new();
        private bool _isProcessing = false;
        public bool IsPlaying { get; private set; }

        public async Task EnqueueSpeechAsync(string text, Action onStarted, Action onEnded)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            _speechQueue.Enqueue((text, onStarted, onEnded));

            if (!_isProcessing)
            {
                await ProcessQueueAsync();
            }
        }

        private async Task ProcessQueueAsync()
        {
            _isProcessing = true;

            while (_speechQueue.Count > 0)
            {
                var item = _speechQueue.Dequeue();

                IsPlaying = true;
                item.OnStarted?.Invoke();

                try
                {
                    await TextToSpeech.Default.SpeakAsync(item.Text, new SpeechOptions
                    {
                        Pitch = 1.0f,
                        Volume = 1.0f
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Audio Error: {ex.Message}");
                }

                IsPlaying = false;
                item.OnEnded?.Invoke();

                // Nghỉ một chút giữa các đoạn hội thoại
                await Task.Delay(500);
            }

            _isProcessing = false;
        }
    }
}
