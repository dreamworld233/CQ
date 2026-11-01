# CQ 初期应用图标

临时识别标记，不是最终游戏名称、世界观或商业美术定稿。采用深蓝绿底、金色盾形、双叶与星点，表达伙伴成长与战斗保护；本图为项目内程序绘制，不依赖未提交的临时角色/背景素材。

- `initial-icon.svg`：可编辑矢量源，1024×1024。
- `preview.png`：默认、圆形、自适应遮罩与小尺寸预览；不是手机实机截图。
- `../../Assets/Art/AppIcon/AppIcon.png`：默认/legacy/round完整图标。
- `../../Assets/Art/AppIcon/AppIconBackground.png`：Android自适应不透明背景。
- `../../Assets/Art/AppIcon/AppIconForeground.png`：Android自适应透明前景；在中心安全区内，避免被圆形遮罩裁切。

## 重新生成

需要 Python 3.9+ 和 Pillow。仓库脚本仅支持当前SVG使用的rect、M/L/C/Z路径与两色竖向渐变，不是通用SVG转换器。

在仓库根目录执行：

```sh
python -I Tools/Art/render_app_icon.py --source ArtSource/AppIcon/initial-icon.svg --output Assets/Art/AppIcon --preview ArtSource/AppIcon/preview.png
```

完整1024方形图标不预烘焙圆角；预览中的圆角/圆形是模拟启动器遮罩。PNG导入为Default、无mipmap、无有损压缩；不要移动或删除已配置纹理的meta GUID。

## Unity 设置

菜单 **CQ → 配置初期应用图标** 可重复应用默认图标和Android的legacy、round、adaptive六档密度。原Android配置菜单也会调用图标配置。自适应层顺序为0前景、1背景。

图标设置随ProjectSettings保存，后续重新构建APK时生效；不会修改已生成或已安装的旧APK。本轮仅验证资源与Unity配置，没有构建APK或验收手机桌面显示。替换图标后仍建议重新构建并在目标设备检查不同启动器遮罩与缓存。
