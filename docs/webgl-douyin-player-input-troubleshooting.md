# WebGL 抖音小游戏玩家触摸移动排查记录

## 当前结论

本问题不能只按普通 Unity 手机触摸处理。当前 WebGL 构建启用了
`STARK_UNITY_INPUT_OVERRIDE`，并在 `UIManager` 中给 `EventSystem` 添加了
`TTInputOverrideBypass`。玩家控制原代码仍只读取 `Input.touchCount` /
`Input.GetTouch` / 鼠标模拟路径，因此运行结果受 SDK 覆盖、EventSystem 初始化时序
和 UI 射线检测共同影响。

本次修复在 `PlayerController` 中将默认方案改为：

1. WebGL 真机优先监听 `TT.OnTouchStart`、`TT.OnTouchMove`、
   `TT.OnTouchEnd`、`TT.OnTouchCancel`。
2. 以已验证真机行为为准：当前抖音 WebGL 回调的 `TTTouch.screenY`
   直接传给 Unity 时方向正确，默认不得执行 `Screen.height - screenY`。
   `Invert Douyin Touch Y` 仅用于验证其他宿主或 SDK 版本的差异。
3. 默认在按下时记录触点与飞机各自的起始世界坐标，拖动过程中使用
   `飞机起点 + (当前触点 - 触点起点)` 作为飞机目标位置；按下不会令飞机瞬移。
4. 原生事件没有工作时，仍回退到 SDK 覆盖轮询，再回退到
   `UnityEngine.Input`。
5. 战斗移动仅被按钮、滚动区域和弹窗等交互 UI 拦截，不再被纯背景图或透明 HUD
   意外挡住。

## 已确认的问题点

| 优先级 | 问题点 | 证据 / 影响 |
| --- | --- | --- |
| 高 | 输入通路依赖 SDK 全局 `Input` 覆盖 | `ProjectSettings.asset` 已启用 `STARK_UNITY_INPUT_OVERRIDE`；SDK 的 `Input.cs` 通过 `EventSystem.current.currentInputModule.input` 取触摸。该对象未准备好或重载未生效时，玩家读不到触摸。 |
| 高 | 按 SDK 字面说明翻转 Y 会在当前真机上造成反向移动 | 首轮事件输入修复将回调做了 `Screen.height - screenY` 转换，真机结果是手指上移、飞机下移；当前默认已改为直接使用 `screenY`。 |
| 高 | 触点直达模式会在异处起拖时造成飞机瞬移 | 当前交互规则要求按下时保存触点与飞机起点，再按拖动偏移移动；默认使用 `PreserveInitialOffset`，`FollowFinger` 只保留为对照模式。 |
| 中 | UI 误判可能把战斗触摸吞掉 | 旧逻辑调用 `EventSystem.current.IsPointerOverGameObject`，任何可射线图片都可能挡住移动；UI 代码存在全屏背景和透明交互区域。 |
| 中 | SDK 覆盖和 Unity 鼠标模拟可能互相掩盖 | 触摸不可见时继续读模拟鼠标，容易出现编辑器正常、抖音 WebGL 不动，或只在某种进入路径下可动。 |

## 仍需真机排除的候选项

| 候选问题 | 典型现象 | 如何验证 | 下一步方案 |
| --- | --- | --- | --- |
| `TT.OnTouch*` 在目标宿主版本没有回调 | 完全没有 `Begin source=DouyinEvent` | 开启输入诊断，观察是否改由 `SdkTouchPolling` 或 `UnityTouch` 接管 | 将 `Touch Input Strategy` 改为 `SdkPollingOnly` 做对比；如原生事件始终无回调，调查 SDK 初始化/版本 |
| 另一宿主或升级后的 SDK 实际暴露顶部原点坐标 | 仅事件路径上下反向，其余路径方向正常 | 观察 `source=DouyinEvent` 且移动方向只在 Y 上相反 | 单独开启 `Invert Douyin Touch Y` 对照；不要同时更换输入策略 |
| SDK 触点像素尺寸与 Unity `Screen` 尺寸不一致 | 有移动日志，但飞机比例偏离或总贴边 | 比较日志中的 `pointer` 与 `screen=宽x高` | 在原生事件转换处加入按屏幕尺寸缩放 |
| EventSystem / `TTInputOverrideBypass` 初始化失败 | 诊断出现 polling unavailable 警告；UI 点击也异常 | 查看首次警告和 UI 是否可点击 | 保持原生事件主通路；另外在场景启动阶段统一创建 EventSystem |
| UI 层仍有未归类的阻挡组件 | 仅某些屏幕区域不能起拖 | 在不同区域首次按下，确认 `Begin` 是否缺失 | 将实际需要阻挡移动的 UI 类型补入交互判定，或战斗中关闭无关 Raycast |
| 飞机已收到目标但另有逻辑覆盖位置 | `Move` 日志目标坐标变化，画面飞机不动 | 对比目标坐标与 Transform/Rigidbody 最终位置 | 搜索其他写入玩家坐标的脚本，或记录 `SetPosition` 后位置 |
| 游戏未处于 `Playing` 状态 | 输入完全不执行但场景仍显示 | 检查关卡进入与结算状态 | 在 `GameManager` 状态变更处加诊断，修正进入战斗流程 |

