using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

public class SchedulerService : BackgroundService
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly IDbContextFactory<TechtonicCmsDbContext> _dbContextFactory;

    public SchedulerService(ILogger<SchedulerService> logger, IDbContextFactory<TechtonicCmsDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(stoppingToken);

            var scheduledItems = await (
                from se in dbContext.EntrySchedules
                where se.ScheduledTime <= DateTime.UtcNow && !se.AlreadyExecuted
                join e in dbContext.Entries on se.EntryId equals e.Id
                select new { Schedule = se, Entry = e }
            ).ToListAsync(stoppingToken);

            foreach (var item in scheduledItems)
            {
                switch (item.Schedule.Action)
                {
                    case ScheduledAction.Publish:
                        item.Entry.Status = EntryStatus.Published;
                        item.Entry.PublishedAt ??= item.Schedule.ScheduledTime;
                        break;
                    case ScheduledAction.Unpublish:
                        item.Entry.Status = EntryStatus.Draft;
                        item.Entry.PublishedAt = null;
                        break;
                    case ScheduledAction.Archive:
                        item.Entry.Status = EntryStatus.Archived;
                        break;
                    case ScheduledAction.Restore:
                        item.Entry.Status = EntryStatus.Draft;
                        break;
                    case ScheduledAction.Delete:
                        item.Entry.Status = EntryStatus.Deleted;
                        break;
                }

                item.Entry.UpdatedAt = DateTime.UtcNow;
                item.Schedule.AlreadyExecuted = true;

                dbContext.EntrySchedules.Update(item.Schedule);
                dbContext.Entries.Update(item.Entry);

                await dbContext.SaveChangesAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}