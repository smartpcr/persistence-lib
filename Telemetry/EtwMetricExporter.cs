//-----------------------------------------------------------------------
// <copyright file="EtwMetricExporter.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Common.Telemetry.Etw
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Linq;
    using OpenTelemetry;
    using OpenTelemetry.Metrics;

    internal sealed class MetricsEventSource : EventSource
    {
        public static readonly MetricsEventSource Log = new("CommonTelemetryMetrics");

        private MetricsEventSource(string name)
            : base(name)
        {
        }

        [Event(1, Level = EventLevel.Informational, Message = "Metric: {0} Value: {1} Tags: {2}")]
        public void Metric(string name, double value, string tags)
        {
            this.WriteEvent(1, name, value, tags);
        }
    }

    public sealed class EtwMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (ref readonly Metric metric in batch)
            {
                foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
                {
                    double value = 0;

                    switch (metric.MetricType)
                    {
                        case MetricType.DoubleSum:
                            point.TryGetSumDouble(out value);
                            break;
                        case MetricType.LongSum:
                            point.TryGetSumLong(out long longValue);
                            value = longValue;
                            break;
                        case MetricType.DoubleGauge:
                            point.TryGetGaugeLastValueDouble(out value);
                            break;
                        case MetricType.LongGauge:
                            point.TryGetGaugeLastValueLong(out long gaugeValue);
                            value = gaugeValue;
                            break;
                        default:
                            continue;
                    }

                    string tags = string.Join(",", point.Tags.Select(t => $"{t.Key}={t.Value}"));
                    MetricsEventSource.Log.Metric(metric.Name, value, tags);
                }
            }

            return ExportResult.Success;
        }
    }

    public static class MeterProviderBuilderExtensions
    {
        public static MeterProviderBuilder AddEtwExporter(this MeterProviderBuilder builder)
        {
            return builder.AddReader(new PeriodicExportingMetricReader(new EtwMetricExporter()));
        }
    }
}
