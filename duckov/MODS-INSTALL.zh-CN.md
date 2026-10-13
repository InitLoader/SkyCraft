DuckovCraft 安装教程

需要：Minecraft 26.3、Fabric Loader 0.19.5、Java 25，以及 Fabric API 0.162.0+26.3。

1. 先退出 Minecraft 和鸭科夫。

2. 在 Duckov.exe 所在目录中找到 Duckov_Data\Mods，创建 DuckovCraft 文件夹。把本包的 DuckovCraft.dll、info.ini、duckovcraft-assets 三个文件一起放进去。

3. 把 Minecraft端\mods 中的 DuckovCraft.jar 放进你的 Minecraft 实例的 mods 文件夹。Fabric API 也放在这里；本包没有附带时，请自行安装上面指定的版本。移出旧的桥接 JAR，避免重复加载。

4. 在 Minecraft 启动器的“JVM 参数 / Java 虚拟机参数”中添加下面这一行：

--enable-native-access=ALL-UNNAMED --add-exports=java.base/jdk.internal.misc=ALL-UNNAMED

鸭科夫连接设置已内置，无需再添加桥接参数。

5. 启动鸭科夫，在主菜单的 Mods 页面允许加载 Mod，并勾选 DuckovCraft - Minecraft。

6. 每次游玩先启动 Minecraft，停在主菜单，不要手动进入世界。再通过 Steam 启动鸭科夫并进入存档。连接完成后自动显示 MC 视角，MC 窗口会隐藏。

常用按键：
WASD 移动，空格跳跃，Shift 潜行，Ctrl 疾跑。
鼠标左键攻击/挖掘，右键使用/放置，数字键和滚轮切换快捷栏。
E 打开 MC 背包，F5 切换第一/第三人称。
R 鸭科夫交互，F6 鸭科夫背包，F10 鸭科夫菜单。
F9 开关桥接，关闭后恢复鸭科夫原版视角。

正常退出鸭科夫后，等待 MC 保存并关闭。MC 背包和方块保存在该实例的 saves\SulfurCraft；这是沿用的存档目录，不用改名。鸭科夫继续使用自己的存档。

本包对应鸭科夫 Mod 0.1.3。两边背包独立，MC 盾牌挡鸭科夫攻击的功能未接入；联机未验证。

如果没显示 MC 视角，检查两边版本、JAR 位置、上面的 JVM 参数和官方 Mod 是否启用。如果安装了 RenoDX DLSS 附加组件，黑屏时可退出游戏并把 renodx-dlss.addon64 移到备份目录后重试。
