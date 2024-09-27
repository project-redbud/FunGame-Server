using System.Net.WebSockets;
using Microsoft.AspNetCore.Diagnostics;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Controller;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Utility;
using Milimoe.FunGame.WebAPI.Architecture;

WebAPIListener listener = new();

try
{
    Console.Title = Config.ServerName;
    Console.WriteLine(FunGameInfo.GetInfo(Config.FunGameType));

    ServerHelper.WriteLine("正在读取配置文件并初始化服务 . . .");
    // 初始化命令菜单
    ServerHelper.InitOrderList();

    // 读取游戏模组
    if (!Config.GetGameModuleList())
    {
        ServerHelper.WriteLine("服务器似乎未安装任何游戏模组，请检查是否正确安装它们。");
    }

    // 检查是否存在配置文件
    if (!INIHelper.ExistINIFile())
    {
        ServerHelper.WriteLine("未检测到配置文件，将自动创建配置文件 . . .");
        INIHelper.Init(Config.FunGameType);
        ServerHelper.WriteLine("配置文件FunGame.ini创建成功，请修改该配置文件，然后重启服务器。");
        Console.ReadKey();
        return;
    }
    else
    {
        ServerHelper.GetServerSettings();
    }

    ServerHelper.WriteLine("请输入 help 来获取帮助，输入 quit 关闭服务器。");

    // 创建全局SQLHelper
    Config.InitSQLHelper();

    ServerHelper.WriteLine("正在启动 Web API 监听 . . .");

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

    // 启用 WebSockets 中间件
    WebSocketOptions webSocketOptions = new()
    {
        KeepAliveInterval = TimeSpan.FromMinutes(2) // 设置 WebSocket 的保活间隔
    };
    app.UseWebSockets(webSocketOptions);

    // 路由到 WebSocket 处理器
    app.Map("/ws", WebSocketConnectionHandler);

    // 开始监听连接
    listener.BannedList.AddRange(Config.ServerBannedList);

    if (Config.ServerNotice != "")
        Console.WriteLine("\n\n********** 服务器公告 **********\n\n" + Config.ServerNotice + "\n");
    else
        Console.WriteLine("无法读取服务器公告");

    Task order = Task.Factory.StartNew(GetConsoleOrder);

    app.Run();
}
catch (Exception e)
{
    ServerHelper.Error(e);
}

async Task GetConsoleOrder()
{
    while (true)
    {
        string order = Console.ReadLine() ?? "";
        ServerHelper.Type();
        if (order != "")
        {
            order = order.ToLower();
            switch (order)
            {
                default:
                    await ConsoleModel.Order(listener, order);
                    break;
            }
        }
    }
}

async Task WebSocketConnectionHandler(HttpContext context)
{
    string clientip = "";
    try
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            WebSocket instance = await context.WebSockets.AcceptWebSocketAsync();
            clientip = context.Connection.RemoteIpAddress?.ToString() + ":" + context.Connection.RemotePort;

            Guid token = Guid.NewGuid();
            ServerWebSocket socket = new(listener, instance, clientip, clientip, token);
            Config.ConnectingPlayerCount++;
            bool isConnected = false;
            bool isDebugMode = false;

            // 开始处理客户端连接请求
            IEnumerable<SocketObject> objs = [];
            while (!objs.Any(o => o.SocketType == SocketMessageType.Connect))
            {
                objs = objs.Union(await socket.ReceiveAsync());
            }
            (isConnected, isDebugMode) = await ConnectController.Connect(listener, socket, token, clientip, objs.Where(o => o.SocketType == SocketMessageType.Connect));
            if (isConnected)
            {
                ServerModel<ServerWebSocket> ClientModel = new(listener, socket, isDebugMode);
                ClientModel.SetClientName(clientip);
                await ClientModel.Start();
            }
            else
            {
                ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 连接失败。", InvokeMessageType.Core);
                await socket.CloseAsync();
            }
            Config.ConnectingPlayerCount--;
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    catch (Exception e)
    {
        if (--Config.ConnectingPlayerCount < 0) Config.ConnectingPlayerCount = 0;
        ServerHelper.WriteLine(ServerHelper.MakeClientName(clientip) + " 中断连接！", InvokeMessageType.Core);
        ServerHelper.Error(e);
    }
}
