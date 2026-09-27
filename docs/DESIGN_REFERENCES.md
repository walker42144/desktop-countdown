# 设计参考记录

本项目只参考公开产品思路，不复制第三方源代码、图标或视觉资产。

## Rainmeter

- 项目：https://github.com/rainmeter/rainmeter
- 参考：拖动锁定、鼠标穿透、桌面层级、位置保存、多显示器和皮肤式主题思路。
- 许可证：GPL-2.0。当前项目未引入其代码。

## ElevenClock

- 项目：https://github.com/marticliment/ElevenClock
- 参考：托盘优先的设置入口、秒级显示、网络时间校准、多显示器适配。
- 许可证：Apache-2.0。当前项目未引入其代码。

## DesktopClock

- 项目：https://github.com/danielchalmers/DesktopClock
- 参考：主题预设、字体与颜色实时预览、便携运行、倒计时与时区功能分层。
- 许可证：MIT。当前项目未引入其代码。

## 当前产品取舍

- 默认保持离线和本地存储，仅 NTP 校准会访问用户配置的时间服务器。
- 主题由程序原生矢量画刷生成，不依赖下载的皮肤资源。
- 核心倒计时每次根据目标绝对时间重新计算，不采用累计减秒。
