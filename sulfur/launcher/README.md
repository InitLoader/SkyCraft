# 火湖 Minecraft 完整整合包 0.1.5

将压缩包里的 `启动火湖MC.bat`、`SulfurCraft` 文件夹和说明文件一起解压到火湖根目录，也就是 `Sulfur.exe` 所在目录。
双击 `启动火湖MC.bat`。启动器会安装缺少的 BepInEx 文件及配套桥接插件，通过 Steam 启动火湖，并用包内 Java 25 启动 Minecraft 26.3／Fabric 0.19.5。
进入火湖存档后即可使用 Minecraft 操作。正常退出火湖后，Minecraft 会保存并自动关闭。

包内已包含 Minecraft 游戏文件、Windows 64 位依赖、资源、Java、Fabric API 和配套桥接模组，使用时不需要打开 PCL2。
火湖本体和 Steam 仍需正常安装；首次运行时 Steam 如要求登录，请完成登录。

本整合包使用独立的新 Minecraft 存档，默认本地玩家名为 `Steve`。没有导入以前的背包、方块或账号登录信息。
新进度保存在 `SulfurCraft/minecraft/saves/SulfurCraft`，原来 PCL2 和开发客户端的存档不受影响。
皮肤使用该本地身份的默认皮肤；本包没有加入 Microsoft 账号登录界面。
可以在 `SulfurCraft/launcher.json` 中调整本地玩家名和 Minecraft 最大内存（默认 4096 MiB）。改变玩家名会改变本地玩家身份。

| 操作 | 按键 |
| --- | --- |
| 移动、跳跃、攻击、放置 | Minecraft 原版按键 |
| Minecraft 背包 | E |
| 第一／第三人称 | F5 |
| 火湖交互／长按交互 | R／长按 R |
| 切换原生火湖操作 | F9 |
| 火湖暂停菜单 | F10 |

再次双击启动器不会启动第二个包内 Minecraft。启动器发现其他桥接客户端正在运行时，会要求先正常退出旧客户端。
Java 启动日志在 `SulfurCraft/logs`；Minecraft 游戏日志在 `SulfurCraft/minecraft/logs`；桥接日志在 `BepInEx/LogOutput.log`。
启动器保留已有的 BepInEx 核心文件、其他插件和配置；若需替换旧桥接插件，会先备份到 `SulfurCraft/backups`。
如果此前用 F9 关闭了桥接，进入存档后按 F9 重新开启。

已在本机使用 Windows PowerShell 5.1 验证包内客户端启动、两端连接、自动创建新存档、`Steve` 玩家名、重复启动保护，以及退出火湖后的玩家和世界保存。
Java 参数文件也通过了中文工作目录、中文参数文件路径、空格、引号和反斜杠的实际解析检查。完整游玩效果沿用现有 0.1.5 验证范围。

此包保留当前 0.1.5 功能：已确认走路动画和位置同步。按你的选择，没有修改允许部分方块埋入非整数高度地面的放置规则。
原生地形挖掘、完整投射物、水和死亡流程以及多人联机仍未完成；门、梯子和第三人称等功能的完整体验仍需实际验证。

## 构建

`Build-Portable.ps1` 从已安装的合并版 Minecraft JSON 选取 Windows 运行库，校验官方 SHA1，并只复制当前资源索引所需的对象。
生成的 `files.json` 记录整合包文件的 SHA256。可用 Windows PowerShell 运行 `Start-SulfurCraft.ps1 -ValidateOnly` 校验包内文件。
构建输出应使用空目录，避免覆盖运行后产生的存档。运行路径根据启动器所在目录生成，未绑定构建机器上的盘符或目录。
Minecraft、Java 和 BepInEx 的二进制文件只放入本地整合包，不提交到源码仓库。

参数文件的引号与反斜杠处理遵循 [Java 25 启动器文档](https://docs.oracle.com/en/java/javase/25/docs/specs/man/java.html#java-command-line-argument-files)。
