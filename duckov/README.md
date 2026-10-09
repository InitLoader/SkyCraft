# DuckovCraft (experimental)

DuckovCraft 将原版 Minecraft 客户端的移动、手部、背包、物品、方块、近战和 F5 第三人称角色接入《逃离鸭科夫》。Duckov 端由官方 `Duckov.Modding.ModManager` 加载 `DuckovCraft.ModBehaviour`，不依赖 BepInEx 或 Harmony。

基于本机 Duckov Unity 6000.3.23f1 的真实程序集构建。Minecraft 使用既有的 26.3 / Fabric 0.19.5 桥接客户端，Fabric 仅用于 Minecraft 端。它沿用 SulfurCraft 的客户端兼容配置，通过 `-Dskycraft.link=Local\DuckovCraft_v1` 连接独立映射；其世界内部目录名仍为 `SulfurCraft`，存放在 DuckovCraft 自己的目录内。

## 安装和启动

将包内所有内容解压到 `Duckov.exe` 所在目录。双击 `启动鸭科夫MC.bat`，启动器通过 Steam 启动 Duckov，并运行内置 Java 25 和 Minecraft。第一次请在 Duckov 主菜单的 Mods 页面允许加载 Mod，并启用 **DuckovCraft - Minecraft**，然后进入存档。启动器不修改原有 Mod、账户信息或 Duckov 存档。

官方 Mod 目录为 `Duckov_Data/Mods/DuckovCraft`。它包含 `DuckovCraft.dll`、`info.ini` 和 `duckovcraft-assets`。设置首次生成到该目录的 `settings.json`；区域映射保存为 `worlds.tsv`。

Minecraft 独立存档在 `DuckovCraft/minecraft/saves/SulfurCraft`，首次启动创建空镜像世界。默认玩家名 **Steve**。正常退出 Duckov 后 Minecraft 保存并关闭。首次不启用官方 Mod 时，Minecraft 会等待连接；这时可正常关闭其后台 Java 进程或启用 Mod 后继续连接。

| 操作 | 按键 |
| --- | --- |
| 移动、跳跃、潜行、疾跑 | Minecraft 原版按键 |
| MC 背包 | E |
| MC 使用、放置、攻击、挖掘 | 原版鼠标左右键 |
| MC 第一人称、第三人称背面、正面 | F5 |
| Duckov 原生交互 / 取消正在进行的交互 | R |
| Duckov 原生背包 | F6 |
| Duckov 菜单 | F10 |
| 开关桥接并恢复原生控制 | F9 |

打开 Duckov 的菜单、背包或对话时，桥接暂时恢复原生控制，关闭后重新接管。Minecraft 背包使用独立物品体系，Duckov 的任务物品、仓库和交易仍通过原生界面操作。

0.1.1 修正原生交互前清空目标的问题；读条期间停止向 MC 转发移动和攻击输入。桥接接管时暂时关闭原生景深，并持续隐藏本地玩家的原生渲染器、枪械激光线和命中标记，关闭桥接后恢复。MC 界面按 sRGB 读取，避免在线性颜色空间中发白。`DisableDepthOfField` 可在 `settings.json` 配置。

## 范围与验证边界

这是首个实验版本。MC 方块、实体和第三人称角色在 Duckov 世界中绘制；原生地形碰撞按附近区域传给 MC，移动沿用已验证的渲染时间同步。MC 近战及原版弓箭命中通过原生 Health.Hurt 结算；原生最终伤害转给 MC，MC 死亡通知原生死亡流程，并保留 Duckov 基地免死规则。

MC 挖掘针对放置的 MC 方块，Duckov 地形没有接入破坏。两边背包不互相转换。原生伤害经 Duckov 护甲结算后再进入 MC 伤害体系，双重护甲和伤害倍率需要实机调整；原生治疗、食物、体力、载具、特殊梯子及联机兼容尚未接入。请以单人测试为准，其他相机或控制 Mod 可能争夺控制。

MC 方块保留物理碰撞，但暂不刷新 Duckov 的 AI 寻路图；原生场景的不可读取 Mesh 不支持该扫描，0.1.1 移除了由放置触发的扫描错误。原生 AI 路径绕行尚未接入。

构建和共享内存检查不能替代实际走路、碰撞、放置、F5、原生交互及战斗验收。

## 构建

```powershell
dotnet build duckov/DuckovCraft.csproj -c Release '-p:DuckovDir=D:\soft\steam\steamapps\common\Escape from Duckov'
dotnet run --project duckov/tests/TransportTests.csproj -c Release
```

`duckov/assets/duckovcraft-assets` 复用 `sulfur/assets/Assets` 中保存的材质与着色器构建产物；无需运行场景重建脚本。游戏 DLL 不随 Mod 分发。

官方加载接口参考：[Duckov Modding Example](https://github.com/xvrsl/duckov_modding/blob/main/README_EN.md)。
