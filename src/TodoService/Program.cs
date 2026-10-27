using Fistix.TaskManager.ApiHost.Shared.Extensions;
using Fistix.TaskManager.DataLayer;
using Fistix.TaskManager.ServiceLayer;
using Fistix.TaskManager.ServiceLayer.Messaging;
using Fistix.TaskManager.ViewModel.Validators.Todos;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
var masterConfig = builder.Configuration.BindMasterConfig();
builder.Services.AddControllers().AddFluentValidation(x => x.RegisterValidatorsFromAssemblyContaining<CreateTodoTaskCommandValidator>());
builder.Services.AddMicroserviceCommon(
    masterConfig,
    builder.Configuration,
    ServiceLayerHostOptions.TodoOnly,
    enableBusOutbox: true,
    enableBusNotifiers: true,
    configureBus: x => x.AddConsumer<CreateSprintFromProposalConsumer>());

var app = builder.Build();
app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment()) Startup.ApplyMigrations(app.Services);
app.UseHttpsRedirection();
app.UseMicroservicePipeline(masterConfig);
app.MapControllers();
await app.RunAsync();
