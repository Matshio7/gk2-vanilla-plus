using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Profiling;
using UnityEngine;

namespace GK2Tweaks
{
    // Messwerte fuer die FPS-Anzeige. Teure Abfragen (Prozess, GPU-Last) laufen in einem Hintergrund-Thread,
    // damit die Anzeige selbst keine Ruckler verursacht. Alles wird nur gemessen, wenn es auch angezeigt wird.
    internal static class OverlayStats
    {
        internal static volatile float CpuPercent = -1f;   // Prozess-CPU, normiert auf alle Kerne
        internal static volatile float GpuPercent = -1f;   // GPU-Last 3D (Windows-Leistungsindikatoren), -1 = nicht verfuegbar
        internal static long RamBytes = -1;                // Arbeitsspeicher des Spiels
        internal static double GpuFrameMs = -1, CpuFrameMs = -1;

        private static Thread thread;
        private static volatile bool wantCpu, wantGpu, wantRam, wantVram, running;
        private static ProfilerRecorder gfxRecorder;
        private static bool frameTimingChecked, frameTimingOn;
        private static readonly FrameTiming[] timings = new FrameTiming[1];

        internal static long VramDxgi = -1, VramBudget = -1;
        private static ProfilerRecorder sysRecorder;

        // VRAM: bevorzugt echte Belegung laut Windows (DXGI), sonst Unitys Schaetzung
        internal static long VramBytes => Interlocked.Read(ref VramDxgi) > 0 ? Interlocked.Read(ref VramDxgi) : (gfxRecorder.Valid && gfxRecorder.LastValue > 0 ? gfxRecorder.LastValue : -1);

        internal static long RamUsed
        {
            get
            {
                if (sysRecorder.Valid && sysRecorder.LastValue > 0) return sysRecorder.LastValue;
                return Interlocked.Read(ref RamBytes);
            }
        }

