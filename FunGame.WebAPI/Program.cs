using System.Net.WebSockets;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.Tokens;
using Milimoe.FunGame;
using Milimoe.FunGame.Core.Api.Utility;
using Milimoe.FunGame.Core.Library.Common.Addon;
using Milimoe.FunGame.Core.Library.Common.Network;
using Milimoe.FunGame.Core.Library.Constant;
using Milimoe.FunGame.Server.Controller;
using Milimoe.FunGame.Server.Model;
using Milimoe.FunGame.Server.Others;
using Milimoe.FunGame.Server.Services;
using Milimoe.FunGame.WebAPI.Architecture;
using Milimoe.FunGame.WebAPI.Interfaces;
using Milimoe.FunGame.WebAPI.Models;
using Milimoe.FunGame.WebAPI.Services;
using Scalar.AspNetCore;

WebAPIListener listener = new();

try
{
    Console.Title = Config.ServerName;
    Console.WriteLine(FunGameInfo.GetInfo(Config.FunGameType));
    Config.AspNetCore = true;

    ServerHelper.WriteLine("正在读取配置文件并初始化服务 . . .");

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

    // 初始化命令菜单
    ServerHelper.InitOrderList();

    // 初始化 SQLHelper
    FunGameSystem.InitSQLHelper();

    // 初始化 MailSender
    FunGameSystem.InitMailSender();

    // 读取 Server 插件
    FunGameSystem.GetServerPlugins();

    // Add services to the container.
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // 读取 Web API 插件
    object[] otherobjs = [builder];
    FunGameSystem.GetWebAPIPlugins(otherobjs);

    // 读取游戏模组
    if (!FunGameSystem.GetGameModuleList())
    {
        ServerHelper.WriteLine("服务器似乎未安装任何游戏模组，请检查是否正确安装它们。");
    }

    // 创建单例
    RESTfulAPIListener apiListener = new();
    RESTfulAPIListener.Instance = apiListener;

    ServerHelper.WriteLine("请输入 help 来获取帮助，按下 Ctrl+C 关闭服务器。");

    ServerHelper.PrintFunGameTitle();

    if (Config.ServerNotice != "")
        Console.WriteLine("\r \n********** 服务器公告 **********\n\n" + Config.ServerNotice + "\n");
    else
        Console.WriteLine("无法读取服务器公告");

    ServerHelper.WriteLine("正在启动 Web API 监听 . . .");
    Console.WriteLine("\r ");

    // 读取扩展控制器
    if (Config.WebAPIPluginLoader != null)
    {
        foreach (WebAPIPlugin plugin in Config.WebAPIPluginLoader.Plugins.Values)
        {
            Assembly? pluginAssembly = Assembly.GetAssembly(plugin.GetType());

            if (pluginAssembly != null)
            {
                // 注册所有控制器
                builder.Services.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(pluginAssembly));
            }
        }
    }

    // 添加 JSON 转换器
    builder.Services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        foreach (JsonConverter converter in JsonTool.JsonSerializerOptions.Converters)
        {
            options.JsonSerializerOptions.Converters.Add(converter);
        }
    });
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer(new SecurityDocumentTransformer());
    });
    // 添加 CORS 服务
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSpecificOrigin", policy =>
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        });
    });
    // 添加 JWT 认证
    builder.Services.AddScoped<JWTService>();
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "undefined")),
            NameClaimType = ClaimTypes.NameIdentifier
        };
    }).AddScheme<AuthenticationSchemeOptions, CustomBearerAuthenticationHandler>("CustomBearer", options => { });
    builder.Logging.AddConsole(options =>
    {
        options.FormatterName = "CustomFormatter";
    });
    builder.Services.AddSingleton<ConsoleFormatter, CustomConsoleFormatter>();
    // 其他依赖注入
    builder.Services.AddHttpClient();
    builder.Services.AddMemoryCache();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUserContext, HttpUserContext>();
    builder.Services.AddSingleton(listener);
    builder.Services.AddSingleton(apiListener);
    builder.Services.AddScoped(provider =>
    {
        // 从上下文中获取用户名
        IUserContext userContext = provider.GetRequiredService<IUserContext>();
        string username = userContext.Username;

        if (apiListener.UserList.ContainsKey(username))
        {
            RESTfulAPIModel apiModel = (RESTfulAPIModel)apiListener.UserList[username];
            return apiModel;
        }

        throw new NoUserLogonException();
    });

    WebApplication app = builder.Build();

    // 启用 CORS
    app.UseCors("AllowSpecificOrigin");

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseDefaultFiles();

    app.UseStaticFiles();

    app.UseHttpsRedirection();

    app.UseMiddleware<JwtAuthenticationMiddleware>();

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

    // 捕捉关闭程序事件
    IHostApplicationLifetime lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(CloseServer);

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
                objs = await socket.ReceiveAsync();
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

void CloseServer()
{
    FunGameSystem.CloseServer();
}
