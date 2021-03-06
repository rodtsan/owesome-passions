﻿using Microsoft.EntityFrameworkCore;
using PinoyCode.Data.Infrustracture;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using PinoyCode.Data.Extensions;
using System.Xml.Serialization;
using CqrsModels = PinoyCode.Cqrs.Models;

namespace PinoyCode.Cqrs
{
    /// <summary>
    /// This is a simple example implementation of an event store, using a SQL database
    /// to provide the storage. Tested and known to work with SQL Server.
    /// </summary>
    public class SqlEventStore : IEventStore
    {
        private readonly IDbContext _context;
        public SqlEventStore(IDbContext context)
        {
            _context = context;
        }

        public IEnumerable LoadEventsFor<TAggregate>(Guid id)
        {
            foreach (var evt in this.Events.Where(p => p.AggregateId == id)
                .OrderBy(o => o.SequenceNumber))
            {
                yield return DeserializeEvent(evt.Type, evt.Body);
            }

        }

        private object DeserializeEvent(string typeName, string data)
        {
            var ser = new XmlSerializer(Type.GetType(typeName));
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(data));
            ms.Seek(0, SeekOrigin.Begin);
            return ser.Deserialize(ms);
        }

        public void SaveEventsFor<TAggregate>(Guid? aggregateId, int eventsLoaded, ArrayList newEvents)
        {
            var aggregate = this.Aggregates.Find(aggregateId.Value);
            if (aggregate == null)
            {
                aggregate = new CqrsModels.Aggregate
                {
                    Id = aggregateId.Value,
                    AggregateType = typeof(TAggregate).AssemblyQualifiedName,
                    CommitDateTime = DateTime.UtcNow
                };

                Aggregates.Add(aggregate);
            }

            for (int i = 0; i < newEvents.Count; i++)
            {
                var e = newEvents[i];
                eventsLoaded = eventsLoaded + i;
                Events.Add(new CqrsModels.Event
                {
                    Id = Guid.NewGuid(),
                    AggregateId = aggregate.Id,
                    SequenceNumber = eventsLoaded,
                    Type = e.GetType().AssemblyQualifiedName,
                    Body = SerializeEvent(e),
                    CommitDateTime = aggregate.CommitDateTime
                });
            }

            

            _context.Commit();

        }

        private string SerializeEvent(object obj)
        {
            var ser = new XmlSerializer(obj.GetType());
            var ms = new MemoryStream();
            ser.Serialize(ms, obj);
            ms.Seek(0, SeekOrigin.Begin);
            return new StreamReader(ms).ReadToEnd();
        }

        public DbSet<CqrsModels.Aggregate> Aggregates
        {
            get
            {
                return _context.Table<CqrsModels.Aggregate>();
            }
        }

        public DbSet<CqrsModels.Event> Events
        {
            get
            {
                return _context.Table<CqrsModels.Event>();
            }
        }

        public IDbContext Context
        {
            get
            {
                return _context;
            }
        }
    }
}
