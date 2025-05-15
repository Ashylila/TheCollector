using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ECommons.EzIpcManager;

namespace TheCollector.Ipc;

internal static class VNavmesh_IPCSubscriber
{
    private static readonly EzIPCDisposalToken[] _disposalTokens =
        EzIPC.Init(typeof(VNavmesh_IPCSubscriber), "vnavmesh", SafeWrapper.IPCException);

    [EzIPC("Nav.IsReady")]
    internal static readonly Func<bool> Nav_IsReady;

    [EzIPC("Nav.BuildProgress")]
    internal static readonly Func<float> Nav_BuildProgress;

    [EzIPC("Nav.Reload")]
    internal static readonly Func<bool> Nav_Reload;

    [EzIPC("Nav.Rebuild")]
    internal static readonly Func<bool> Nav_Rebuild;

    [EzIPC("Nav.Pathfind")]
    internal static readonly Func<Vector3, Vector3, bool, Task<List<Vector3>>> Nav_Pathfind;

    [EzIPC("Nav.PathfindCancelable")]
    internal static readonly Func<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>>
        Nav_PathfindCancelable;

    [EzIPC("Nav.PathfindCancelAll")]
    internal static readonly Action Nav_PathfindCancelAll;

    [EzIPC("Nav.PathfindInProgress")]
    internal static readonly Func<bool> Nav_PathfindInProgress;

    [EzIPC("Nav.PathfindNumQueued")]
    internal static readonly Func<int> Nav_PathfindNumQueued;

    [EzIPC("Nav.IsAutoLoad")]
    internal static readonly Func<bool> Nav_IsAutoLoad;

    [EzIPC("Nav.SetAutoLoad")]
    internal static readonly Action<bool> Nav_SetAutoLoad;

    [EzIPC("Query.Mesh.NearestPoint")]
    internal static readonly Func<Vector3, float, float, Vector3> Query_Mesh_NearestPoint;

    [EzIPC("Query.Mesh.PointOnFloor")]
    internal static readonly Func<Vector3, bool, float, Vector3> Query_Mesh_PointOnFloor;

    [EzIPC("Path.MoveTo")]
    internal static readonly Action<List<Vector3>, bool> Path_MoveTo;

    [EzIPC("Path.Stop")]
    internal static readonly Action Path_Stop;

    [EzIPC("Path.IsRunning")]
    internal static readonly Func<bool> Path_IsRunning;

    [EzIPC("Path.NumWaypoints")]
    internal static readonly Func<int> Path_NumWaypoints;

    [EzIPC("Path.GetMovementAllowed")]
    internal static readonly Func<bool> Path_GetMovementAllowed;

    [EzIPC("Path.SetMovementAllowed")]
    internal static readonly Action<bool> Path_SetMovementAllowed;

    [EzIPC("Path.GetAlignCamera")]
    internal static readonly Func<bool> Path_GetAlignCamera;

    [EzIPC("Path.SetAlignCamera")]
    internal static readonly Action<bool> Path_SetAlignCamera;

    [EzIPC("Path.GetTolerance")]
    internal static readonly Func<float> Path_GetTolerance;

    [EzIPC("Path.SetTolerance")]
    internal static readonly Action<float> Path_SetTolerance;

    [EzIPC("SimpleMove.PathfindAndMoveTo")]
    internal static readonly Func<Vector3, bool, bool> SimpleMove_PathfindAndMoveTo;

    [EzIPC("SimpleMove.PathfindInProgress")]
    internal static readonly Func<bool> SimpleMove_PathfindInProgress;

    [EzIPC("Window.IsOpen")]
    internal static readonly Func<bool> Window_IsOpen;

    [EzIPC("Window.SetOpen")]
    internal static readonly Action<bool> Window_SetOpen;

    [EzIPC("DTR.IsShown")]
    internal static readonly Func<bool> DTR_IsShown;

    [EzIPC("DTR.SetShown")]
    internal static readonly Action<bool> DTR_SetShown;

    internal static bool IsEnabled => IPCSubscriber_Common.IsReady("vnavmesh");

    internal static void Dispose()
    {
        IPCSubscriber_Common.DisposeAll(_disposalTokens);
    }
}