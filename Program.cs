using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Quartz;

namespace QuartzHealthCheck
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddTransient<BackgroundJob>();
            builder.Services.AddQuartz();
            builder.Services.AddQuartzHostedService(quartzOptions => quartzOptions.WaitForJobsToComplete = true);
            var scheduler = Task.Run(() => MyScheduler.GetScheduler()).Result;
            builder.Services.AddSingleton(scheduler);
            builder.Services.AddHealthChecks()
                .AddCheck<QuartzHealthCheck>("QuartzHealthCheck");

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapRazorPages();
            app.MapHealthChecks("/hc", new HealthCheckOptions()
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
            app.Run();
        }

        public class BackgroundJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                await Console.Out.WriteLineAsync("Executing background job");
            }
        }

        public static class MyScheduler
        {
            public static async Task<IScheduler> GetScheduler()
            {
                var schedulerFactory = SchedulerBuilder.Create().Build();
                var scheduler = await schedulerFactory.GetScheduler();

                var job = JobBuilder.Create<BackgroundJob>()
                    .WithIdentity(name: "BackgroundJob", group: "TriggerGroup")
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(name: "RepeatingTrigger", group: "TriggerGroup")
                    .WithSimpleSchedule(o => o
                    .RepeatForever()
                    .WithIntervalInSeconds(1))
                    .Build();

                await scheduler.ScheduleJob(job, trigger);
                await scheduler.Start();

                return scheduler;
            }
        }

        public class QuartzHealthCheck : IHealthCheck
        {
            private readonly IScheduler _scheduler;
            public QuartzHealthCheck(IScheduler scheduler)
            {
                _scheduler = scheduler;
            }

            public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            {
                var result = await _scheduler.IsJobGroupPaused("TriggerGroup");

                if (!result)
                {
                    return HealthCheckResult.Healthy();
                }

                return HealthCheckResult.Unhealthy();
            }
        }
    }
}