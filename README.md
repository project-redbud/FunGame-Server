# 项目简介

FunGameServer 是 [FunGame](https://github.com/project-redbud/FunGame-Core) 的服务器端实现，基于 ASP.NET Core Web API，轻量、高性能、跨平台。

它支持多种服务模式，使得服务器端的扩展和集成更加灵活：

- Socket 服务：支持传统的 TCP 通信。
- WebSocket 服务：支持基于 Web 的实时通信。
- WebAPI 服务：提供标准的 RESTful API 接口。

# 使用

本项目将构建两个服务器端程序：`FunGameServer` 和 `FunGameWebAPI`。

FunGameServer 提供 Socket 和 WebSocket 的通信服务，但只允许开启其中一种，不能同时开启。

FunGameWebAPI 提供 WebSocket 和 RESTful API 共存的服务，共享数据处理。

# 插件和模组集成

实现 FunGame 的 `GameModuleServer` 和使用基于 ASP.NET Core Web API 的 `Controller` 可以轻松扩展服务器功能。

## Modules 和 GameModuleServer

具体的实现可以参考示例代码：[示例代码](https://github.com/project-redbud/FunGame-Core/tree/master/Library/Common/Addon/Example)

此类 DLL 需要放在服务器编译目录的 `Modules` 文件夹下，需要连同依赖的 DLL 一起复制。

## Web API Controller

集成 ASP.NET Core Controller 的方式：

- 新建一个 ASP.NET Core 类库项目，新建一个类，继承并实现 `WebAPIPlugin`；
- 新建一个 ASP.NET Core Controller 类；
- 编译项目为 DLL 后，放入服务器编译目录的 `Plugins` 文件夹下，需要连同依赖的 DLL 一起复制；
- 启动 `FunGameWebAPI.exe`，`FunGameServer.exe` 没有这个功能。

# 文档

需要更多帮助请查看 [FunGame 开发文档](https://project-redbud.github.io/)，此文档不保证及时更新。
