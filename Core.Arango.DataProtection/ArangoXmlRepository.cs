﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Core.Arango.Protocol;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Arango.DataProtection
{
    internal class ArangoXmlRepository : IXmlRepository
    {
        private readonly string _collection;
        private readonly IArangoContext _context;
        private readonly string _database;
        private readonly ILogger _logger;
        private readonly IServiceProvider _services;

        public ArangoXmlRepository(IServiceProvider services, ILoggerFactory loggerFactory,
            string database, string collection, IArangoContext context)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _logger = loggerFactory.CreateLogger<ArangoXmlRepository>();
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _database = database;
            _collection = collection;
            _context = context ?? services.GetRequiredService<ArangoContext>();

            try
            {
                if (!_context.Database.ExistAsync(_database).Result)
                    _context.Database.CreateAsync(_database).Wait();

                var collections = _context.Collection.ListAsync(_database).Result;

                if (!collections.Contains(collection))
                    _context.Collection.CreateAsync(_database, _collection, ArangoCollectionType.Document).Wait();
            }
            catch (Exception e)
            {
                var logger = _logger;
                logger?.LogError(e, e.Message);
            }
        }

        /// <inheritdoc />
        public virtual IReadOnlyCollection<XElement> GetAllElements()
        {
            var logger = _logger;

            return _context.Query.FindAsync<DataProtectionEntity>(_database, _collection, $"true").Result
                .Select(key => TryParseKeyXml(key.Xml, logger))
                .ToList().AsReadOnly();
        }

        /// <inheritdoc />
        public void StoreElement(XElement element, string friendlyName)
        {
            var newKey = new DataProtectionEntity
            {
                FriendlyName = friendlyName,
                Xml = element.ToString(SaveOptions.DisableFormatting)
            };

            _context.Document.CreateAsync(_database, _collection, newKey).Wait();
        }

        private static XElement TryParseKeyXml(string xml, ILogger logger)
        {
            try
            {
                return XElement.Parse(xml);
            }
            catch (Exception e)
            {
                logger?.LogError(e, e.Message);
                return null;
            }
        }
    }
}