using Fistix.TaskManager.AiJobsService.Sagas;
using Fistix.TaskManager.ApiHost.Shared.Extensions;
using Fistix.TaskManager.DataLayer;
using Fistix.TaskManager.DataLayer.Sagas;
using Fistix.TaskManager.ServiceLayer;
using Fistix.TaskManager.ViewModel.Validators.Todos;
using FluentValidation.AspNetCore;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
var masterConfig = builder.Configuration.BindMasterConfig();
builder.Services.AddControllers().AddFluentValidation(x => x.RegisterValidatorsFromAssemblyContaining<CreateTodoTaskCommandValidator>());
builder.Services.AddMicroserviceCommon(
    masterConfig,
    builder.Configuration,
    ServiceLayerHostOptions.AiJobsOnly,
    enableBusOutbox: true,
    enableBusNotifiers: true,
    enableDelayedMessageScheduler: true,
    configureBus: x =>
    {
        x.AddSagaStateMachine<SprintApproveStateMachine, SprintApproveState>()
            .EntityFrameworkRepository(r =>
            {
                r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                r.ExistingDbContext<WriteEfContext>();
                r.UsePostgres();
            });
    });

var app = builder.Build();
app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment()) Startup.ApplyMigrations(app.Services);
app.UseHttpsRedirection();
app.UseMicroservicePipeline(masterConfig);
app.MapControllers();
await app.RunAsync();
