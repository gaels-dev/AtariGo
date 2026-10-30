using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

using AtariGo.Server.Data;

namespace AtariGo.Server.Configuration
{
    public sealed class DatabaseReadinessCheck
    {
        private const int CommandTimeoutSeconds = 10;
        private const int SchemaProbeRowLimit = 0;

        private readonly AtariGoDbContext _dbContext;

        public DatabaseReadinessCheck(AtariGoDbContext dbContext)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            _dbContext = dbContext;
        }

        public async Task<int> CheckAsync(CancellationToken cancellationToken)
        {
            int checkedTables = 0;
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                foreach (IEntityType entityType in _dbContext.Model.GetEntityTypes())
                {
                    await CheckTableAsync(entityType, cancellationToken);
                    checkedTables++;
                }
            }
            finally
            {
                await _dbContext.Database.CloseConnectionAsync();
            }

            return checkedTables;
        }

        private async Task CheckTableAsync(
            IEntityType entityType,
            CancellationToken cancellationToken)
        {
            string tableName = entityType.GetTableName()
                ?? throw new InvalidOperationException("A mapped SQL table is required.");
            string? schema = entityType.GetSchema();
            StoreObjectIdentifier table = StoreObjectIdentifier.Table(tableName, schema);
            ISqlGenerationHelper sqlHelper = _dbContext.GetService<ISqlGenerationHelper>();
            string columns = string.Join(", ", entityType.GetProperties()
                .Select(property => property.GetColumnName(table))
                .OfType<string>()
                .Distinct()
                .Select(column => sqlHelper.DelimitIdentifier(column)));

            // TOP (0) validates mapped columns and SELECT access without fetching records.
            await using DbCommand command = _dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandTimeout = CommandTimeoutSeconds;
            command.CommandText = $"SELECT TOP ({SchemaProbeRowLimit}) {columns} FROM "
                + sqlHelper.DelimitIdentifier(tableName, schema);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
