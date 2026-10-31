using System;
using System.Collections.Generic;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

static partial class Patcher
{
    const string Banned = "为遵守相关法律法规，PCL 不再提供 MC 下载功能，非常抱歉 qwq……";

    static bool HasBanned(MethodDef md)
    {
        return md.HasBody && md.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == Banned);
    }

    // 恢复整合包安装：类型分发 + 手动选择整合包文件
    static bool PatchModpack(ModuleDefMD m)
    {
        var t = m.Find("PCL.ModModpack", false);
        if (t == null) return false;
        bool changed = false;

        var full = t.Methods.FirstOrDefault(x => x.Name == "ModpackInstall" && x.Parameters.Count == 3 && HasBanned(x));
        if (full != null) { RestoreDispatch(t, full); changed = true; }

        var noArg = t.Methods.FirstOrDefault(x => x.Name == "ModpackInstall" && x.Parameters.Count == 0 && HasBanned(x));
        if (noArg != null) { RestoreSelectAndInstall(m, noArg); changed = true; }

        return changed;
    }

    // ModpackInstall(File, InstanceName, Logo)：把末尾的 throw 换回 switch 分发
    static void RestoreDispatch(TypeDef t, MethodDef full)
    {
        var ins = full.Body.Instructions;
        var L = ins.First(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == Banned);
        int li = ins.IndexOf(L);
        if (ins[li + 1].OpCode != OpCodes.Newobj || ins[li + 2].OpCode != OpCodes.Throw)
            throw new Exception("ModpackInstall 结构与预期不符，版本不兼容");
        var v = full.Body.Variables;
        Local zip = v[1], baseDir = v[2], num = v[4];
        Func<string, MethodDef> M = n =>
        {
            var r = t.Methods.FirstOrDefault(x => x.Name == n);
            if (r == null) throw new Exception("找不到 " + n + "，版本不兼容");
            return r;
        };

        full.Body.SimplifyBranches();
        ins.RemoveAt(li + 2);
        ins.RemoveAt(li + 1);

        // try 之外的收尾：ldloc.0; ret
        var endLoad = Instruction.Create(OpCodes.Ldloc, v[0]);
        var endRet = Instruction.Create(OpCodes.Ret);
        ins.Add(endLoad);
        ins.Add(endRet);
        foreach (var h in full.Body.ExceptionHandlers)
            if (h.HandlerType == ExceptionHandlerType.Finally && h.HandlerEnd == null) h.HandlerEnd = endLoad;

        var seq = new List<Instruction>();
        Action<int, string, int> Case = (k, name, argc) =>
        {
            var next = Instruction.Create(OpCodes.Nop);
            seq.Add(Instruction.Create(OpCodes.Ldloc, num));
            seq.Add(Instruction.CreateLdcI4(k));
            seq.Add(Instruction.Create(OpCodes.Bne_Un, next));
            seq.Add(Instruction.Create(OpCodes.Ldarg_0));
            seq.Add(Instruction.Create(OpCodes.Ldloc, zip));
            if (argc >= 3) seq.Add(Instruction.Create(OpCodes.Ldloc, baseDir));
            if (argc == 4 || argc == 5) seq.Add(Instruction.Create(OpCodes.Ldarg_1));
            if (argc == 5) seq.Add(Instruction.Create(OpCodes.Ldarg_2));
            seq.Add(Instruction.Create(OpCodes.Call, M(name)));
            seq.Add(Instruction.Create(OpCodes.Stloc, v[0]));
            seq.Add(Instruction.Create(OpCodes.Leave, endLoad));
            seq.Add(next);
        };
        Case(0, "InstallPackCurseForge", 5);
        Case(1, "InstallPackHMCL", 3);
        Case(2, "InstallPackMMC", 3);
        Case(3, "InstallPackMCBBS", 4);
        Case(4, "InstallPackModrinth", 5);
        Case(9, "InstallPackLauncherPack", 3);
        seq.Add(Instruction.Create(OpCodes.Ldarg_0));
        seq.Add(Instruction.Create(OpCodes.Ldloc, zip));
        seq.Add(Instruction.Create(OpCodes.Call, M("InstallPackCompress")));
        seq.Add(Instruction.Create(OpCodes.Stloc, v[0]));
        seq.Add(Instruction.Create(OpCodes.Leave, endLoad));

        // 原 ldstr 原地改成第一条指令，保证它作为 catch 出口的跳转目标仍然有效
        var first = seq[0];
        L.OpCode = first.OpCode;
        L.Operand = first.Operand;
        for (int k = 1; k < seq.Count; k++) ins.Insert(li + k, seq[k]);
        full.Body.OptimizeBranches();
        full.Body.UpdateInstructionOffsets();
    }

    // ModpackInstall()：恢复“选择整合包文件 → 后台安装”
    static void RestoreSelectAndInstall(ModuleDefMD m, MethodDef noArg)
    {
        // 复用 McSkinSelect 里已有的文件对话框调用所需的各个成员引用
        var skin = m.Find("PCL.ModMinecraft", false).Methods.First(x => x.Name == "McSkinSelect");
        var si = skin.Body.Instructions;
        var arrs = si.Where(i => i.OpCode == OpCodes.Newarr).ToList();
        object vtArr = arrs[0].Operand, strArr = arrs[1].Operand;
        object stelemVt = si.First(i => i.OpCode == OpCodes.Stelem).Operand;
        var calls = si.Where(i => i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Newobj).ToList();
        object vtCtor = calls.First(i => i.OpCode == OpCodes.Newobj && i.Operand.ToString().Contains("ValueTuple")).Operand;
        object opImp = calls.First(i => i.Operand.ToString().Contains("op_Implicit")).Operand;
        object nullCtor = calls.First(i => i.OpCode == OpCodes.Newobj && i.Operand.ToString().Contains("Nullable")).Operand;
        object selFile = calls.First(i => i.Operand.ToString().Contains("Dialogs::SelectFile")).Operand;
        object firstOrDef = calls.First(i => i.Operand.ToString().Contains("FirstOrDefault")).Operand;

        var body = noArg.Body;
        var ins = body.Instructions;
        object closureCtor = ins.First(i => i.OpCode == OpCodes.Newobj).Operand;
        var closureLocal = body.Variables[0];
        var closureDef = m.Find("PCL.ModModpack/_Closure$__0-0", false);
        var fileField = closureDef.Fields.First(f => f.Name == "$VB$Local_File");
        var lambda = closureDef.Methods.First(x => x.Name == "_Lambda$__0");
        var runInThread = m.Find("PCL.ModBase", false).Methods.First(x => x.Name == "RunInThread" && x.Parameters.Count == 1);
        var actionCtor = m.Import(typeof(Action).GetConstructors()[0]);
        var isNullOrEmpty = m.Import(typeof(string).GetMethod("IsNullOrEmpty", new[] { typeof(string) }));

        var path = new Local(m.CorLibTypes.String);
        body.Variables.Add(path);
        ins.Clear();
        body.ExceptionHandlers.Clear();

        Action<OpCode> A0 = op => ins.Add(Instruction.Create(op));
        Action<OpCode, object> A = (op, o) => ins.Add(new Instruction(op, o));

        A(OpCodes.Ldstr, "选择整合包压缩文件");
        A0(OpCodes.Ldc_I4_0);
        A0(OpCodes.Ldnull);
        A0(OpCodes.Ldc_I4_1);
        A(OpCodes.Newarr, vtArr);
        A0(OpCodes.Dup);
        A0(OpCodes.Ldc_I4_0);
        A0(OpCodes.Ldc_I4_3);
        A(OpCodes.Newarr, strArr);
        string[] ext = { "rar", "zip", "mrpack" };
        for (int k = 0; k < ext.Length; k++)
        {
            A0(OpCodes.Dup);
            ins.Add(Instruction.CreateLdcI4(k));
            A(OpCodes.Ldstr, ext[k]);
            A0(OpCodes.Stelem_Ref);
        }
        A(OpCodes.Ldstr, "整合包文件");
        A(OpCodes.Newobj, vtCtor);
        A(OpCodes.Stelem, stelemVt);
        A(OpCodes.Call, opImp);
        A(OpCodes.Newobj, nullCtor);
        A(OpCodes.Call, selFile);
        A(OpCodes.Call, firstOrDef);
        ins.Add(Instruction.Create(OpCodes.Stloc, path));
        ins.Add(Instruction.Create(OpCodes.Ldloc, path));
        A(OpCodes.Call, isNullOrEmpty);
        var cont = Instruction.Create(OpCodes.Nop);
        ins.Add(Instruction.Create(OpCodes.Brfalse, cont));
        A0(OpCodes.Ret);
        ins.Add(cont);
        A(OpCodes.Newobj, closureCtor);
        ins.Add(Instruction.Create(OpCodes.Stloc, closureLocal));
        ins.Add(Instruction.Create(OpCodes.Ldloc, closureLocal));
        ins.Add(Instruction.Create(OpCodes.Ldloc, path));
        A(OpCodes.Stfld, fileField);
        ins.Add(Instruction.Create(OpCodes.Ldloc, closureLocal));
        A(OpCodes.Ldftn, lambda);
        A(OpCodes.Newobj, actionCtor);
        A(OpCodes.Call, runInThread);
        A0(OpCodes.Ret);
        body.OptimizeBranches();
        body.UpdateInstructionOffsets();
    }
}
