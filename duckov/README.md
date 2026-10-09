# DuckovCraft (experimental)

2026-10-09 验证状态：0.1.4 启动时出现黑屏和 D3D11 设备失效，盾牌测试没有通过。当前本机已回退到 0.1.3，用户确认 MC 视角恢复且没有再次黑屏。不要将 0.1.4 的构建成功视为可用性验证；失败原因仍在定位，详见 [启动失败与回退记录](VALIDATION.md)。

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

0.1.2 修正角色销毁/切场景后漏清理接管状态的问题，避免重复留下 HUD 隐藏令牌。退出桥接时恢复原生瞄准目标并强制刷新 Cinemachine 相机与焦点。脚底坐标改用 ECM2 原生 API；碰撞导出沿用原生角色实际碰撞层，等待 MC 消费脚底和身体周围的碰撞区域后再放行移动。下降轨迹穿过真实原生地面时触发位置同步恢复，避免漏碰撞直接落入地下。地面高度、碰撞准备状态与恢复次数会写入日志，仍需实机验证复杂区域与 F9 反复切换。

0.1.2 的 F9 恢复已由用户确认正常，但室外仍报告空气墙和悬空。0.1.3 为 `TerrainCollider` 接入独立高度网格导出：读取当前区域的原生网格与洞口，在网格中心查询原生碰撞面以匹配三角形对角线，不再用通用多方向射线把不同地形表面连在一起。此修复仍需在报告问题的室外位置复测，不能据编译或传输检查认定所有碰撞已正确。

用户确认 0.1.3 的空气墙与悬空均有改善。0.1.4 保留原生伤害事件中的攻击者和近战/枪械类型，交由 MC 原版盾牌进行方向、举盾和耐久判定；此前所有输入都丢失来源并作为普通伤害处理。配套 MC 客户端保留本机远距离攻击者的来源信息。毒、环境、效果、真实伤害和爆炸不冒充可格挡的正面攻击。若传输队列已满，伤害退回原生结算，并防止重复转发。盾牌防御仍需生存模式中面对敌人和背对敌人分别复测。

## 范围与验证边界

这是首个实验版本。MC 方块、实体和第三人称角色在 Duckov 世界中绘制；原生地形碰撞按附近区域传给 MC，移动沿用已验证的渲染时间同步。MC 近战及原版弓箭命中通过原生 Health.Hurt 结算；原生最终伤害转给 MC，MC 死亡通知原生死亡流程，并保留 Duckov 基地免死规则。

MC 挖掘针对放置的 MC 方块，Duckov 地形没有接入破坏。两边背包不互相转换。原生伤害经 Duckov 护甲结算后再进入 MC 伤害体系，双重护甲和伤害倍率需要实机调整；原生治疗、食物、体力、载具、特殊梯子及联机兼容尚未接入。请以单人测试为准，其他相机或控制 Mod 可能争夺控制。

MC 方块保留物理碰撞，但暂不刷新 Duckov 的 AI 寻路图；原生场景的不可读取 Mesh 不支持该扫描，0.1.1 移除了由放置触发的扫描错误。原生 AI 路径绕行尚未接入。

构建和共享内存检查不能替代实际走路、碰撞、放置、F5、原生交互及战斗验收。

## 构建

```powershell
dotnet build duckov/DuckovCraft.csproj -c Release '-p:DuckovDir=D:\soft\steam\steamapps\common\Escape from Duckov'
dotnet run --project duckov/tests/TransportTests.csproj -c Release '-p:DuckovDir=D:\soft\steam\steamapps\common\Escape from Duckov'
```

`duckov/assets/duckovcraft-assets` 复用 `sulfur/assets/Assets` 中保存的材质与着色器构建产物；无需运行场景重建脚本。游戏 DLL 不随 Mod 分发。

官方加载接口参考：[Duckov Modding Example](https://github.com/xvrsl/duckov_modding/blob/main/README_EN.md)。
