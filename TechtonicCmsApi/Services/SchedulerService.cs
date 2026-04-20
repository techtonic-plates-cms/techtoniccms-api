using Microsoft.EntityFrameworkCore;
using TechtonicCmsApi.Contexts;
using TechtonicCmsApi.Schema.TechtonicCms.Enums;

public class SchedulerService : IHostedService, IDisposable
{
    private readonly ILogger<SchedulerService> _logger;
    private readonly IDbContextFactory<TechtonicCmsDbContext> _dbContextFactory;


    public SchedulerService(ILogger<SchedulerService> logger, IDbContextFactory<TechtonicCmsDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var scheduledItems = from se in dbContext.EntrySchedules
                where se.ScheduledTime <= DateTime.UtcNow && !se.AlreadyExecuted
                join e in dbContext.Entries on se.EntryId equals e.Id
                select new { Schedule = se, Entry = e };


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

                await dbContext.SaveChangesAsync();
            }

            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }

        
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }


    public void Dispose()
    {
        
    }

}