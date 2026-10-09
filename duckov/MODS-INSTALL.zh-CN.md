# DuckovCraft 0.1.3 双端 Mod 包安装教程

本包用于把 Minecraft 的移动、第一人称手持物、背包、放置/挖掘、近战和 F5 第三人称角色接入《逃离鸭科夫》。鸭科夫端只使用游戏官方 Mod 加载器。Minecraft 端使用 Fabric。

本包采用 2026-10-09 用户确认能够进入 MC 视角、正常走动的 **DuckovCraft 0.1.3** 配套文件。MC 桥接文件名仍为 `sulfurcraft.jar`，这是沿用的文件名；按下面的参数设置后连接的是鸭科夫。本包没有包含尚未验收的 0.1.4 盾牌补丁。

## 1. 包内文件与准备条件

```text
Minecraft端/
  mods/
    sulfurcraft.jar
    fabric-api-0.162.0+26.3.jar
鸭科夫端/
  Duckov_Data/
    Mods/
      DuckovCraft/
        DuckovCraft.dll
        info.ini
        duckovcraft-assets
安装教程.md
文件校验.sha256
许可证/
  SkyCraft-MIT.txt
  Fabric-API-Apache-2.0.txt
```

需要自行安装：

| 项目 | 本包配套版本/条件 |
| --- | --- |
| 逃离鸭科夫 | 本机 Steam 安装版本于 2026-10-09 实测；允许官方 Mods 加载 |
| Minecraft Java 版 | **26.3** |
| Minecraft 加载器 | **Fabric Loader 0.19.5**；其他版本未按本包验证 |
| Minecraft Java 运行环境 | **Java 25** |
| Fabric API | **0.162.0+26.3**，包内已附 |

本包不包含 Minecraft/鸭科夫游戏本体、Fabric 安装器、Java、运行库、整合包启动器或存档。它需要用你自己的 Minecraft 启动器启动实例，再通过 Steam 启动鸭科夫。请使用一个独立 Minecraft 实例，便于隔离存档和其他模组；这份 MC Mod 不用于 Forge 1.20.1 的亡者世界整合包。

