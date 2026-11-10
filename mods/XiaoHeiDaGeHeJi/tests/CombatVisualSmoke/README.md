# 游戏内特效创建检查

这是独立的 BepInEx 检查插件，不包含在正式安装包中。只放进临时测试配置；执行完成会自动退出游戏。

在 `mods/XiaoHeiDaGeHeJi` 目录下，使用已安装游戏和 BepInEx 的路径编译：

```powershell
dotnet build tests/CombatVisualSmoke/CombatVisualSmoke.csproj -c Release "-p:GameDirectory=E:/steam/steamapps/common/Risk of Rain 2" "-p:BepInExDirectory=D:/project/r2mod/.tools/smoke-profile/BepInEx"
```

测试配置需要 BepInExPack、RoR2BepInExPack、HookGenPatcher、FixPluginTypesSerialization、本项目 DLL，以及 manifest 中的四个 R2API 模块和其依赖。将生成的 `CombatVisualSmoke.dll` 放入该配置的 `BepInEx/plugins`。用 Doorstop 4 启动并保留图形设备：

```powershell
& 'E:/steam/steamapps/common/Risk of Rain 2/Risk of Rain 2.exe' -force-d3d11 --doorstop-enabled true --doorstop-target-assembly 'D:/project/r2mod/.tools/smoke-profile/BepInEx/core/BepInEx.Preloader.dll'
```

日志必须出现 30 条 `SMOKE PASS` 和一条 `SMOKE COMPLETE`，且没有 `SMOKE FAIL`。

2026-11-06 本地游戏 1.5.0 的检查结果：12/12 通过。覆盖 Addressables 着色器加载及 GPU 支持、HUD 面板创建、可见插剑计数、飞剑网格和拖尾、金印、追踪弹、中文字体、两种枪线、金剑和落剑。原先 `No GUID record is available for shader name` 的异常没有在检查中重现。测试提前退出时，游戏可能报告仍有异步加载操作的 AssetBundle 无法立即卸载。

该检查直接调用特效初始化方法，不涵盖进局画面辨识度、完整技能命中、HUD 在战斗画面中的位置和多人同步。需要另行实测这些内容。


0.5.5 的验证在正常客户端启动模式下运行，并加载本机的 LobbySkinsFix 1.2.5：24/24 通过。新增四人的独立 display 结构、Awake 完成网格/材质/骨架绑定、首帧后未回到指挥官网格，以及战斗预制体双枪预隐藏、原生动画资源保留的检查。display 生命周期检查等待精英目录索引有效后再执行，以满足原版 CharacterModel 的初始化要求。检查期间未出现 CharacterModel 生命周期异常；游戏原有 ProBuilder 着色器 GUID 加载错误仍会在启动时出现。实际大厅构图及完整战斗仍需画面实测。


0.5.6 增加可选的游戏资源预览：启动参数追加 `--art-preview-out=D:/project/r2mod/mods/XiaoHeiDaGeHeJi/dist/weapon-art-after.png`，检查插件会用游戏中的实际网格和材质渲染九格图。三行分别是青剑／追踪弹／椅子、沙发／杠铃片／摇摇杯、金剑／小黑持枪／金印。固定灯光、视角、正交构图和超采样；每格独立缩放以便观察细节，不代表游戏中的相对尺寸。0.5.6 运行结果为 24 项常规检查加 1 项资源渲染检查，合计 25/25 通过；两份 Python 美术生成器重复执行所得文件一致，网格坐标和三角形索引验证通过。


0.5.7 游戏内检查 24/24 通过。HUD 检查改为验证附加提示挂在原版技能图标之上、不改动原图标精灵／颜色／启用状态、不创建背景 Image、不拦截鼠标，并跳过未绑定图像的图标。该检查使用构造的 SkillIcon；完整战斗中的位置、不同分辨率和多人观战仍需实测。


0.5.8 增加角色材质对照导出参数 `--character-preview-out=D:/project/r2mod/mods/XiaoHeiDaGeHeJi/dist/character-lighting-after.png`。四人单排离屏渲染使用相同摄像机和灯光，旁边的 `.materials.txt` 记录实际绑定的材质参数。两种预览同时启用时共 26 项检查通过。该对照用于核对材质提亮及亮部保护，不代表特定关卡、天气或战斗中的最终画面。


0.5.9 常规检查 25/25 通过。新增临时本地服务器上的击杀报告夹具，分别验证小黑和杰哥：同源但不同 DamageInfo 的间接伤害不刷新、友军击杀不刷新、精确直接伤害报告排队刷新、出招结束后恢复全部充能并清零计时、服务器独立扣费账本允许再次施放，以及结束追踪后旧报告不再触发。该检查直接构造击杀报告，不覆盖完整弹道、真实双客户端网络时序或关卡实战。


