#nullable enable
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ExtractionWeight.Telemetry;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionWeight.Tests.EditMode
{
    public class TelemetryLoggerTests
    {
        private string _tempDirectory = null!;
        private string _logPath = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "ExtractionWeightTelemetryTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
            _logPath = Path.Combine(_tempDirectory, "telemetry.jsonl");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        [Test]
        public async Task EventsAreWrittenCorrectlyToLogFile()
        {
            var logger = new TelemetryLogger(_logPath);

            logger.Enqueue(
                TelemetryEventNames.ItemPickedUp,
                "run-001",
                new ItemPickedUpPayload
                {
                    itemId = "battery",
                    itemName = "Leaking Battery",
                    value = 42f,
                    breakpoint = "Loaded",
                    isVolatile = true,
                    capacityFraction = 0.58f,
                });

            await logger.FlushAsync();
            await logger.DisposeAsync();

            var line = File.ReadAllText(_logPath).Trim();
            var envelope = JsonUtility.FromJson<TelemetryEnvelopeProbe>(line);

            Assert.That(envelope.runId, Is.EqualTo("run-001"));
            Assert.That(envelope.eventName, Is.EqualTo(TelemetryEventNames.ItemPickedUp));
            StringAssert.Contains("\"itemId\":\"battery\"", line);
            StringAssert.Contains("\"breakpoint\":\"Loaded\"", line);
            StringAssert.Contains("\"isVolatile\":true", line);
        }

        [Test]
        public async Task LogFileIsReadableAsValidJsonLines()
        {
            var logger = new TelemetryLogger(_logPath);

            for (var i = 0; i < 4; i++)
            {
                logger.Enqueue(
                    TelemetryEventNames.RunStarted,
                    $"run-{i}",
                    new RunStartedPayload
                    {
                        zoneId = "drydock",
                        zoneDisplayName = "Drydock",
                    });
            }

            await logger.FlushAsync();
            await logger.DisposeAsync();

            var lines = File.ReadAllLines(_logPath);
            Assert.That(lines, Has.Length.EqualTo(4));

            for (var i = 0; i < lines.Length; i++)
            {
                var envelope = JsonUtility.FromJson<TelemetryEnvelopeProbe>(lines[i]);
                Assert.That(envelope.timestampUtc, Is.Not.Empty);
                Assert.That(envelope.runId, Is.EqualTo($"run-{i}"));
                Assert.That(envelope.eventName, Is.EqualTo(TelemetryEventNames.RunStarted));
            }
        }

        [Test]
        public async Task HighEventRatesUseAsyncQueueWithoutBlockingMainThread()
        {
            var logger = new TelemetryLogger(_logPath);

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < 1000; i++)
            {
                logger.Enqueue(
                    TelemetryEventNames.BreakpointCrossed,
                    "run-high-rate",
                    new BreakpointCrossedPayload
                    {
                        breakpoint = "Loaded",
                        direction = "Up",
                        capacityFraction = 0.5f,
                    });
            }

            stopwatch.Stop();
            await logger.FlushAsync();
            await logger.DisposeAsync();

            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(500));
            Assert.That(File.ReadAllLines(_logPath), Has.Length.EqualTo(1000));
        }
    }
}
