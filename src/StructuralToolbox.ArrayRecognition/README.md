# 组件阵列识别独立模块

- `STPVARRAYRECOGNIZE`：设置外轮廓断开净距 `G`，多次框选顶层闭合二维多段线或块参照，识别完整 `M×N` 阵列。
- `STPVARRAYTABLE`：插入与配重块统计表分开的组件阵列统计表。
- 自动配重块页面使用同一识别核心和同一份识别快照，命令为
  `STPVBALLASTDETECT`、`STPVBALLASTARRAYTABLE`、`STPVBALLASTGENERATE`。
- `M` 沿组件长边方向，`N` 沿组件短边方向；`1×1` 计为有效阵列。
- 不递归展开 Xref 或嵌套块；不支持对象、缺块、错位、重复位置和混合标高会明确报告。
- 识别、取消不会修改组件；插表单独使用一个事务，可由一次 `UNDO` 撤销。
