using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

static class Program
{
    static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            if (!e.Name.StartsWith("dnlib", StringComparison.OrdinalIgnoreCase)) return null;
            using (var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("dnlib.dll"))
            {
                var buf = new byte[st.Length];
                st.Read(buf, 0, buf.Length);
                return Assembly.Load(buf);
            }
        };
        int code = Run();
        Console.WriteLine();
        Console.WriteLine("按任意键退出...");
        try { Console.ReadKey(true); } catch (InvalidOperationException) { }
        return code;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int Run()
    {
        Console.WriteLine("=== PCL2 下载功能修复工具 ===");
        string self = Assembly.GetExecutingAssembly().Location;
        string dir = Path.GetDirectoryName(self);
        var files = Directory.GetFiles(dir, "*.exe")
            .Where(f => !string.Equals(f, self, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Console.WriteLine("扫描目录：" + dir);

        int fixedCount = 0, failed = 0, okCount = 0;
        foreach (var f in files)
        {
            string name = Path.GetFileName(f);
            try
            {
                var r = Patcher.Patch(f);
                switch (r)
                {
                    case PatchResult.NotPcl: break; // 不是 PCL，静默跳过
                    case PatchResult.AlreadyOk:
                        Console.WriteLine("[跳过] " + name + "：下载功能本来就是正常的，无需修复"); okCount++; break;
                    case PatchResult.Fixed:
                        Console.WriteLine("[成功] " + name + " 已修复（原文件备份为 " + name + ".bak）"); fixedCount++; break;
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine("[失败] " + name + "：" + ex.Message);
            }
        }
        if (fixedCount == 0 && failed == 0 && okCount == 0)
            Console.WriteLine("没有找到需要修复的 PCL2 程序。请把本工具放在 Plain Craft Launcher 2.exe 同一目录下再运行。");
        else if (fixedCount > 0)
            Console.WriteLine("完成。现在可以打开启动器，在“下载”里查看是否恢复。");
        return failed > 0 ? 1 : 0;
    }
}
