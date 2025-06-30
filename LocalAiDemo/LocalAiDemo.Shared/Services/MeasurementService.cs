using System.Timers;
using LocalAiDemo.Shared.Models;
using Microsoft.Extensions.Logging;

namespace LocalAiDemo.Shared.Services
{
    public interface IMeasurementService
    {
        List<Measurement> GetMeasurements();

        event EventHandler<List<Measurement>> MeasurementsUpdated;
    }

    public class MeasurementService : IMeasurementService, IDisposable
    {
        private readonly List<Measurement> _measurements;
        private readonly System.Timers.Timer _updateTimer;
        private readonly Random _random = new Random();
        private readonly ILogger<MeasurementService>? _logger;

        public event EventHandler<List<Measurement>> MeasurementsUpdated = delegate { };

        public MeasurementService(ILogger<MeasurementService>? logger = null)
        {
            _logger = logger;
            _logger?.LogInformation("MeasurementService is being initialized");

            _measurements = new List<Measurement>
            {
                new Measurement { Id = 1, Name = "CPU-Auslastung", Value = 45.2, Unit = "%", Timestamp = DateTime.Now },
                new Measurement
                    { Id = 2, Name = "Speichernutzung", Value = 3.7, Unit = "GB", Timestamp = DateTime.Now },
                new Measurement
                    { Id = 3, Name = "Netzwerkverkehr", Value = 1.2, Unit = "MB/s", Timestamp = DateTime.Now },
                new Measurement { Id = 4, Name = "Latenz", Value = 23, Unit = "ms", Timestamp = DateTime.Now },
                new Measurement { Id = 5, Name = "Temperatur", Value = 42, Unit = "°C", Timestamp = DateTime.Now }
            }; // Set up timer to periodically update measurements
            _updateTimer = new System.Timers.Timer(5000); // Update every 5 seconds
            _updateTimer.Elapsed += OnTimerElapsed;
            _updateTimer.AutoReset = true;
            _updateTimer.Enabled = true;
            _updateTimer.Start();

            _logger?.LogInformation("MeasurementService initialized with {Count} measurements and timer started",
                _measurements.Count);
            // Trigger initial update
            Task.Run(() => UpdateMeasurements());
        }

        public List<Measurement> GetMeasurements()
        {
            return _measurements;
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                UpdateMeasurements();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating measurements");
            }
        }

        private void UpdateMeasurements()
        {
            _logger?.LogDebug("Updating measurements - started");

            // Update with random fluctuations
            foreach (var measurement in _measurements)
            {
                double originalValue = measurement.Value;
                switch (measurement.Name)
                {
                    case "CPU-Auslastung":
                        measurement.Value = Math.Min(100,
                            Math.Max(5, measurement.Value + (_random.NextDouble() * 10 - 5)));
                        break;

                    case "Speichernutzung":
                        measurement.Value = Math.Min(8, Math.Max(2, measurement.Value + (_random.NextDouble() - 0.5)));
                        break;

                    case "Netzwerkverkehr":
                        measurement.Value = Math.Min(5,
                            Math.Max(0.1, measurement.Value + (_random.NextDouble() - 0.5)));
                        break;

                    case "Latenz":
                        measurement.Value = Math.Min(100,
                            Math.Max(10, measurement.Value + (_random.NextDouble() * 10 - 5)));
                        break;

                    case "Temperatur":
                        measurement.Value = Math.Min(80,
                            Math.Max(35, measurement.Value + (_random.NextDouble() * 2 - 1)));
                        break;
                }

                measurement.Timestamp = DateTime.Now;
                _logger?.LogTrace("Updated {Name}: {OldValue:F1} -> {NewValue:F1} {Unit}",
                    measurement.Name, originalValue, measurement.Value, measurement.Unit);
            }

            // Notify subscribers
            _logger?.LogInformation("Raising MeasurementsUpdated event with {Count} measurements", _measurements.Count);
            try
            {
                MeasurementsUpdated?.Invoke(this, new List<Measurement>(_measurements));
                _logger?.LogDebug("MeasurementsUpdated event successfully raised");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error raising MeasurementsUpdated event");
            }
        }

        public void Dispose()
        {
            _updateTimer?.Stop();
            _updateTimer?.Dispose();
        }
    }
}