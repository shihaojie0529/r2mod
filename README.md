# r2mod

《雨中冒险2》mod 仓库，每个 mod 都放在 `mods/` 下的独立目录里。

| Mod | 内容 | 联机要求 |
|---|---|---|
| [小黑大哥合集](mods/XiaoHeiDaGeHeJi/README.md) | 小黑、伟哥、杰哥、崔哥四位幸存者 | 所有玩家安装相同版本 |
| [道具装备管理面板](mods/GearPanel/package/README.md) | 对局中按 F6，增删改玩家的道具和主动装备 | 只需房主安装 |

**[下载小黑大哥合集安装包](https://github.com/shihaojie0529/r2mod/releases/latest)**

## 目录

每个 mod 的结构相同：

```text
mods/<Mod>/
  src/         插件代码和 .csproj
  package/     r2modman 包的 manifest、说明和图标
  package.py   构建并打包，安装包输出到 dist/
  dist/        安装包（zip 不进 Git）
```

小黑大哥合集另有 `art/`（美术生成脚本）、`docs/`（技能设定）和 `tests/`（游戏内检查）。

## 构建

需要 .NET SDK 和 Python 3。在对应的 mod 目录运行：

```bash
python3 package.py
```