        internal static void Configure(bool active, bool cpu, bool gpu, bool ram, bool vram, bool frameTime)
        {
            wantCpu = active && cpu;
            wantGpu = active && gpu;
            wantRam = active && ram;
            wantVram = active && vram;
            bool needThread = wantCpu || wantGpu || wantRam || wantVram;
            if (wantRam && !sysRecorder.Valid) { try { sysRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory"); } catch { } }
            else if (!wantRam && sysRecorder.Valid) sysRecorder.Dispose();
            if (needThread && !running)
            {
                running = true;
                thread = new Thread(Worker) { IsBackground = true, Name = "GK2Tweaks overlay", Priority = System.Threading.ThreadPriority.BelowNormal };
                thread.Start();
            }
            else if (!needThread) running = false;

            if (active && vram && !gfxRecorder.Valid)
            {
                try { gfxRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory"); } catch { }
            }
            else if (!(active && vram) && gfxRecorder.Valid) gfxRecorder.Dispose();

            if (active && (gpu || frameTime))
            {
                if (!frameTimingChecked)
                {
                    frameTimingChecked = true;
                    try { frameTimingOn = FrameTimingManager.IsFeatureEnabled(); } catch { frameTimingOn = false; }
                }
                if (frameTimingOn)
                {
                    try
                    {
                        FrameTimingManager.CaptureFrameTimings();
                        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                        {
                            GpuFrameMs = timings[0].gpuFrameTime;
                            CpuFrameMs = timings[0].cpuFrameTime;
                        }
                    }
                    catch { frameTimingOn = false; }
                }
            }
        }

        private static void Worker()
        {
            Process proc = null;
            TimeSpan lastCpu = TimeSpan.Zero;
            DateTime lastAt = DateTime.MinValue;
            Pdh gpu = null;
            Dxgi dxgi = null;
            bool dxgiFailed = false;
            int cores = Math.Max(1, Environment.ProcessorCount);
            while (running)
            {
                try
                {
                    if (wantCpu || wantRam)
                    {
                        if (proc == null) proc = Process.GetCurrentProcess();
                        proc.Refresh();
                        if (wantRam) Interlocked.Exchange(ref RamBytes, proc.WorkingSet64);
                        if (wantCpu)
                        {
                            TimeSpan cpu = proc.TotalProcessorTime;
                            DateTime now = DateTime.UtcNow;
                            if (lastAt != DateTime.MinValue)
                            {
                                double wall = (now - lastAt).TotalMilliseconds;
                                if (wall > 0) CpuPercent = (float)Math.Min(100.0, (cpu - lastCpu).TotalMilliseconds / (wall * cores) * 100.0);
                            }
                            lastCpu = cpu; lastAt = now;
                        }
                    }
                    if (wantVram && !dxgiFailed)
                    {
                        try
                        {
                            if (dxgi == null) dxgi = new Dxgi();
                            if (dxgi.Query(out long used, out long budget)) { Interlocked.Exchange(ref VramDxgi, used); Interlocked.Exchange(ref VramBudget, budget); }
                            else dxgiFailed = true;
                        }
                        catch (Exception e) { dxgiFailed = true; Plugin.Log.LogInfo("Overlay: DXGI VRAM query not available (" + e.Message + ")"); }
                    }
                    if (wantGpu && !WineFix.IsWine)
                    {
                        if (gpu == null) gpu = new Pdh();
                        GpuPercent = gpu.Sample3D();
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning("Overlay stats: " + e.Message);
                    wantGpu = false;
                }
                Thread.Sleep(1000);
            }
            gpu?.Dispose();
        }

        // VRAM-Belegung des Spiels laut Windows: IDXGIAdapter3::QueryVideoMemoryInfo (wie im Task-Manager "Dedizierter GPU-Speicher")
        private sealed class Dxgi
        {
            [DllImport("dxgi.dll")] private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumAdapters1Fn(IntPtr self, uint index, out IntPtr adapter);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetDesc1Fn(IntPtr self, IntPtr desc);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryInterfaceFn(IntPtr self, ref Guid riid, out IntPtr obj);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint ReleaseFn(IntPtr self);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryVideoMemoryInfoFn(IntPtr self, uint node, int group, IntPtr info);

            private readonly IntPtr adapter3;
            private readonly QueryVideoMemoryInfoFn query;

            private static T Fn<T>(IntPtr com, int slot) where T : class =>
                Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(com), slot * IntPtr.Size), typeof(T)) as T;

            internal Dxgi()
            {
                Guid factoryId = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
                Guid adapter3Id = new Guid("645967A4-1392-4310-A798-8053CE3E93FD");
                if (CreateDXGIFactory1(ref factoryId, out IntPtr factory) != 0) throw new Exception("CreateDXGIFactory1");
                string wanted = SystemInfo.graphicsDeviceName ?? "";
                IntPtr best = IntPtr.Zero;
                IntPtr desc = Marshal.AllocHGlobal(512);
                try
                {
                    var enumAdapters = Fn<EnumAdapters1Fn>(factory, 12);
                    for (uint i = 0; i < 8 && enumAdapters(factory, i, out IntPtr a) == 0; i++)
                    {
                        string name = "";
                        if (Fn<GetDesc1Fn>(a, 10)(a, desc) == 0) name = Marshal.PtrToStringUni(desc);
                        bool match = name.Length > 0 && (wanted.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (best == IntPtr.Zero || match)
                        {
                            if (best != IntPtr.Zero) Fn<ReleaseFn>(best, 2)(best);
                            best = a;
                            if (match) break;
                        }
                        else Fn<ReleaseFn>(a, 2)(a);
                    }
                }
                finally { Marshal.FreeHGlobal(desc); Fn<ReleaseFn>(factory, 2)(factory); }
                if (best == IntPtr.Zero) throw new Exception("no adapter");
                int hr = Fn<QueryInterfaceFn>(best, 0)(best, ref adapter3Id, out adapter3);
                Fn<ReleaseFn>(best, 2)(best);
                if (hr != 0) throw new Exception("IDXGIAdapter3 missing");
                query = Fn<QueryVideoMemoryInfoFn>(adapter3, 14);
            }

            internal bool Query(out long used, out long budget)
            {
                used = budget = -1;
                IntPtr info = Marshal.AllocHGlobal(32);
                try
                {
                    if (query(adapter3, 0, 0, info) != 0) return false;
                    budget = Marshal.ReadInt64(info, 0);
                    used = Marshal.ReadInt64(info, 8);
                    return used > 0;
                }
                finally { Marshal.FreeHGlobal(info); }
            }
        }

        // GPU-Last wie im Task-Manager ("3D"): Summe aller "GPU Engine(*engtype_3D)"-Zaehler.
        private sealed class Pdh : IDisposable
        {
            [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQueryW(string src, IntPtr user, out IntPtr query);
            [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr user, out IntPtr counter);
            [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(IntPtr query);
            [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);
            [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(IntPtr query);

            private const uint PDH_FMT_DOUBLE = 0x200, PDH_FMT_NOCAP100 = 0x8000, PDH_MORE_DATA = 0x800007D2;
            private IntPtr query, counter;
            private bool ok;

            internal Pdh()
            {
                ok = PdhOpenQueryW(null, IntPtr.Zero, out query) == 0
                     && PdhAddEnglishCounterW(query, @"\GPU Engine(*engtype_3D)\Utilization Percentage", IntPtr.Zero, out counter) == 0;
                if (ok) PdhCollectQueryData(query);
                if (!ok) Plugin.Log.LogInfo("Overlay: GPU load counter not available");
            }

            internal float Sample3D()
            {
                if (!ok || PdhCollectQueryData(query) != 0) return -1f;
                uint size = 0;
                uint r = PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out uint count, IntPtr.Zero);
                if (r != PDH_MORE_DATA || size == 0) return -1f;
                IntPtr buf = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (PdhGetFormattedCounterArrayW(counter, PDH_FMT_DOUBLE | PDH_FMT_NOCAP100, ref size, out count, buf) != 0) return -1f;
                    // PDH_FMT_COUNTERVALUE_ITEM_W (x64): Name-Zeiger (8), CStatus (4), Padding (4), double (8)
                    int stride = IntPtr.Size + 16;
                    double sum = 0;
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr item = IntPtr.Add(buf, i * stride);
                        int st = Marshal.ReadInt32(item, IntPtr.Size);
                        if (st != 0 && st != 1) continue;
                        sum += BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, IntPtr.Size + 8));
                    }
                    return (float)Math.Min(100.0, sum);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }

            public void Dispose()
            {
                if (query != IntPtr.Zero) PdhCloseQuery(query);
                query = IntPtr.Zero;
            }
        }
    }
}
