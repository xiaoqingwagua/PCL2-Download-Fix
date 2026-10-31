using System;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

enum PatchResult { NotPcl, AlreadyOk, Fixed }

static partial class Patcher
{
    public static PatchResult Patch(string path)
    {
        ModuleDefMD m;
        try { m = ModuleDefMD.Load(path); }
        catch { return PatchResult.NotPcl; }

        using (m)
        {
            var pageInstall = m.Find("PCL.PageDownloadInstall", false);
            var pageLeft = m.Find("PCL.PageDownloadLeft", false);
            var modDownload = m.Find("PCL.ModDownload", false);
            if (pageInstall == null || pageLeft == null || modDownload == null) return PatchResult.NotPcl;

            bool needLoader = false, needLeft = false;

            // 1) LoaderInit：把“关闭下载”假加载器换回 ModDownload.DlClientListLoader
            var li = pageInstall.Methods.FirstOrDefault(x => x.Name == "LoaderInit" && x.HasBody);
            if (li == null) throw new Exception("找不到 LoaderInit，版本不兼容");
            var ins = li.Body.Instructions;
            var ldstr = ins.FirstOrDefault(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "关闭下载");
            if (ldstr != null)
            {
                needLoader = true;
                int s = ins.IndexOf(ldstr);
                int e = -1;
                for (int k = s + 1; k < ins.Count; k++)
                {
                    var i = ins[k];
                    if (i.OpCode == OpCodes.Newobj && ((IMethod)i.Operand).DeclaringType.Name.String.StartsWith("LoaderTask"))
                    { e = k; break; }
                }
                if (e < 0) throw new Exception("LoaderInit 结构与预期不符，版本不兼容");
                var field = modDownload.Fields.FirstOrDefault(f => f.Name == "DlClientListLoader");
                if (field == null) throw new Exception("找不到 DlClientListLoader，版本不兼容");
                for (int k = e; k >= s; k--) ins.RemoveAt(k);
                ins.Insert(s, Instruction.Create(OpCodes.Ldsfld, field));
                li.Body.SimplifyBranches();
                li.Body.OptimizeBranches();
                li.Body.UpdateInstructionOffsets();
            }

            // 2) PageDownloadLeft 构造函数：去掉隐藏“安装”页并强制跳转 Mod 页的代码
            var ct = pageLeft.Methods.FirstOrDefault(x => x.Name == ".ctor" && x.HasBody);
            if (ct == null) throw new Exception("找不到 PageDownloadLeft 构造函数，版本不兼容");
            var ci = ct.Body.Instructions;
            var getInstall = ci.FirstOrDefault(i => i.Operand != null && i.Operand.ToString().Contains("get_ItemInstall"));
            if (getInstall != null)
            {
                needLeft = true;
                int a = ci.IndexOf(getInstall) - 1; // 前一条是 ldarg.0
                for (int k = ci.Count - 2; k >= a; k--) ci.RemoveAt(k);
                ct.Body.UpdateInstructionOffsets();
            }

            bool needPack = PatchModpack(m);

            if (!needLoader && !needLeft && !needPack) return PatchResult.AlreadyOk;

            // 先写临时文件，成功后再备份并替换，避免写坏原文件
            string tmp = path + ".tmp";
            m.Write(tmp);
            m.Dispose();
            string bak = path + ".bak";
            if (!File.Exists(bak)) File.Copy(path, bak);
            File.Delete(path);
            File.Move(tmp, path);
            return PatchResult.Fixed;
        }
    }
}