## Inspector 切换方案

`warplane-01` / `warplane-02` 的 `PlayerController` 新增以下字段。新字段缺失旧序列化值时，
枚举的 `0` 值就是当前推荐默认值。

| 字段 | 值 | 用途 |
| --- | --- | --- |
| `Touch Input Strategy` | `DouyinEventsPreferred` | 推荐默认值。原生抖音回调优先，轮询和 Unity 输入兜底。 |
| `Touch Input Strategy` | `SdkPollingOnly` | 隔离原生回调问题，只测 `TTInputOverrideBypass` / 全局覆盖链路。 |
| `Touch Input Strategy` | `UnityLegacyOnly` | 完全绕开玩家脚本中的 SDK 覆盖读取，用于验证标准 WebGL/编辑器输入。 |
| `Pointer Tracking Mode` | `PreserveInitialOffset` | 推荐默认值。按下记录起点，手指移动多少，飞机从原位移动多少。 |
| `Pointer Tracking Mode` | `FollowFinger` | 对照方案。飞机直接到触点位置，会在异处起拖时跳动。 |
| `Invert Douyin Touch Y` | 关闭 | 当前真机验证后的推荐默认值，回调 Y 直接使用。 |
| `Invert Douyin Touch Y` | 开启 | 仅用于另一个宿主或 SDK 版本确认发生上下反向时的单变量对照。 |
| `Log Pointer Input Diagnostics` | 开启 | 真机测试临时开启，输出实际选中的输入源与节流后的坐标。发布前关闭。 |

## 日志判读

开启 `Log Pointer Input Diagnostics` 后，拖动飞机应出现：

```text
[PlayerInput] Begin source=DouyinEvent, strategy=DouyinEventsPreferred, tracking=PreserveInitialOffset, invertDouyinY=False, ...
[PlayerInput] Move source=DouyinEvent, pointer=(...), target=(...).
```

| 日志内容 | 含义 |
| --- | --- |
| `source=DouyinEvent` | 本次新增的抖音原生事件路径已工作。 |
| `source=SdkTouchPolling` | 原生事件未接管，但 SDK 覆盖轮询可用。 |
| `source=UnityTouch` 或 `source=UnityMouse` | 只有 Unity 回退路径接管，应重点核查 SDK 初始化/覆盖。 |
| `TTSDK polling input unavailable` | 全局覆盖读输入时异常，通常与 EventSystem 或 SDK 重载时序有关。 |
| 有持续 `Move`，但飞机画面不动 | 输入层已通，转查坐标缩放、相机边界夹取或外部覆盖位置。 |
| 完全没有 `Begin` | 转查回调注册、游戏状态、UI 起触区域或包体是否为最新构建。 |

## 真机回归顺序

1. 使用默认组合 `DouyinEventsPreferred + PreserveInitialOffset + Invert Douyin Touch Y 关闭`，开启诊断打一个开发包。
2. 在真机从主界面进入战斗后，分别从画面底部、中部、左右边缘拖动，保留日志。
3. 若仅表现为上下反向且日志为 `source=DouyinEvent`，只开启 `Invert Douyin Touch Y` 重打包做方向对照。
4. 若没有事件输入或仍不能移动，将输入策略只改为 `SdkPollingOnly` 重打包对比。
5. 若仍失败，改为 `UnityLegacyOnly` 对比；这一轮用于判断问题是否集中在 SDK 输入层。
6. 若需要确认问题是否来自相对拖动手感，再单独测试 `FollowFinger`；若坐标仍异常，按缩放/边界方向继续排查。

每次只切一个字段并保留对应包名和日志，后续才能明确是哪条输入通道生效，而不是重复覆盖已有修复。
