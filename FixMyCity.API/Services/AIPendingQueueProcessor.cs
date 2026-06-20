// FixMyCity.API/Services/AIPendingQueueProcessor.cs
// Background service: polls AIPendingScoreQueue and retries AI scoring
// for complaints that could not be scored when the AI service was offline.
// Runs every 5 minutes; gives up after MAX_ATTEMPTS failures per complaint.

using FixMyCity.DAL.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;

namespace FixMyCity.API.Services
{
    public class AIPendingQueueProcessor : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AIPendingQueueProcessor> _logger;
        private const int MAX_ATTEMPTS = 5;
        private const int POLL_SECONDS = 300;  // 5 minutes

        public AIPendingQueueProcessor(IServiceScopeFactory scopeFactory,
                                       ILogger<AIPendingQueueProcessor> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AIPendingQueueProcessor started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQueueAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AIPendingQueueProcessor: error during poll.");
                }

                await Task.Delay(TimeSpan.FromSeconds(POLL_SECONDS), stoppingToken);
            }
        }

        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context   = scope.ServiceProvider.GetRequiredService<FixMyCityDbContext>();
            // Phase 5.4 — AiService is scoped (holds DbContext) so we resolve
            // it from this iteration's scope, never as a captured field on
            // the singleton hosted service. See risk_analysis.md R6.
            var aiService = scope.ServiceProvider.GetRequiredService<AiService>();

            // ISSUE 4 FIX: PendingQueueRow is internal and not registered in the EF model.
            // SqlQueryRaw<PendingQueueRow> throws InvalidOperationException at runtime.
            // Use raw ADO.NET instead — same pattern as AdminRepository.GetPlatformStats.
            var items = await FetchQueueItemsAsync(context, ct);

            if (items.Count == 0) return;

            _logger.LogInformation("AI retry queue: {count} item(s) pending.", items.Count);

            bool aiOnline = await aiService.IsHealthyAsync(ct);
            if (!aiOnline)
            {
                _logger.LogWarning("AI service offline — retry deferred.");
                return;
            }

            foreach (var item in items)
            {
                try
                {
                    // Deterministic — no external AI call. Always available.
                    await aiService.ScoreComplaintAsync(
                        item.ComplaintId, item.CategoryId,
                        item.Criticality, item.LocalityId, item.DeptId,
                        ct: ct);

                    await context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM dbo.AIPendingScoreQueue WHERE QueueId = @p0",
                        item.QueueId);

                    _logger.LogInformation("Retry scoring succeeded for complaint {id}.", item.ComplaintId);
                }
                catch (Exception ex)
                {
                    var msg = ex.Message[..Math.Min(500, ex.Message.Length)];
                    await context.Database.ExecuteSqlRawAsync(
                        @"UPDATE dbo.AIPendingScoreQueue
                          SET AttemptCount = AttemptCount + 1,
                              LastAttempt  = SYSDATETIME(),
                              ErrorMessage = @p0
                          WHERE QueueId = @p1",
                        msg, item.QueueId);
                }
            }
        }

        /// <summary>
        /// Reads pending queue rows via raw ADO.NET.
        /// Connection is closed after reading so EF's SessionContextInterceptor
        /// runs cleanly on subsequent ExecuteSqlRawAsync calls.
        /// </summary>
        private static async Task<List<PendingQueueRow>> FetchQueueItemsAsync(
            FixMyCityDbContext context, CancellationToken ct)
        {
            var items = new List<PendingQueueRow>();
            var conn = context.Database.GetDbConnection();
            bool wasOpen = conn.State == ConnectionState.Open;

            if (!wasOpen)
                await conn.OpenAsync(ct);

            try
            {
                // Defensive: this raw ADO path bypasses SessionContextInterceptor.
                // Once RLS is re-enabled (currently STATE = OFF in 00_Schema_Sprint2.sql),
                // an unset SESSION_CONTEXT('UserRole') would cause the JOIN onto
                // dbo.Complaints to read zero rows. Mirror AdminRepository.GetPlatformStats
                // and pin the session to SuperAdmin so the queue is always visible.
                using (var sessionCmd = conn.CreateCommand())
                {
                    sessionCmd.CommandText =
                        "EXEC sp_set_session_context N'UserRole', N'SuperAdmin', @read_only = 0;";
                    await sessionCmd.ExecuteNonQueryAsync(ct);
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT q.QueueId, q.ComplaintId, q.AttemptCount,
                           c.CategoryId, c.Criticality, c.LocalityId, c.DeptId
                    FROM   dbo.AIPendingScoreQueue q
                    INNER JOIN dbo.Complaints c ON c.ComplaintId = q.ComplaintId
                    WHERE  q.AttemptCount < @max";

                var p = cmd.CreateParameter();
                p.ParameterName = "@max";
                p.Value = MAX_ATTEMPTS;
                cmd.Parameters.Add(p);

                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    items.Add(new PendingQueueRow
                    {
                        QueueId = reader.GetInt32(0),
                        ComplaintId = reader.GetInt32(1),
                        AttemptCount = reader.GetByte(2),
                        CategoryId = reader.GetInt16(3),
                        Criticality = reader.GetString(4),
                        LocalityId = reader.GetInt32(5),
                        DeptId = reader.IsDBNull(6) ? null : (int?)reader.GetInt32(6)
                    });
                }
            }
            finally
            {
                // Return connection to pool so EF opens a fresh connection (with interceptor)
                // for subsequent ExecuteSqlRawAsync calls.
                if (!wasOpen)
                    conn.Close();
            }

            return items;
        }
    }

    internal class PendingQueueRow
    {
        public int QueueId { get; set; }
        public int ComplaintId { get; set; }
        public byte AttemptCount { get; set; }
        public short CategoryId { get; set; }
        public string Criticality { get; set; }
        public int LocalityId { get; set; }
        public int? DeptId { get; set; }
    }
}