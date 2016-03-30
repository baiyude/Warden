﻿using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Sentry.Core;

namespace Sentry.Watchers.MongoDb
{
    /// <summary>
    /// MongoDbWatcher designed for MongoDB monitoring.
    /// </summary>
    public class MongoDbWatcher : IWatcher
    {
        private readonly MongoDbWatcherConfiguration _configuration;
        private readonly IMongoDbConnection _connection;
        public string Name { get; }

        protected MongoDbWatcher(string name, MongoDbWatcherConfiguration configuration)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Watcher name can not be empty.");

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration),
                    "MongoDB Watcher configuration has not been provided.");
            }

            Name = name;
            _configuration = configuration;
            _connection = configuration.ConnectionProvider(configuration.ConnectionString);
        }

        public async Task<IWatcherCheckResult> ExecuteAsync()
        {
            try
            {
                var database = await _connection.GetDatabaseAsync() ?? _configuration.MongoDbProvider();
                if (database == null)
                {
                    return MongoDbWatcherCheckResult.Create(this, false, _configuration.Database,
                        _configuration.ConnectionString, $"Database: '{_configuration.Database}' has not been found.");
                }
                if (string.IsNullOrWhiteSpace(_configuration.Query))
                {
                    return MongoDbWatcherCheckResult.Create(this, true, _configuration.Database,
                        _configuration.ConnectionString);
                }

                var queryResult = await database.QueryAsync(_configuration.CollectionName, _configuration.Query);
                var isValid = true;
                if (_configuration.EnsureThatAsync != null)
                    isValid = await _configuration.EnsureThatAsync?.Invoke(queryResult);

                isValid = isValid && (_configuration.EnsureThat?.Invoke(queryResult) ?? true);

                return MongoDbWatcherCheckResult.Create(this, isValid, _configuration.Database,
                    _configuration.ConnectionString, _configuration.Query, queryResult);
            }
            catch (MongoException ex)
            {
                return MongoDbWatcherCheckResult.Create(this, false, _configuration.Database,
                    _configuration.ConnectionString, ex.Message);
            }
            catch (Exception ex)
            {
                throw new WatcherException("There was an error while trying to access the MongoDB.", ex);
            }
        }

        /// <summary>
        /// Factory method for creating a new instance of MongoDbWatcher.
        /// </summary>
        /// <param name="name">Name of the MongoDbWatcher.</param>
        /// <param name="database">Name of the MongoDB database.</param>
        /// <param name="connectionString">Connection string of the MongoDB server.</param>
        /// <param name="timeout">Optional timeout of the MongoDB query (5 seconds by default).</param>
        /// <param name="configurator">Optional lambda expression for configuring the MongoDbWatcher.</param>
        /// <returns>Instance of MongoDbWatcher.</returns>
        public static MongoDbWatcher Create(string name, string connectionString, string database,
            TimeSpan? timeout = null, Action<MongoDbWatcherConfiguration.Default> configurator = null)
        {
            var config = new MongoDbWatcherConfiguration.Builder(connectionString, database, timeout);
            configurator?.Invoke((MongoDbWatcherConfiguration.Default) config);

            return Create(name, config.Build());
        }

        /// <summary>
        /// Factory method for creating a new instance of MongoDbWatcher.
        /// </summary>
        /// <param name="name">Name of the MongoDbWatcher.</param>
        /// <param name="configuration">Configuration of MongoDbWatcher.</param>
        /// <returns>Instance of MongoDbWatcher.</returns>
        public static MongoDbWatcher Create(string name, MongoDbWatcherConfiguration configuration)
            => new MongoDbWatcher(name, configuration);
    }
}