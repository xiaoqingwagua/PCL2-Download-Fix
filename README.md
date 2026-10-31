# PCL2 下载功能修复工具

PCL2（Plain Craft Launcher 2）正式版 2.99.0.1 移除了“原版游戏下载”和“整合包安装”。本工具在你本机的 PCL2 程序上打补丁，恢复这两项功能。

> 本项目只发布修复工具的源码和可执行文件，不包含、也不分发 PCL2 本体。PCL2 请从官方渠道获取。

<!-- VIDEO_PLACEHOLDER -->

## 恢复的功能

| 功能 | 修复前 | 修复后 |
| --- | --- | --- |
| 下载 → 原版游戏 | 标签被隐藏，强制跳到 Mod 页；版本列表加载时直接报错 | 正常显示版本列表并可安装 |
| 下载整合包（CurseForge / Modrinth） | 文件能下完，安装时提示“PCL 不再提供 MC 下载功能” | 正常安装 |
| 手动导入整合包（zip / mrpack） | 点击后只弹出红色提示 | 弹出文件选择框并安装 |

支持的整合包格式与 PCL2 Release 2.6.6 一致：CurseForge、Modrinth、HMCL、MultiMC、MCBBS、带启动器的压缩包、普通压缩包。

## 使用方法

1. 到 [Releases](../../releases) 下载 `PCL下载修复工具.exe`。
2. 先关闭正在运行的 PCL2。
3. 把工具放到 `Plain Craft Launcher 2.exe` 所在的文件夹。
4. 双击运行，看到 `[成功]` 后按任意键退出。
5. 打开 PCL2，进入“下载”页。

工具会自动扫描同目录下的所有 exe，只处理 PCL2，其他程序会跳过。

- 修复前会把原文件备份为 `Plain Craft Launcher 2.exe.bak`。想还原时，删掉修复后的 exe，再把 `.bak` 改回原名。
- 先写入临时文件，成功后才替换原文件。中途出错不会损坏原程序。
- 重复运行是安全的：已修复的项目会显示 `[跳过]`。
- 如果程序结构和预期不同，工具会提示“版本不兼容”并停止，不会强行修改。

运行环境：Windows 10/11，.NET Framework 4.8（系统自带，PCL2 本身也需要）。

## 已测试版本

| PCL2 版本 | SHA-256 | 结果 |
| --- | --- | --- |
| 正式版 2.99.0.1 (415) | `0db34e19d9eeb6d496d20fabe786ec16bbd36ba6b0ccdc8d29cb7e909c114397` | 通过 |

其他版本可能可用，但没有测试过。

## 原理

PCL2 是 .NET 程序。工具用 [dnlib](https://github.com/0xd4d/dnlib) 读取程序集，在中间语言（IL）层面修改四个方法：

1. `PageDownloadInstall.LoaderInit`：把只会抛异常的假加载器换回 `ModDownload.DlClientListLoader`。
2. `PageDownloadLeft` 构造函数：删掉隐藏“原版游戏”标签和强制跳转 Mod 页的代码。
3. `ModModpack.ModpackInstall(File, InstanceName, Logo)`：把末尾的 `throw` 换回按整合包类型分发的 `switch`。
4. `ModModpack.ModpackInstall()`：重建“选择文件 → 后台安装”的流程。

这四处的原始实现在 2.99.0.1 里大多还留着，只是入口被切断了。详细分析见 [docs/原理.md](docs/原理.md)。

## 自行构建

需要 .NET SDK 8 或更高版本：

```powershell
cd src/PCLDownloadFix
dotnet publish -c Release -o ../../out
```

产物是 `out/PCL下载修复工具.exe`。dnlib 会作为内嵌资源打进 exe，所以只有一个文件。

## 免责声明

- 本工具仅供学习和个人使用。PCL2 作者出于合规考虑移除了这些功能。使用本工具的风险由你自行承担。
- 本项目与 PCL2 及其作者 龙腾猫跃 无关。如果 PCL2 对你有帮助，请支持[官方](https://github.com/Meloong-Git/PCL)。
- 修改后的 PCL2 请自己使用，不要再分发。

## 致谢

- 本项目的分析、补丁、修复工具、演示视频的录制与剪辑由 [Claude Code](https://claude.com/claude-code)（Anthropic）完成。
- [dnlib](https://github.com/0xd4d/dnlib)，MIT 许可证。
- [ILSpy](https://github.com/icsharpcode/ILSpy)，用于反编译对比。

## 许可证

[MIT](LICENSE)
