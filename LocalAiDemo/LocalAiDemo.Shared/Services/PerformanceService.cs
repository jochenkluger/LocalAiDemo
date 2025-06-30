using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services
{
    public interface IPerformanceService
    {
        double? LastSttDuration { get; }
        double? LastLlmDuration { get; }
        double? LastTtsDuration { get; }

        List<string> GetLogEntries();

        // Methods for measuring real durations
        IDisposable StartSttMeasurement();

        IDisposable StartLlmMeasurement();

        IDisposable StartTtsMeasurement();

        // Manual logging methods (for backwards compatibility)
        void LogSttDuration(double durationMs);

        void LogLlmDuration(double durationMs);

        void LogTtsDuration(double durationMs);

        void AddLogEntry(string message, LogLevel level = LogLevel.Information);

        event EventHandler? PerformanceUpdated;
    }

    public class PerformanceService : IPerformanceService
    {
        private readonly ILogger<PerformanceService>? _logger;
        private readonly List<string> _logEntries = new();
        private readonly object _lock = new();

        public double? LastSttDuration { get; private set; }
        public double? LastLlmDuration { get; private set; }
        public double? LastTtsDuration { get; private set; }

        public event EventHandler? PerformanceUpdated;

        public PerformanceService(ILogger<PerformanceService>? logger = null)
        {
            _logger = logger;
            _logger?.LogInformation("PerformanceService initialisiert");
        }

        public IDisposable StartSttMeasurement()
        {
            return new PerformanceMeasurement(this, MeasurementType.Stt);
        }

        public IDisposable StartLlmMeasurement()
        {
            return new PerformanceMeasurement(this, MeasurementType.Llm);
        }

        public IDisposable StartTtsMeasurement()
        {
            return new PerformanceMeasurement(this, MeasurementType.Tts);
        }

        public void LogSttDuration(double durationMs)
        {
            lock (_lock)
            {
                LastSttDuration = durationMs;
                AddLogEntry($"STT: {durationMs:F1}ms", LogLevel.Information);
                _logger?.LogInformation("STT Duration: {Duration}ms", durationMs);
                PerformanceUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void LogLlmDuration(double durationMs)
        {
            lock (_lock)
            {
                LastLlmDuration = durationMs;
                AddLogEntry($"LLM: {durationMs:F1}ms", LogLevel.Information);
                _logger?.LogInformation("LLM Duration: {Duration}ms", durationMs);
                PerformanceUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void LogTtsDuration(double durationMs)
        {
            lock (_lock)
            {
                LastTtsDuration = durationMs;
                AddLogEntry($"TTS: {durationMs:F1}ms", LogLevel.Information);
                _logger?.LogInformation("TTS Duration: {Duration}ms", durationMs);
                PerformanceUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void AddLogEntry(string message, LogLevel level = LogLevel.Information)
        {
            lock (_lock)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var levelStr = level switch
                {
                    LogLevel.Trace => "TRC",
                    LogLevel.Debug => "DBG",
                    LogLevel.Information => "INF",
                    LogLevel.Warning => "WRN",
                    LogLevel.Error => "ERR",
                    LogLevel.Critical => "CRT",
                    _ => "LOG"
                };
                var logEntry = $"[{timestamp}] [{levelStr}] {message}";
                _logEntries.Add(logEntry);

                // Behalte nur die letzten 100 Einträge
                if (_logEntries.Count > 100)
                {
                    _logEntries.RemoveAt(0);
                }
            }
        }

        public List<string> GetLogEntries()
        {
            lock (_lock)
            {
                return new List<string>(_logEntries);
            }
        }
    }

    public enum MeasurementType
    {
        Stt,
        Llm,
        Tts
    }

    public class PerformanceMeasurement : IDisposable
    {
        private readonly PerformanceService _service;
        private readonly MeasurementType _type;
        private readonly Stopwatch _stopwatch;
        private bool _disposed = false;

        public PerformanceMeasurement(PerformanceService service, MeasurementType type)
        {
            _service = service;
            _type = type;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                var durationMs = _stopwatch.Elapsed.TotalMilliseconds;

                switch (_type)
                {
                    case MeasurementType.Stt:
                        _service.LogSttDuration(durationMs);
                        break;

                    case MeasurementType.Llm:
                        _service.LogLlmDuration(durationMs);
                        break;

                    case MeasurementType.Tts:
                        _service.LogTtsDuration(durationMs);
                        break;
                }

                _disposed = true;
            }
        }
    }
}