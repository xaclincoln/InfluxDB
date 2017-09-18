﻿// <copyright file="InfluxDbMetricsReporter.cs" company="Allan Hardy">
// Copyright (c) Allan Hardy. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics.Filters;
using App.Metrics.Formatters;
using App.Metrics.Formatters.InfluxDB;
using App.Metrics.Logging;
using App.Metrics.Reporting.InfluxDB.Client;

namespace App.Metrics.Reporting.InfluxDB
{
    public class InfluxDbMetricsReporter : IReportMetrics
    {
        private static readonly ILog Logger = LogProvider.For<InfluxDbMetricsReporter>();
        private readonly IMetricsOutputFormatter _defaultMetricsOutputFormatter = new MetricsInfluxDbLineProtocolOutputFormatter();
        private readonly ILineProtocolClient _lineProtocolClient;

        public InfluxDbMetricsReporter(
            MetricsReportingInfluxDbOptions options,
            ILineProtocolClient lineProtocolClient)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.FlushInterval < TimeSpan.Zero)
            {
                throw new InvalidOperationException($"{nameof(MetricsReportingInfluxDbOptions.FlushInterval)} must not be less than zero");
            }

            _lineProtocolClient = lineProtocolClient ?? throw new ArgumentNullException(nameof(lineProtocolClient));

            Formatter = options.MetricsOutputFormatter ?? _defaultMetricsOutputFormatter;

            FlushInterval = options.FlushInterval > TimeSpan.Zero
                ? options.FlushInterval
                : AppMetricsConstants.Reporting.DefaultFlushInterval;

            Filter = options.Filter;

            Logger.Info($"Using Metrics Reporter {this}. Url: {options.InfluxDb.BaseUri + options.InfluxDb.Endpoint} FlushInterval: {FlushInterval}");
        }

        /// <inheritdoc />
        public IFilterMetrics Filter { get; set; }

        /// <inheritdoc />
        public TimeSpan FlushInterval { get; set; }

        /// <inheritdoc />
        public IMetricsOutputFormatter Formatter { get; set; }

        /// <inheritdoc />
        public async Task<bool> FlushAsync(MetricsDataValueSource metricsData, CancellationToken cancellationToken = default)
        {
            Logger.Trace("Flushing metrics snapshot");

            LineProtocolWriteResult result;

            using (var stream = new MemoryStream())
            {
                await Formatter.WriteAsync(stream, metricsData, cancellationToken);

                result = await _lineProtocolClient.WriteAsync(Encoding.UTF8.GetString(stream.ToArray()), cancellationToken);
            }

            if (result.Success)
            {
                Logger.Trace("Flushed metrics snapshot");
                return true;
            }

            Logger.Error(result.ErrorMessage);

            return false;
        }
    }
}