using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;

namespace LocalAiDemo.Shared.Services
{
    public class WhisperService : IAsyncDisposable
    {
        private const string ModelFileName = "ggml-base.bin";
        private WhisperFactory? _whisperFactory;
        private bool _isInitialized;

        public event EventHandler<int>? DownloadProgress;

        public async Task Initialize()
        {
            if (_isInitialized) return;

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var myAppDataPath = Path.Combine(appDataPath, "LocalAiDemo");

            if (Directory.Exists(myAppDataPath) == false)
            {
                Directory.CreateDirectory(myAppDataPath);
            }

            var modelPath = Path.Combine(myAppDataPath, ModelFileName);

            if (!File.Exists(modelPath))
            {
                await DownloadModel(modelPath);
            }

            _whisperFactory = WhisperFactory.FromPath(modelPath);
            _isInitialized = true;
        }

        private async Task DownloadModel(string modelPath)
        {
            try
            {
                //using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
                //using var fileWriter = File.OpenWrite(modelName);
                //await modelStream.CopyToAsync(fileWriter);

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(15); // Model is large, increase timeout

                var response =
                    await client.GetAsync("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
                        HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using var modelStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(modelPath);

                var buffer = new byte[81920];
                int bytesRead;
                long totalBytesRead = 0;

                while ((bytesRead = await modelStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progressPercentage = (int)((totalBytesRead * 100) / totalBytes);
                        DownloadProgress?.Invoke(this, progressPercentage);
                    }
                }
            }
            catch (Exception)
            {
                if (File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                }

                throw;
            }
        }

        public async Task<string> TranscribeAudio(string audioPath)
        {
            if (!_isInitialized)
            {
                await Initialize();
            }

            if (_whisperFactory == null)
            {
                throw new InvalidOperationException("Whisper factory not initialized");
            }

            var result = new System.Text.StringBuilder();

            using var processor = _whisperFactory.CreateBuilder()
                .WithLanguage("de")
                .Build();

            using var fileStream = File.OpenRead(audioPath);
            await foreach (var segment in processor.ProcessAsync(fileStream))
            {
                result.AppendLine(segment.Text);
            }

            return result.ToString().Trim();
        }

        public async Task<string> TranscribeAudioData(byte[] audioData)
        {
            if (!_isInitialized)
            {
                await Initialize();
            }

            if (_whisperFactory == null)
            {
                throw new InvalidOperationException("Whisper factory not initialized");
            }

            var result = new System.Text.StringBuilder();

            using var processor = _whisperFactory.CreateBuilder()
                .WithLanguage("de")
                .Build();

            using var memoryStream = new MemoryStream(audioData);
            await foreach (var segment in processor.ProcessAsync(memoryStream))
            {
                result.Append(segment.Text);
            }

            return result.ToString().Trim();
        }

        public ValueTask DisposeAsync()
        {
            _whisperFactory?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}