0.5.10 常规检查 25/25 通过。锁敌夹具验证正面可见敌人会被搜索选中，背后、超距、隔墙、已死亡目标不通过校验，无有效目标时返回空。新建角色在首帧属性计算前补齐测试视距；生产逻辑保留原生视距和可见性限制。直接击杀／间接伤害／友军过滤与服务器充能账本检查扩展到崔哥 Shift，并验证不会刷新其他技能槽；小黑、杰哥右键同时回归通过。检查使用构造目标、墙体与击杀报告，不涵盖完整动态追撞手感、门传送和真实双客户端网络时序。


0.5.11 常规检查 29/29 通过。新增准星俯仰前后飞行、横移、斜向限速、松键悬停、垂直视角横移及重力／飞行引用计数恢复检查；验证 Shift 基础冷却为 2 秒。瞬移胶囊检测覆盖无障碍 16 米、向上闪现、零向量拒绝、墙前截断、起点被障碍占据时拒绝及探测不移动角色。特效检查验证两条剑光、四个端点光环、三把残剑、材质支持及到时销毁。该检查使用游戏内夹具和实际材质，未覆盖实际关卡的连续按键手感与真实双客户端特效时序。


0.5.12 常规检查 30/30 通过；追加 `--lobby-preview-out=D:/project/r2mod/mods/XiaoHeiDaGeHeJi/dist/lobby-lighting-comparison.png` 时 31/31 通过。新增四人独立展示补光材质、缓存复用、战斗材质隔离与原有发光不降低检查。预览使用 SceneCatalog 的 lobby 场景 GUID，并按原生 PreGameController 的方式实例化经典模式 DefaultLobbyBackground；保留其场景灯光与背景，不添加预览白光。同一姿势和摄像机，上排原材质、下排展示补光材质。图像未包含 UI 和界面后处理，不能作为完整选人截图的逐像素对照。


0.5.13 使用 `--spectacle-preview-out=D:/project/r2mod/mods/XiaoHeiDaGeHeJi/dist/skill-spectacle-preview.png` 导出实际网格与材质的九格资源预览，38/38 项检查通过（不导出预览为 37 项）。新增透明加法 Shader 混合参数、六种家具模型和倍率、巨拳和法相／48 把环绕剑、四人位移拖尾、10 种联网特效注册／电弧及到期销毁检查。在本地服务器上为敌我身体实际生成网络身份，再验证冰箱 Slow80 和餐桌 Bleed；未生成网络身份的身体不能代表真实对局中的流血目标。预览为离屏资源渲染，各格独立构图，不代表同一画面中的相对尺寸。仍需在真实关卡中检验完整攻击手感、多人网络时序及多人同时开大的帧率。


0.5.14 常规检查 42/42 通过。新增崔哥原生屏障叠加（8%／12%）、200% 生命上限、原生衰减钩子的战斗保留与脱战慢衰减、0～6 层泵感减伤及与冲撞减伤的乘算／消耗后的保留、350%～1400% 蓄力曲线、服务器蓄力许可的一次性与时长上限／过期／非法数值拒绝、Shift 按键边沿激活注册，以及全队瞬间治疗的自身／远处友军／敌人／死亡目标筛选和 30%／50% 治疗比例。检查使用隔离服务器和原生 HealthComponent；菜单中 RecalculateStats 会因没有 Run 提前返回，因此夹具预先设置明确的生命上限再验证钩子，不代替真实关卡中的道具、难度修正、蓄力手感或双客户端同步实测。


0.5.16 游戏内检查 63/63 通过。新增小黑闪现终点序列化往返、往返位移路径不随消散漂移、双端粒子与光环、十五米法相真实渲染边界、不同视野角／画幅／俯仰下左侧投影构图，以及法相／位移／烘焙网格到期释放。特效目录扩展为 12 项，包含伟哥全选光阵与目标连线。生命周期检查使用与 Unity 延迟销毁一致的游戏时间，避免加载卡顿时真实时间先到造成误报。预览位于 dist/requested-vfx-0.5.16.png。构建保留已有 MMHOOK 框架兼容警告；检查不覆盖真实双客户端同步和完整关卡实战。


0.5.17 游戏内检查 66/66 通过。小黑大招改为单一联网驻地巨型分身；验证十五米身高、本人移动后位置不变、只有一个身体网格、360 度自动索敌、死亡转火、友军／超距／隔墙过滤、联网锚点与放大枪口一致、18～36 发调度及原生子弹实际命中。满高光夹具中 36 发全部发射，角色伤害设为 10、每发 250%，目标生命从 10000 降至 9100。菜单夹具显式创建原版 GlobalEventManager，保留原生命中处理。预览为 dist/requested-vfx-0.5.17.png。尚未覆盖真实双客户端延迟和完整关卡实战。
