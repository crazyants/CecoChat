﻿using System.Diagnostics;
using System.Reflection;

namespace CecoChat.Data.History.Telemetry;

internal static class HistoryInstrumentation
{
    private static readonly string ActivitySourceName = "OpenTelemetry.Instrumentation.CecoChat.HistoryDB";
    private static readonly AssemblyName AssemblyName = typeof(HistoryInstrumentation).Assembly.GetName();
    private static readonly Version ActivitySourceVersion = AssemblyName.Version!;

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName, ActivitySourceVersion.ToString());

    public static class Operations
    {
        public const string AddDataMessage = "HistoryDB.AddDataMessage";
        public const string GetHistory = "HistoryDB.GetHistory";
        public const string SetReaction = "HistoryDB.SetReaction";
        public const string UnsetReaction = "HistoryDB.UnsetReaction";
    }
}