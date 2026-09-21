using Microsoft.EntityFrameworkCore;
using Skill_Hub_BackEnd.Data;

namespace Skill_Hub_BackEnd.Services.Implementations
{
    /// <summary>
    /// Background hosted service that solves the NeonDB Serverless cold-start problem.
    ///
    /// PROBLEM:
    ///   NeonDB's free/serverless tier suspends database branches after ~5 minutes of
    ///   inactivity. When the branch wakes, the PostgreSQL startup handshake can take
    ///   3–8 seconds, causing Npgsql to throw:
    ///     "System.TimeoutException: Timeout during reading attempt"
    ///   during the RawOpen / SSL handshake phase — BEFORE any SQL command runs.
    ///
    /// SOLUTION — Two-phase approach:
    ///   1. STARTUP WARMUP: On application start, this service attempts a lightweight
    ///      SELECT 1 query in a retry loop (up to 10 attempts, 3-second intervals)
    ///      until NeonDB is fully awake and responsive.
    ///
    ///   2. KEEP-ALIVE HEARTBEAT: After the initial warmup, a periodic ping runs every
    ///      4 minutes (NeonDB sleeps after 5 min idle) to keep the branch alive during
    ///      the server's operational lifetime.
    /// </summary>
    public sealed class NeonDbWarmupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NeonDbWarmupService> _logger;

        // NeonDB sleeps after 5 min of inactivity — ping every 4 min to prevent it.
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(4);

        // On startup: retry every 3s for up to 10 attempts (30s total warm-up window).
        private const int MaxWarmupAttempts = 10;
        private static readonly TimeSpan WarmupRetryDelay = TimeSpan.FromSeconds(3);

        public NeonDbWarmupService(
            IServiceScopeFactory scopeFactory,
            ILogger<NeonDbWarmupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // ── Phase 1: Startup warm-up (blocking until DB is responsive) ──────────
            await WarmUpAsync(stoppingToken);

            // ── Phase 2: Periodic keep-alive heartbeat ───────────────────────────────
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(HeartbeatInterval, stoppingToken);
                    await PingAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // Graceful shutdown
                }
                catch (Exception ex)
                {
                    // Non-fatal: log and continue — next heartbeat will retry.
                    _logger.LogWarning(ex,
                        "[NeonDbWarmup] Heartbeat ping failed — will retry in {Minutes} minutes.",
                        HeartbeatInterval.TotalMinutes);
                }
            }
        }

        /// <summary>
        /// Retries a lightweight DB ping until NeonDB is responsive or all attempts exhaust.
        /// </summary>
        private async Task WarmUpAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "[NeonDbWarmup] Starting NeonDB cold-start warm-up (max {Max} attempts, {Delay}s delay)...",
                MaxWarmupAttempts, WarmupRetryDelay.TotalSeconds);

            for (int attempt = 1; attempt <= MaxWarmupAttempts; attempt++)
            {
                if (stoppingToken.IsCancellationRequested) return;

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Lightweight ping — just asks the server if it's alive.
                    var isAlive = await db.Database.CanConnectAsync(stoppingToken);

                    if (isAlive)
                    {
                        _logger.LogInformation(
                            "[NeonDbWarmup] ✓ NeonDB is warm and responsive (attempt {Attempt}/{Max}). " +
                            "Heartbeat will run every {Interval} minutes.",
                            attempt, MaxWarmupAttempts, HeartbeatInterval.TotalMinutes);
                        return;
                    }

                    _logger.LogWarning(
                        "[NeonDbWarmup] Attempt {Attempt}/{Max}: CanConnect returned false — retrying in {Delay}s...",
                        attempt, MaxWarmupAttempts, WarmupRetryDelay.TotalSeconds);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "[NeonDbWarmup] Attempt {Attempt}/{Max} failed: {Error} — retrying in {Delay}s...",
                        attempt, MaxWarmupAttempts, ex.Message, WarmupRetryDelay.TotalSeconds);
                }

                if (attempt < MaxWarmupAttempts)
                    await Task.Delay(WarmupRetryDelay, stoppingToken);
            }

            // Exhausted all attempts — log a warning but don't crash the server.
            // EF Core's EnableRetryOnFailure will handle subsequent request-level retries.
            _logger.LogError(
                "[NeonDbWarmup] All {Max} warm-up attempts exhausted. " +
                "NeonDB may still be waking — request-level retries (EnableRetryOnFailure) will compensate.",
                MaxWarmupAttempts);
        }

        /// <summary>Periodic keep-alive ping to prevent NeonDB branch from entering sleep.</summary>
        private async Task PingAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Use a raw SQL ping for maximum efficiency — no ORM overhead.
            await db.Database.ExecuteSqlRawAsync("SELECT 1", stoppingToken);

            _logger.LogDebug("[NeonDbWarmup] Keep-alive heartbeat sent to NeonDB successfully.");
        }
    }
}
