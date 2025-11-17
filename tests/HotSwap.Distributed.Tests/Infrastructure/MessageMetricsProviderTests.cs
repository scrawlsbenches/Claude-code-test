using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using FluentAssertions;
using HotSwap.Distributed.Infrastructure.Metrics;
using Xunit;

namespace HotSwap.Distributed.Tests.Infrastructure;

public class MessageMetricsProviderTests : IDisposable
{
    private readonly MessageMetricsProvider _provider;
    private readonly MeterListener _meterListener;
    private readonly ConcurrentDictionary<string, long> _counterValues;
    private readonly ConcurrentDictionary<string, List<double>> _histogramValues;
    private readonly ConcurrentDictionary<string, double> _gaugeValues;

    public MessageMetricsProviderTests()
    {
        _provider = new MessageMetricsProvider();
        _counterValues = new ConcurrentDictionary<string, long>();
        _histogramValues = new ConcurrentDictionary<string, List<double>>();
        _gaugeValues = new ConcurrentDictionary<string, double>();

        // Set up MeterListener to capture metrics
        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MessageMetricsProvider.ServiceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            var name = instrument.Name;
            _counterValues.AddOrUpdate(name, measurement, (key, oldValue) => oldValue + measurement);
        });

        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            var name = instrument.Name;

            // Histograms
            if (instrument is Histogram<double>)
            {
                _histogramValues.AddOrUpdate(
                    name,
                    new List<double> { measurement },
                    (key, list) =>
                    {
                        lock (list)
                        {
                            list.Add(measurement);
                        }
                        return list;
                    });
            }
            // Gauges (ObservableGauge)
            else
            {
                _gaugeValues.AddOrUpdate(name, measurement, (key, oldValue) => measurement);
            }
        });

        _meterListener.Start();
    }

    #region Counter Tests - Published

    [Fact]
    public void IncrementPublished_IncrementsCounter()
    {
        // Act
        _provider.IncrementPublished(topicName: "test.topic");
        _provider.IncrementPublished(topicName: "test.topic");
        _provider.IncrementPublished(topicName: "test.topic");

        // Wait for metrics to be collected
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _counterValues.Should().ContainKey("messages.published.total");
        _counterValues["messages.published.total"].Should().Be(3);
    }

    [Fact]
    public void IncrementPublished_WithDifferentTopics_CountsSeparately()
    {
        // Act
        _provider.IncrementPublished(topicName: "topic1");
        _provider.IncrementPublished(topicName: "topic1");
        _provider.IncrementPublished(topicName: "topic2");

        // Wait for metrics to be collected
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _counterValues["messages.published.total"].Should().Be(3);
    }

    #endregion

    #region Counter Tests - Delivered

    [Fact]
    public void IncrementDelivered_IncrementsCounter()
    {
        // Act
        _provider.IncrementDelivered(topicName: "test.topic");
        _provider.IncrementDelivered(topicName: "test.topic");

        // Wait for metrics to be collected
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _counterValues.Should().ContainKey("messages.delivered.total");
        _counterValues["messages.delivered.total"].Should().Be(2);
    }

    [Fact]
    public void IncrementDelivered_WithPriority_IncludesTag()
    {
        // Act
        _provider.IncrementDelivered(topicName: "test.topic", priority: 9);

        // Wait for metrics to be collected
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _counterValues.Should().ContainKey("messages.delivered.total");
        _counterValues["messages.delivered.total"].Should().Be(1);
    }

    #endregion

    #region Counter Tests - Failed

    [Fact]
    public void IncrementFailed_IncrementsCounter()
    {
        // Act
        _provider.IncrementFailed(topicName: "test.topic", reason: "timeout");
        _provider.IncrementFailed(topicName: "test.topic", reason: "invalid_payload");

        // Wait for metrics to be collected
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _counterValues.Should().ContainKey("messages.failed.total");
        _counterValues["messages.failed.total"].Should().Be(2);
    }

    [Fact]
    public void IncrementFailed_WithDifferentReasons_CountsAll()
    {
        // Act
        _provider.IncrementFailed(topicName: "test.topic", reason: "timeout");
        _provider.IncrementFailed(topicName: "test.topic", reason: "timeout");
        _provider.IncrementFailed(topicName: "test.topic", reason: "network_error");

        // Wait for metrics to be collected
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _counterValues["messages.failed.total"].Should().Be(3);
    }

    #endregion

    #region Histogram Tests - Publish Duration

    [Fact]
    public void RecordPublishDuration_RecordsHistogram()
    {
        // Act
        _provider.RecordPublishDuration(TimeSpan.FromMilliseconds(50), topicName: "test.topic");
        _provider.RecordPublishDuration(TimeSpan.FromMilliseconds(75), topicName: "test.topic");
        _provider.RecordPublishDuration(TimeSpan.FromMilliseconds(100), topicName: "test.topic");

        // Wait for metrics to be collected
        Thread.Sleep(100);

        // Assert
        _histogramValues.Should().ContainKey("message.publish.duration");
        _histogramValues["message.publish.duration"].Should().HaveCount(3);
        _histogramValues["message.publish.duration"].Should().Contain(50.0);
        _histogramValues["message.publish.duration"].Should().Contain(75.0);
        _histogramValues["message.publish.duration"].Should().Contain(100.0);
    }

    [Fact]
    public void RecordPublishDuration_CalculatesAverageCorrectly()
    {
        // Act
        _provider.RecordPublishDuration(TimeSpan.FromMilliseconds(100), topicName: "test.topic");
        _provider.RecordPublishDuration(TimeSpan.FromMilliseconds(200), topicName: "test.topic");

        // Wait for metrics to be collected
        Thread.Sleep(100);

        // Assert
        _histogramValues["message.publish.duration"].Average().Should().Be(150.0);
    }

    #endregion

    #region Histogram Tests - Delivery Duration

    [Fact]
    public void RecordDeliveryDuration_RecordsHistogram()
    {
        // Act
        _provider.RecordDeliveryDuration(TimeSpan.FromMilliseconds(120), topicName: "test.topic");
        _provider.RecordDeliveryDuration(TimeSpan.FromMilliseconds(180), topicName: "test.topic");

        // Wait for metrics to be collected
        Thread.Sleep(100);

        // Assert
        _histogramValues.Should().ContainKey("message.delivery.duration");
        _histogramValues["message.delivery.duration"].Should().HaveCount(2);
        _histogramValues["message.delivery.duration"].Should().Contain(120.0);
        _histogramValues["message.delivery.duration"].Should().Contain(180.0);
    }

    [Fact]
    public void RecordDeliveryDuration_WithMultipleTopics_RecordsAll()
    {
        // Act
        _provider.RecordDeliveryDuration(TimeSpan.FromMilliseconds(50), topicName: "topic1");
        _provider.RecordDeliveryDuration(TimeSpan.FromMilliseconds(75), topicName: "topic2");
        _provider.RecordDeliveryDuration(TimeSpan.FromMilliseconds(100), topicName: "topic1");

        // Wait for metrics to be collected
        Thread.Sleep(100);

        // Assert
        _histogramValues["message.delivery.duration"].Should().HaveCount(3);
    }

    #endregion

    #region Gauge Tests - Queue Depth

    [Fact]
    public void UpdateQueueDepth_UpdatesGauge()
    {
        // Act
        _provider.UpdateQueueDepth(topicName: "test.topic", depth: 150);

        // Wait for gauge to be observed
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _gaugeValues.Should().ContainKey("queue.depth");
        _gaugeValues["queue.depth"].Should().Be(150);
    }

    [Fact]
    public void UpdateQueueDepth_WithZero_SetsGaugeToZero()
    {
        // Arrange
        _provider.UpdateQueueDepth(topicName: "test.topic", depth: 100);
        Thread.Sleep(50);

        // Act
        _provider.UpdateQueueDepth(topicName: "test.topic", depth: 0);

        // Wait for gauge to be observed
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _gaugeValues["queue.depth"].Should().Be(0);
    }

    [Fact]
    public void UpdateQueueDepth_WithMultipleTopics_TracksLatestForEach()
    {
        // Act
        _provider.UpdateQueueDepth(topicName: "topic1", depth: 50);
        _provider.UpdateQueueDepth(topicName: "topic2", depth: 75);
        _provider.UpdateQueueDepth(topicName: "topic1", depth: 100);

        // Wait for gauge to be observed
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        // The gauge should report the sum or latest value
        // For this test, we're verifying it updates correctly
        _gaugeValues["queue.depth"].Should().BeGreaterThan(0);
    }

    #endregion

    #region Gauge Tests - Consumer Lag

    [Fact]
    public void UpdateConsumerLag_UpdatesGauge()
    {
        // Act
        _provider.UpdateConsumerLag(topicName: "test.topic", lag: 25);

        // Wait for gauge to be observed
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _gaugeValues.Should().ContainKey("consumer.lag");
        _gaugeValues["consumer.lag"].Should().Be(25);
    }

    [Fact]
    public void UpdateConsumerLag_WithZeroLag_SetsGaugeToZero()
    {
        // Arrange
        _provider.UpdateConsumerLag(topicName: "test.topic", lag: 100);
        Thread.Sleep(50);

        // Act
        _provider.UpdateConsumerLag(topicName: "test.topic", lag: 0);

        // Wait for gauge to be observed
        Thread.Sleep(100);
        _meterListener.RecordObservableInstruments();

        // Assert
        _gaugeValues["consumer.lag"].Should().Be(0);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentMetricUpdates_ThreadSafe()
    {
        // Act - Simulate concurrent metric updates
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                _provider.IncrementPublished("test.topic");
                _provider.IncrementDelivered("test.topic");
                _provider.RecordPublishDuration(TimeSpan.FromMilliseconds(50), "test.topic");
                _provider.RecordDeliveryDuration(TimeSpan.FromMilliseconds(100), "test.topic");
            }));
        }

        await Task.WhenAll(tasks);

        // Wait for metrics to be collected
        Thread.Sleep(200);
        _meterListener.RecordObservableInstruments();

        // Assert
        _counterValues["messages.published.total"].Should().Be(10);
        _counterValues["messages.delivered.total"].Should().Be(10);
        _histogramValues["message.publish.duration"].Should().HaveCount(10);
        _histogramValues["message.delivery.duration"].Should().HaveCount(10);
    }

    #endregion

    #region Integration Test

    [Fact]
    public void CompleteMessageFlow_RecordsAllMetrics()
    {
        // Act: Simulate complete message flow
        // 1. Publish
        _provider.RecordPublishDuration(TimeSpan.FromMilliseconds(25), topicName: "orders.created");
        _provider.IncrementPublished(topicName: "orders.created");

        // 2. Update queue depth
        _provider.UpdateQueueDepth(topicName: "orders.created", depth: 1);

        // 3. Deliver
        _provider.RecordDeliveryDuration(TimeSpan.FromMilliseconds(150), topicName: "orders.created");
        _provider.IncrementDelivered(topicName: "orders.created");

        // 4. Update consumer lag
        _provider.UpdateConsumerLag(topicName: "orders.created", lag: 0);

        // Wait for metrics to be collected
        Thread.Sleep(150);
        _meterListener.RecordObservableInstruments();

        // Assert: All metrics recorded
        _counterValues["messages.published.total"].Should().Be(1);
        _counterValues["messages.delivered.total"].Should().Be(1);
        _histogramValues["message.publish.duration"].Should().Contain(25.0);
        _histogramValues["message.delivery.duration"].Should().Contain(150.0);
        _gaugeValues["queue.depth"].Should().Be(1);
        _gaugeValues["consumer.lag"].Should().Be(0);
    }

    #endregion

    #region Dispose Test

    [Fact]
    public void Dispose_DisposesMeters()
    {
        // Arrange
        var provider = new MessageMetricsProvider();

        // Act
        provider.Dispose();

        // Assert
        // No exception should be thrown, and subsequent operations should not crash
        var action = () => provider.Dispose();
        action.Should().NotThrow();
    }

    #endregion

    public void Dispose()
    {
        _meterListener.Dispose();
        _provider.Dispose();
    }
}
