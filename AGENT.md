# AGENT.md — SmartFillMonitor 项目规则

## PowerShell 转义问题

当需要输出包含特殊字符（引号、反斜杠、换行）的原始字符串时，使用 here-string 语法避免转义：

```powershell
# ✅ 正确：here-string，内部不用转义
$code = @"
public class Foo {
    string path = "C:\Users\test";
}
"@

# ❌ 错误：普通字符串，引号和反斜杠需要转义，容易出错
$code = "public class Foo {`r`n    string path = `"C:\\Users\\test`";`r`n}"
```

**规则**：任何多行字符串或包含引号/路径的代码块，默认使用 `@" ... "@` 输出。

## C# 规则

- 编辑时做最小化精确替换，只改需要改的部分，不要大块删除+重建
- 缩进保持一致（4空格）

## 数据库

- FreeSql + SQLite，WAL 模式
- `InitializeCoreService()` 必须在 `ConfigLogging()` 之前调用
