using Kernel.Extensions;
using Kernel.Modules;

var builder = WebApplication.CreateBuilder(args);

builder.AddKernel();
builder.AddModules();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();