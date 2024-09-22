using Microsoft.AspNetCore.Diagnostics;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;

try
{
    Console.Title = Config.ServerName;
    Console.WriteLine(FunGameInfo.GetInfo(Config.FunGameType));

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    // 添加 CORS 服务
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSpecificOrigin", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    WebApplication app = builder.Build();

    // 启用 CORS
    app.UseCors("AllowSpecificOrigin");

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            IExceptionHandlerFeature? contextFeature = context.Features.Get<IExceptionHandlerFeature>();
            if (contextFeature != null)
            {
                await context.Response.WriteAsync(new
                {
                    context.Response.StatusCode,
                    Message = "Internal Server Error.",
                    Detailed = contextFeature.Error.Message
                }.ToString() ?? "");
            }
        });
    });

    app.Run();
}
catch (Exception e)
{
    ServerHelper.Error(e);
}