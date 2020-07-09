using MongoDB.Driver;
using StreamActions.Database;
using StreamActions.Database.Documents.Commands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace StreamActions
{
    internal static class MiscScheduledTasks
    {
        #region Internal Fields

        internal static ConcurrentDictionary<Guid, CommandCooldownDocument> _changedCooldowns = new ConcurrentDictionary<Guid, CommandCooldownDocument>();

        #endregion Internal Fields

        #region Internal Methods

        internal static void Schedule() => ScheduleCommandCooldownCleanupAndSave();

        #endregion Internal Methods

        #region Private Methods

        private static void ScheduleCommandCooldownCleanupAndSave() => Scheduler.AddJob(async () =>
            {
                foreach (KeyValuePair<Guid, CommandCooldownDocument> kvp in _changedCooldowns)
                {
                    IMongoCollection<CommandDocument> commands = DatabaseClient.Instance.MongoDatabase.GetCollection<CommandDocument>(CommandDocument.CollectionName);

                    FilterDefinition<CommandDocument> filter = Builders<CommandDocument>.Filter.Where(c => c.Id.Equals(kvp.Key));

                    if (await commands.CountDocumentsAsync(filter).ConfigureAwait(false) > 0)
                    {
                        kvp.Value.CleanupUserCooldowns(false);

                        using IAsyncCursor<CommandDocument> cursor = await commands.FindAsync(filter).ConfigureAwait(false);
                        CommandDocument commandDocument = await cursor.SingleAsync().ConfigureAwait(false);

                        UpdateDefinition<CommandDocument> update = Builders<CommandDocument>.Update.Set(c => c.Cooldowns, kvp.Value);

                        _ = await commands.UpdateOneAsync(filter, update).ConfigureAwait(false);
                    }

                    _ = _changedCooldowns.TryRemove(kvp.Key, out _);
                }
            }, s => s.WithName("CommandCooldownCleanupAndSave").ToRunNow().AndEvery(15).Minutes());

        #endregion Private Methods
    }
}