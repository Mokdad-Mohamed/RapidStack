using RapidStack.AutoDI;
using RapidStack.AutoEndpoint;
using RapidStack.SampleApp.Modules.Users;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAutoEndpointsSwagger();
builder.Services.AddScoped<UserService>(); // Register your services

// 👇 Auto DI from current assembly
builder.Services.AddRapidStackAutoDI(Assembly.GetExecutingAssembly());
builder.Services.AddSingleton<ISchemaGenerator, SchemaGenerator>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapAutoEndpoints();
app.Run();
