// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Galaxy.Platform.ILogging;

namespace Vega.AutoTest.Spike;

/// <summary>
/// 测试宿主用的日志服务替身。
///
/// 需要它的原因：Galaxy 的 XActivator 静态构造要求 ILogService 已在容器中注册，
/// 而真实的 Galaxy.Platform.Logging.LogService 在测试进程内初始化会一直阻塞
/// （实测超过 8 分钟未返回）。这里用空实现满足注册要求。
/// </summary>
public sealed class StubLogService : ILogService
{
#pragma warning disable CS0067 // 替身不触发该事件
    public event LogNotificationHandler? LogNotificationEvent;
#pragma warning restore CS0067

    public void Verbose(string? p0, string? p1) { }

    public void Verbose(string? p0, string? p1, bool p2, string p3) { }

    public void Debug(string? p0, string? p1) { }

    public void Debug(string? p0, string? p1, bool p2, string p3) { }

    public void Debug(int p0, string? p1, string? p2) { }

    public void Information(string? p0, string? p1) { }

    public void Information(string? p0, string? p1, bool p2, string p3) { }

    public void Warning(string? p0, string? p1) { }

    public void Warning(string? p0, string? p1, bool p2, string p3) { }

    public void Error(string? p0, string? p1) { }

    public void Error(string? p0, string? p1, bool p2, string p3) { }

    public void Fatal(string? p0, string? p1) { }

    public void Fatal(string? p0, string? p1, bool p2, string p3) { }

    public void StackTracedWarning(string? p0, string? p1, string? p2) { }

    public void StackTracedWarning(string? p0, string? p1, string? p2, bool p3, string p4) { }

    public void StackTracedWarning(string? p0, string? p1, Exception p2) { }

    public void StackTracedWarning(string? p0, string? p1, Exception p2, bool p3, string p4) { }

    public void StackTracedError(string? p0, string? p1, string? p2) { }

    public void StackTracedError(string? p0, string? p1, string? p2, bool p3, string p4) { }

    public void StackTracedError(string? p0, string? p1, Exception p2) { }

    public void StackTracedError(string? p0, string? p1, Exception p2, bool p3, string p4) { }

    public void StackTracedFatal(string? p0, string? p1, string? p2) { }

    public void StackTracedFatal(string? p0, string? p1, string? p2, bool p3, string p4) { }

    public void StackTracedFatal(string? p0, string? p1, Exception p2) { }

    public void StackTracedFatal(string? p0, string? p1, Exception p2, bool p3, string p4) { }

    public bool IsDebugEnabled(int p0) => false;

    public void SetDebugMask(int p0) { }

    public void EnableDebugModules(int p0) { }

    public void DisableDebugModules(int p0) { }

    public int GetDebugMask() => 0;

    public Task ConnectNowAsync() => Task.CompletedTask;

    public Task FlushAsync(CancellationToken p0) => Task.CompletedTask;
}