Fabric 安装器与安装说明：[Fabric 官方下载](https://fabricmc.net/use/installer/)。Fabric API 信息：[Fabric API 项目页](https://modrinth.com/mod/fabric-api)。Fabric API 的安装位置是 Minecraft 实例的 `mods` 文件夹。

## 2. 安装鸭科夫官方 Mod

1. 安装或更新文件前，正常退出鸭科夫和配套 Minecraft。
2. 找到 `Duckov.exe` 所在的游戏根目录。本机示例：

   ```text
   D:\soft\steam\steamapps\common\Escape from Duckov
   ```

3. 把包内 **`鸭科夫端` 文件夹里的 `Duckov_Data` 文件夹**复制到游戏根目录，合并同名目录。最终应当是：

   ```text
   Escape from Duckov/
     Duckov.exe
     Duckov_Data/
       Mods/
         DuckovCraft/
           DuckovCraft.dll
           info.ini
           duckovcraft-assets
   ```

4. 如果已经安装 DuckovCraft，只替换这三个文件。原来的 `settings.json` 和 `worlds.tsv` 保留；本包不会提供个人配置或区域映射。不要把 `鸭科夫端` 这个外层目录也放进游戏根目录。
5. 启动鸭科夫，在主菜单 **Mods** 页面允许加载 Mod，并启用 **DuckovCraft - Minecraft**。更新后已启用的情况下也请核对一次。

鸭科夫这一端不需要安装 BepInEx、Harmony 或 SKSE。包内 DLL 和 `duckovcraft-assets` 必须放在同一目录。

## 3. 安装 Minecraft Mod 与连接参数

1. 在你自己的启动器里创建 **Minecraft 26.3 + Fabric Loader 0.19.5** 实例，选择 **Java 25** 作为该实例的 Java 运行环境。首次先正常启动一次，到 Minecraft 主菜单，完成启动器/游戏自己的首次提示，然后正常退出。
2. 使用启动器的“打开实例文件夹/游戏目录”功能找到这个实例实际的游戏目录。如果开启了版本隔离，应使用该实例自己的目录。
3. 将包内 `Minecraft端/mods` 中的 **两个 `.jar` 文件**放进该实例的 `mods` 文件夹，保持 JAR 完整，不要解压。已有 Fabric API 时，只保留这一份匹配版本；移出其他 SkyCraft/SulfurCraft/DuckovCraft 桥接 JAR，避免同一 Mod ID 重复加载。
4. 在这个实例的 **JVM 参数 / Java 虚拟机参数**里添加以下内容：

   ```text
   --enable-native-access=ALL-UNNAMED --add-exports=java.base/jdk.internal.misc=ALL-UNNAMED -Dskycraft.host=sulfur -Dskycraft.link=Local\DuckovCraft_v1 -Dskycraft.startHidden=false -Dskycraft.quitWithSkyrim=true -Dskycraft.discordAppId=0
   ```

   这是 **Java 参数**，不是聊天命令、游戏内命令或 Minecraft 的 `--gameDir` 一类游戏参数。保留启动器原有必要参数；如果已有同名 `-Dskycraft.*` 参数，替换成这里的值，不要重复写两组。

   - `-Dskycraft.link=Local\DuckovCraft_v1` 选择鸭科夫连接通道，是必需项。反斜杠只写一个。
   - `-Dskycraft.host=sulfur` 选择现有 Unity 客户端兼容模式，参数名沿用 SulfurCraft；不要改成 `duckov`。
   - `-Dskycraft.startHidden=false` 让首次未连接时的 MC 窗口可见。连接鸭科夫后，MC 窗口会自动隐藏。
   - `-Dskycraft.quitWithSkyrim=true` 让鸭科夫退出后，已连接的 MC 正常保存并关闭，参数名同样沿用原项目。
5. 建议给 MC 分配约 **4 GB** 内存，玩家名可在自己的启动器中设置为 **Steve**。实例内没有加入本包以外的其他 Mod 是当前测试条件。

## 4. 每次启动与存档

1. 用自己的 Minecraft 启动器启动上述 Fabric 实例，等到主菜单。先不要手动进入一个普通 Minecraft 世界。
2. 通过 Steam 启动鸭科夫，确认官方 Mod 已启用，再进入你要玩的鸭科夫存档。
3. 等两端加载完成。连接后 MC 会自动创建或打开名为 **`SulfurCraft`** 的镜像世界，然后显示 MC 物品栏和手持物；首次创建需要一点时间。加载期间仍显示鸭科夫原版视角是正常的，等 MC 完成加载再判断。
4. Minecraft 玩家数据、背包和方块保存在 **该 MC 实例的 `saves/SulfurCraft`**；鸭科夫继续使用自己的原生存档。两边存档独立。本包不含测试存档，也不自动把旧整合包的存档搬进新实例。
5. 正常退出鸭科夫，等待配套 Minecraft 保存并关闭。尽量使用正常退出流程。

如果本机已经安装了之前的完整 DuckovCraft 整合包，可在退出两端后按第 2 节替换官方 Mod，把这两个 MC JAR 放到现有的 `DuckovCraft/minecraft/mods`，继续使用原来的 `启动鸭科夫MC.bat`。本次精简包自身不包含那个启动器及其运行环境。

## 5. 常用按键

| 功能 | 按键 |
| --- | --- |
| 走路、跳跃、潜行、疾跑 | MC 原版按键：WASD、空格、Shift、Ctrl |
| MC 背包 | E |
| 切换 MC 第一人称、第三人称背面、正面 | F5 |
| MC 攻击、挖掘 | 鼠标左键 |
| MC 使用、放置 | 鼠标右键 |
| 切换 MC 快捷栏 | 数字键 1–9、滚轮 |
| 鸭科夫原生交互 / 取消交互读条 | R |
| 鸭科夫原生背包、仓库等入口 | F6 打开原生背包；其他入口通过原生交互 |
| 鸭科夫菜单 | F10 |
| 开关桥接、恢复原版控制与 HUD | F9 |

对话、鸭科夫菜单和原生背包打开时，会暂时使用原生界面控制。关闭后重新接管。

## 6. 黑屏/未响应与连接排查

### 已在本机复现的 RenoDX DLSS 冲突

本机此前在 0.1.3、0.1.4 都出现过启动黑屏/未响应。ReShade 日志记录 **RenoDX DLSS 神经渲染启动后发生 `DEVICE_HUNG`**，随后 MC 连接断开。即使官方 Mods 页面中的 DuckovDLSS5 已关闭，根目录的 **`renodx-dlss.addon64`** 仍可能由 ReShade 加载。

本机已将这个文件备份移出加载路径，重启后用户确认 MC 视角、走动正常。不要为了安装这个包把已停用的文件重新放回根目录。

其他机器如有同样症状并安装了这个附加组件，可以先正常退出游戏，把根目录 `renodx-dlss.addon64` 移到游戏目录外的备份位置，再做对照。保留备份即可，不需要删除游戏或存档。只取消勾选官方 DLSS Mod 并不能保证外部附加组件停止加载。本包不包含也不会自动修改 ReShade/RenoDX 文件；这种处理只针对安装了该组件的情况。

本机现有备份位置：

```text
DuckovCraft/backups/render-conflict-20261009-142436/
```

### 没有进入 MC 视角

依次确认 Minecraft 实例仍在运行、Fabric/Java/游戏版本符合上表、两个 JAR 已放进实际实例的 `mods`、JVM 中的连接通道参数正确、鸭科夫官方 Mod 已启用、两端加载已完成。两端都已加载后，可用 F9 检查桥接开关；连续反复按会把刚开启的桥接再次关掉。

Minecraft 窗口在连接后隐藏是正常行为，不表示程序退出。需要显示 MC 窗口来处理首次提示或排查时，可在重启 MC 前临时添加 `-Dskycraft.showWindow=true`，排查后移除该参数。

### 日志位置

- 鸭科夫：`%USERPROFILE%/AppData/LocalLow/TeamSoda/Duckov/Player.log`。
- 自己的 Minecraft 实例：`logs/latest.log`，有崩溃时查看 `crash-reports`。
- 使用以前完整包启动器时：游戏根目录 `DuckovCraft/logs`。
- 安装了 ReShade 时：游戏根目录 `ReShade.log`，查看是否出现 `RenoDX DLSS`、`Device was lost`、`DEVICE_HUNG`。

## 7. 当前范围

MC 与鸭科夫背包独立；原生任务、仓库和交易继续通过鸭科夫界面操作。MC 挖掘针对 MC 方块，鸭科夫原生地形不可通过 MC 挖掘破坏。MC 方块有物理碰撞，但鸭科夫 AI 寻路绕行尚未接入。

**0.1.3 没有接入 MC 盾牌对鸭科夫近战/枪弹的方向防御。** 原生治疗、体力/饮食、特殊梯子和联机兼容也尚未完整接入。本包按单人使用验证，不能将这一包视为已支持联机。

已确认的结果是本机 MC 视角和走动恢复，以及此前 F9 原版恢复、室外空气墙/悬空有所改善；这些确认不能证明所有地图碰撞和长期稳定性均已正确。

## 8. 卸载

正常退出两端后，移出鸭科夫的 `Duckov_Data/Mods/DuckovCraft` 和 MC 实例中的 `sulfurcraft.jar` 即可。Fabric API 如仍被其他 Mod 使用则保留。保留 MC 的 `saves/SulfurCraft` 与原生存档，便于以后继续使用；不要为了卸载删除整个游戏目录。

来源与许可证：[SkyCraft 原项目](https://github.com/chasmlol/SkyCraft)、[本次适配 PR](https://github.com/InitLoader/SkyCraft/pull/1)、[Fabric API](https://modrinth.com/mod/fabric-api)。相应许可证随包附带。
