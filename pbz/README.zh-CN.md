# PBZCraft：影之刃零接入预览

本目录是针对 Steam build 26266538、Unreal 5.7 的本机接入试验。它将既有 Minecraft 26.3 客户端的相机、手持物、背包和方块接入 PBZ。它不是完整玩法版：原世界暂停，移动查询限制在初始位置水平三米、垂直两米内；未接入 PBZ 战斗、交互、场景切换。

最终使用方式见 [使用说明.txt](使用说明.txt)。输入桥接的协议测试与实际键鼠手感是两种验收：前者已测试，后者需要人工游玩，本次后台运行没有操作系统键鼠输入。

## 结构

- `host/`：.NET 8 Windows 桥接程序。复用 `duckov/src/Link` 中的协议 11 传输；独立映射 `Local\PBZCraft_v1`。
- `client/`：对配套 MC JAR 增加原生碰撞查询和诊断，替换原三角形碰撞 Mixin。原 JAR 中其他类不改动。
- `native/`：通过 UE4SS 官方 LuaMadeSimple API 暴露两条共享内存通道；没有自造 Unreal C++ 对象 ABI。
- `mod/Scripts/`：游戏线程上的相机、输入、胶囊查询、局部地面射线和原生网格绘制。
- `loader-config/`：本机已验证的 UE4SS 设置、PBZ 成员布局与签名。
- `tools/`：单次运行启动器、独立 MC 参数生成和只含 Mod/桥接程序的打包脚本。
- `checks/`：Windows 文件读取锁、输入映射和裂纹纹理转换检查。

## 构建

需要 Windows x64、.NET 8 SDK/Desktop Runtime、Java 25、Python（含 `pefile`）、Visual Studio x64 C++ 工具。现有 MC 运行目录需要 `client.json`、`minecraft/client.jar`、完整 assets/libraries 和 Java runtime，客户端 JAR 内的实际版本必须为 26.3。

UE4SS 使用 `profile.json` 固定的官方实验构建 `v3.0.1-1185-gdff435dd`；LuaMadeSimple/LuaRaw 头文件来自对应源码提交，fmt 为 11.2.0。PBZ 特殊布局和签名来源为 [UE4SS PR #1484](https://github.com/UE4SS-RE/RE-UE4SS/pull/1484)，本机可执行文件哈希单独固定。不要直接套用其他 UE4SS 版本。

从仓库根目录运行：

```powershell
dotnet build pbz/host/PBZCraft.Trial.csproj -c Release
dotnet run --project pbz/checks/HostChecks.csproj -c Release
python pbz/client/build.py --bundle '现有MC运行目录' --base-mod '配套原桥接JAR'
python pbz/native/make-import.py '固定版本的UE4SS.dll'
```

原桥接 JAR 必须匹配 `profile.json` 中的 `BaseModSHA256`，不是任意 Fabric Mod。它来自本机先前验证的配套客户端；本仓库当前其他未完成的 Forge/协议修改不属于此次 PBZ 构建输入。脚本会保留其已有类，只改变 Fabric 元信息、碰撞 Mixin 配置并加入本目录新增类。游戏本体、Java、此配套二进制 JAR、UE4SS 二进制和生成的游戏 SDK 不入库。

在 x64 Visual Studio 开发者命令提示符中设置 `UE4SS_SOURCE`（对应源码根目录）、`FMT_INCLUDE`（fmt include 目录），再运行 `pbz\native\build.cmd`。

打包前构建 Host、MC JAR 和 PBZNative.dll，然后运行：

```powershell
python pbz/tools/package_preview.py --stage '新的暂存目录' --zip '新的预览ZIP路径' --minecraft '现有MC运行目录' --ue4ss '固定版本ue4ss目录' --proxy '对应dwmapi.dll路径'
```

包只含桥接程序、游戏 Mod、MC Mod、Fabric API、UE4SS 加载文件和许可。它引用现有的 MC/Java 目录，不复制游戏本体、账户、存档或个人设置。输出目录和 ZIP 都必须不存在。

## 运行与边界

启动器检查原 EXE SHA256，拒绝不同的现有加载器配置和并行预览。启动前备份本地 PBZ SaveGames，结束后恢复文件并比对相对路径和 SHA256。MC 使用独立目录/存档。F9 结束两端，当前不支持恢复原版后继续同一进程游玩。异常退出仍保留存档备份。

同一时刻只有一个 MC 碰撞请求：MC 先处理自己的方块，再请求 Unreal 原生胶囊扫掠。查询超时返回零位移。自由轴保留 MC 原请求，避免 Unreal 厘米精度舍入让 MC 误判顶头而取消跳跃。当前没有完整台阶、斜坡边界、墙角、蹲起及全地图验收。

原生几何与 MC 透明 HUD 分别绘制。后台 `PrintWindow` 捕获不包含独立透明窗口；带 `-composite.png` 的验证图是原生窗口捕获与真实 MC HUD 缓冲的离线合成，不是单张原始游戏截图。捕获会使原生线程短暂停顿，不能用该阶段的碰撞查询峰值代表日常输入延迟。

动态人物和物品几何当前按 100ms 文件交换。原版半透明材质、动态图集更新、投射物、原生敌人伤害和完整交互均未验收。具体实机结果见 [VALIDATION.md](VALIDATION.md)。

当前材质复用游戏内已烹饪的 Paper2D 遮罩自发光材质。`PBZTexture` 只在本模块同步导入纹理期间，为新建的 transient Texture2D 设置最近邻采样；在导入返回后才修改 `Filter` 来不及改变已经创建的 GPU 采样器。亮度比例通过 `config.json/TextureBrightness` 传入，默认 0.15，用于当前暂停测试场景中的曝光匹配；它不代表已经适配任意地图的光照。
