#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ExtractionWeight.Telemetry
{
    public sealed class TelemetryLogger : IDisposable, IAsyncDisposable
    {
        private readonly ConcurrentQueue<string> _pendingLines = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly Task _workerTask;

        private int _queuedLineCount;
        private bool _disposed;

        public TelemetryLogger(string filePath)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            _workerTask = Task.Run(ProcessQueueAsync);
        }

        public string FilePath { get; }

        public void Enqueue<TPayload>(string eventName, string runId, TPayload payload)
            where TPayload : class
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TelemetryLogger));
            }

            var line = JsonUtility.ToJson(new TelemetryEnvelope<TPayload>
            {
                timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
                runId = runId,
                eventName = eventName,
                payload = payload,
            });

            Interlocked.Increment(ref _queuedLineCount);
            _pendingLines.Enqueue(line);
            _signal.Release();
        }

        public async Task FlushAsync()
        {
            while (Volatile.Read(ref _queuedLineCount) > 0)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await FlushAsync().ConfigureAwait(false);
            _cancellationTokenSource.Cancel();
            _signal.Release();

            try
            {
                await _workerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            _signal.Dispose();
            _cancellationTokenSource.Dispose();
        }

        private async Task ProcessQueueAsync()
        {
            await using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream);

            while (!_cancellationTokenSource.IsCancellationRequested || !_pendingLines.IsEmpty)
            {
                await _signal.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false);

                while (_pendingLines.TryDequeue(out var line))
                {
                    await writer.WriteLineAsync(line).ConfigureAwait(false);
                    Interlocked.Decrement(ref _queuedLineCount);
                }

                await writer.FlushAsync().ConfigureAwait(false);
            }
        }

        [Serializable]
        private sealed class TelemetryEnvelope<TPayload>
            where TPayload : class
        {
            public string timestampUtc = string.Empty;
            public string runId = string.Empty;
            public string eventName = string.Empty;
            public TPayload? payload;
        }
    }
}
